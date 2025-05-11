namespace SDLControllerWrapper
{
    using Generated.SDL_gamecontroller;
    using Generated.SDL_joystick;
    using Generated.SDL_sensor;
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Threading;
    using System.Threading.Tasks;

    public unsafe class Controller : IDisposable
    {
        private bool _disposedValue;
        internal _SDL_GameController* _controller;
        private readonly SDLControllerWrapper _parent;
        private readonly object _lockObj = new object();

        /// <summary>
        /// The <see cref="Joystick"/> this game controller is correlated with
        /// </summary>
        public readonly int JoystickIndex;

        /// <summary>
        /// Whether the controller supports rumble effects
        /// </summary>
        public readonly bool HasRumble;

        /// <summary>
        /// Whether the game controller has a gyroscope sensor
        /// </summary>
        public readonly bool HasGyro;

        /// <summary>
        /// Whether the game controller has an accelerometer
        /// </summary>
        public readonly bool HasAccel;

        /// <summary>
        /// The name of the controller according to SDL
        /// </summary>
        public readonly string Name;

        private readonly bool[][] _buttonStates;
        private readonly float[][] _axisStates;
        private readonly DPad[] _dpadStates;
        private readonly float[][] _gyroStates;
        private readonly float[][] _accelStates;
        private readonly double[][] _gyroAbsolutePositions;
        private readonly double[] _gyroAbsolutePositionsImmediate;
        private readonly float _gyroSampleRate;
        private readonly Vector2[][] _stickAnglesAndDisplacements;

        private int _currentSample;
        private int _prevSample;
        private int _sampleCount;
        private bool _doNoiseCalibration;

        /// <summary>
        /// How many samples to keep.  Increasing this increases the smoothness
        /// of gyro movements at the cost of responsiveness, memory, and increased
        /// computational intensity.
        /// </summary>
        private const int GYRO_SAMPLES_KEPT = 16;
        /// <summary>
        /// Ring storing last GYRO_SAMPLES_KEPT raw gyro samples for smoothing
        /// </summary>
        private float[,] _prevGyroStates;
        private int _ringIndex = 0;

        /// <summary>
        /// Get the most recent sampled values for all buttons, indexed by <see cref="Button"/>
        /// </summary>
        public bool[] CurrentButtonValues => this._buttonStates[this._currentSample];

        /// <summary>
        /// Get the previous sampled values for all buttons, indexed by <see cref="Button"/>
        /// </summary>
        public bool[] PreviousButtonValues => this._buttonStates[this._prevSample];

        /// <summary>
        /// Get the most recent sampled values for all axes, on a scale of (-1, 1),
        /// indexed by <see cref="Axis"/>
        /// </summary>
        public float[] CurrentAxisValues => this._axisStates[this._currentSample];

        /// <summary>
        /// Get the previous sampled values for all axes, on a scale of (-1, 1),
        /// indexed by <see cref="Axis"/>
        /// </summary>
        public float[] PreviousAxisValues => this._axisStates[this._prevSample];

        /// <summary>
        /// Get the most recent angle and displacement (on a (0, 1) scale) of the thumbsticks,
        /// indexed by <see cref="Stick"/>
        /// </summary>
        public Vector2[] CurrentStickAnglesAndDisplacements => this._stickAnglesAndDisplacements[this._currentSample];

        /// <summary>
        /// Get the previous angle and displacement (on a (0, 1) scale) of the thumbsticks,
        /// indexed by <see cref="Stick"/>
        /// </summary>
        public Vector2[] PreviousStickAnglesAndDisplacements => this._stickAnglesAndDisplacements[this._prevSample];

        /// <summary>
        /// Get the most recent sampled value of the DPad
        /// </summary>
        public DPad CurrentDPadValue => this._dpadStates[this._currentSample];

        /// <summary>
        /// Get the previous sampled value of the DPad
        /// </summary>
        public DPad PreviousDPadValue => this._dpadStates[this._prevSample];

        /// <summary>
        /// Get the most recent sampled values from the gyro, if present,
        /// indexed by <see cref="GyroAxis"/>.  These are rotational rates measured in rad/s.
        /// </summary>
        public float[] CurrentGyroValues => this._gyroStates[this._currentSample];

        /// <summary>
        /// Get the previous sampled values from the gyro, if present,
        /// indexed by <see cref="GyroAxis"/>.  These are rotational rates measured in rad/s.
        /// </summary>
        public float[] PreviousGyroValues => this._gyroStates[this._prevSample];

        /// <summary>
        /// Get the estimated absolute position of the gyro, if present,
        /// indexed by <see cref="GyroAxis"/>.  Note that this will accumulate error over time.
        /// </summary>
        public double[] CurrentGyroAbsolutePosition => this._gyroAbsolutePositions[this._currentSample];

        /// <summary>
        /// Get the previous estimated absolute position of the gyro, if present,
        /// indexed by <see cref="GyroAxis"/>.  Note that this will accumulate error over time.
        /// </summary>
        public double[] PreviousGyroAbsolutePosition => this._gyroAbsolutePositions[this._prevSample];

        /// <summary>
        /// Get the most recent sampled values from the accelerometer, if present, 
        /// indexed by <see cref="AccelAxis"/>.  These are accelerations measured in m/s^2.
        /// </summary>
        public float[] CurrentAccelValues => this._accelStates[this._currentSample];

        /// <summary>
        /// Get the previous sampled values from the accelerometer, if present, 
        /// indexed by <see cref="AccelAxis"/>.  These are accelerations measured in m/s^2.
        /// </summary>
        public float[] PreviousAccelValues => this._accelStates[this._prevSample];

        /// <summary>
        /// Get or set the "noise" thresholds for the gyro.  Updates below this threshold are ignored.
        /// </summary>
        public readonly float[] GyroNoise;

        /// <summary>
        /// Get or set the constant-rate drift for the gyro.
        /// </summary>
        public readonly float[] GyroDrift;
        /// <summary>
        /// Should we even do smoothing?
        /// </summary>
        public readonly bool PerformSmoothing;
        /// <summary>
        /// The magnitude of the input required to stop applying smoothing.
        /// Note that at about half this threshold, 
        /// </summary>
        public readonly float SmoothingThreshold;

        internal unsafe Controller(SDLControllerWrapper parent, int index)
        {
            this._parent = parent;
            this._controller = SDL_gamecontroller.SDL_GameControllerOpen(index);
            this.JoystickIndex = SDL_joystick.SDL_JoystickInstanceID(SDL_gamecontroller.SDL_GameControllerGetJoystick(this._controller));
            this.HasRumble = SDL_gamecontroller.SDL_GameControllerHasRumble(this._controller) == Generated.Shared.SDL_bool.SDL_TRUE;

            this.HasGyro = SDL_gamecontroller.SDL_GameControllerHasSensor(this._controller, SDL_SensorType.SDL_SENSOR_GYRO) == Generated.Shared.SDL_bool.SDL_TRUE;
            if (this.HasGyro)
            {
                _ = SDL_gamecontroller.SDL_GameControllerSetSensorEnabled(this._controller, SDL_SensorType.SDL_SENSOR_GYRO, Generated.Shared.SDL_bool.SDL_TRUE);
                this._gyroSampleRate = SDL_gamecontroller.SDL_GameControllerGetSensorDataRate(this._controller, SDL_SensorType.SDL_SENSOR_GYRO);
            }

            this.HasAccel = SDL_gamecontroller.SDL_GameControllerHasSensor(this._controller, SDL_SensorType.SDL_SENSOR_ACCEL) == Generated.Shared.SDL_bool.SDL_TRUE;
            if (this.HasAccel)
            {
                _ = SDL_gamecontroller.SDL_GameControllerSetSensorEnabled(this._controller, SDL_SensorType.SDL_SENSOR_ACCEL, Generated.Shared.SDL_bool.SDL_TRUE);
            }

            this.Name = new string(SDL_gamecontroller.SDL_GameControllerName(this._controller));

            this._dpadStates = new DPad[3];
            this._buttonStates = new bool[3][];
            this._axisStates = new float[3][];
            this._gyroStates = new float[3][];
            this._gyroAbsolutePositions = new double[3][];
            this._accelStates = new float[3][];
            this._stickAnglesAndDisplacements = new Vector2[3][];
            this._gyroAbsolutePositionsImmediate = new double[3];
            this.GyroNoise = new float[3];
            this.GyroDrift = new float[3];
            this._prevGyroStates = new float[GYRO_SAMPLES_KEPT,3];

            for (int i = 0; i < 3; i++)
            {
                this._buttonStates[i] = new bool[(int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MAX];
                this._axisStates[i] = new float[(int)SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_MAX];
                this._gyroStates[i] = new float[3];
                this._gyroAbsolutePositions[i] = new double[3];
                this._accelStates[i] = new float[3];
                this._stickAnglesAndDisplacements[i] = new Vector2[2];
            }
        }

        internal unsafe void UpdateGyroAbsolutePositions(float* data)
        {
            lock (this._lockObj)
            {
                this._sampleCount++;

                if (this._doNoiseCalibration)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        this.GyroNoise[i] = Math.Max(this.GyroNoise[i], Math.Abs(data[i]));
                    }

                    return;
                }

                for (int i = 0; i < 3; i++)
                {
                    if (Math.Abs(data[i]) > this.GyroNoise[i])
                    {
                        this._gyroAbsolutePositionsImmediate[i] += unchecked((double)1 / this._gyroSampleRate * (data[i] - this.GyroDrift[i]));
                    }
                }
            }
        }

        internal static bool IsController(int joystickId)
        {
            return SDL_gamecontroller.SDL_IsGameController(joystickId) == Generated.Shared.SDL_bool.SDL_TRUE;
        }

        /// <summary>
        /// Poll the controller, updating the current and previous state of each button, axis, and sensor
        /// </summary>
        public void Poll()
        {
            int nextSample = (this._currentSample + 1) % 3;
            _SDL_GameController* controller = this._controller;

            for (int i = 0; i < this._axisStates[nextSample].Length; i++)
            {
                short axisValue = SDL_gamecontroller.SDL_GameControllerGetAxis(controller, (SDL_GameControllerAxis)i);
                this._axisStates[nextSample][i] = Math.Clamp(axisValue / (float)short.MaxValue, -1, 1);
            }

            float leftX = this._axisStates[nextSample][0];
            float leftY = this._axisStates[nextSample][1];
            float rightX = this._axisStates[nextSample][2];
            float rightY = this._axisStates[nextSample][3];

            this._stickAnglesAndDisplacements[nextSample][0].X = (float)Math.Atan2(leftY, leftX);
            this._stickAnglesAndDisplacements[nextSample][1].X = (float)Math.Atan2(rightY, rightX);

            this._stickAnglesAndDisplacements[nextSample][0].Y = (float)Math.Sqrt((leftY * leftY) + (leftX * leftX));
            this._stickAnglesAndDisplacements[nextSample][1].Y = (float)Math.Sqrt((rightY * rightY) + (rightX * rightX));

            byte dpadValue = 0;
            byte dpadMul = 1;
            for (int i = 0; i < this._buttonStates[nextSample].Length; i++)
            {
                byte state = SDL_gamecontroller.SDL_GameControllerGetButton(controller, (SDL_GameControllerButton)i);
                this._buttonStates[nextSample][i] = state != 0;
                if (i >= (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP && i <= (int)SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT)
                {
                    dpadValue += (byte)(state * dpadMul);
                    dpadMul *= 2;
                }
            }

            this._dpadStates[nextSample] = (DPad)dpadValue;

            if (this.HasGyro)
            {
                fixed (float* gyroStates = this._gyroStates[nextSample])
                {
                    _ = SDL_gamecontroller.SDL_GameControllerGetSensorData(controller, SDL_SensorType.SDL_SENSOR_GYRO, gyroStates, 3);
                }

                for (int i = 0; i < this._gyroStates[nextSample].Count(); i++)
                {
                    if (Math.Abs(this._gyroStates[nextSample][i]) < this.GyroNoise[i])
                    {
                        this._gyroStates[nextSample][i] = 0;
                    }
                }
                // If we're performing smoothing, modify the sample we just set
                // Merge this block with the above for loop if smoothed input is unresponsive
                if (PerformSmoothing)
                {
                    float magnitude = 0.0f;
                    float lower_threshold = this.SmoothingThreshold / 2;
                    for (int i = 0; i < this._gyroStates[nextSample].Count(); i++)
                    {
                        this._prevGyroStates[_ringIndex,i] = this._gyroStates[nextSample][i];
                        magnitude += this._gyroStates[nextSample][i]*this._gyroStates[nextSample][i];
                    }
                    this._ringIndex = (_ringIndex + 1) % GYRO_SAMPLES_KEPT;
                    float directWeight = (magnitude - lower_threshold) / (SmoothingThreshold - lower_threshold);
                    if (directWeight < 0)
                        directWeight = 0;
                    else if (directWeight > 1)
                        directWeight = 1;
                    float new_sample_x = 0.0f, new_sample_y = 0.0f, new_sample_z = 0.0f;
                    for (int i = 0; i < GYRO_SAMPLES_KEPT; i++)
                    {
                        new_sample_x += this._prevGyroStates[i,0];
                        new_sample_y += this._prevGyroStates[i,1];
                        new_sample_z += this._prevGyroStates[i,2];
                    }
                    this._gyroStates[nextSample][0] = this._gyroStates[nextSample][0]*directWeight + new_sample_x*(1.0f-directWeight) / GYRO_SAMPLES_KEPT;
                    this._gyroStates[nextSample][1] = this._gyroStates[nextSample][1]*directWeight + new_sample_y*(1.0f-directWeight) / GYRO_SAMPLES_KEPT;
                    this._gyroStates[nextSample][2] = this._gyroStates[nextSample][2]*directWeight + new_sample_z*(1.0f-directWeight) / GYRO_SAMPLES_KEPT;
                }

                lock (this._lockObj)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        this._gyroAbsolutePositions[nextSample][i] = this._gyroAbsolutePositions[this._currentSample][i] + this._gyroAbsolutePositionsImmediate[i];
                        this._gyroAbsolutePositionsImmediate[i] = 0;
                    }

                    this._sampleCount = 0;
                }
            }

            if (this.HasAccel)
            {
                fixed (float* accelStates = this._accelStates[nextSample])
                {
                    _ = SDL_gamecontroller.SDL_GameControllerGetSensorData(controller, SDL_SensorType.SDL_SENSOR_ACCEL, accelStates, 3);
                }
            }

            this._prevSample = this._currentSample;
            this._currentSample = (this._currentSample + 1) % 3;
        }

        /// <summary>
        /// Calibrate the controller gyro by monitoring drift and noise for the specified duration.
        /// All connected controllers will not send updates until this process is completed.
        /// User should be advised to put the controller on a stationary surface.
        /// Note that calibration will take _twice_ the specified amount of time, as we first check noise, then drift.
        /// </summary>
        /// <param name="durationMs">Duration for which to calibrate</param>
        /// <param name="callback">Function to call when calibration is finished</param>
        /// <returns>True if calibration has begun, false if it has not (for example, if the controller has no gyro)</returns>
        public bool CalibrateGyro(int durationMs, Action callback)
        {
            if (!this.HasGyro)
            {
                return false;
            }

            _ = Task.Run(() => this.CalibrateGyroTask(durationMs, callback));
            return true;
        }

        private void CalibrateGyroTask(int durationMs, Action callback)
        {
            this._parent.PollingEnabled = false;

            // Calibrate for noise
            this._doNoiseCalibration = true;
            this.ZeroGyroAbsolute();
            for (int i = 0; i < durationMs / 50; i++)
            {
                Thread.Sleep(50);
                SDL_gamecontroller.SDL_GameControllerUpdate();
            }
            this._doNoiseCalibration = false;
            this._sampleCount = 0;

            // Calibrate for drift over time
            Thread.Sleep(durationMs);
            for (int i = 0; i < durationMs / 50; i++)
            {
                Thread.Sleep(50);
                SDL_gamecontroller.SDL_GameControllerUpdate();
            }

            if (this._sampleCount > 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    this.GyroDrift[i] = (float)(this._gyroAbsolutePositionsImmediate[i] / this._sampleCount);
                }
            }
            this.ZeroGyroAbsolute();
            this._sampleCount = 0;

            this._parent.PollingEnabled = true;
            callback();
        }

        /// <summary>
        /// Make the device rumble for the specified duration; this will replace any current rumble effect.
        /// </summary>
        /// <param name="lowFrequency">Intensity value for low-frequency rumble motor</param>
        /// <param name="highFrequency">Intensity value for high-frquency rumble motor</param>
        /// <param name="durationMilliseconds">Duration of the rumble effect</param>
        public void Rumble(ushort lowFrequency, ushort highFrequency, uint durationMilliseconds)
        {
            _ = SDL_gamecontroller.SDL_GameControllerRumble(this._controller, lowFrequency, highFrequency, durationMilliseconds);
        }

        /// <summary>
        /// Resets the "absolute" values of the gyro to zero, clearing any accumulated error
        /// </summary>
        public void ZeroGyroAbsolute()
        {
            lock (this._lockObj)
            {
                for (int i = 0; i < this._gyroAbsolutePositions.Length; i++)
                {
                    for (int j = 0; j < this._gyroAbsolutePositions[i].Length; j++)
                    {
                        this._gyroAbsolutePositions[i][j] = 0;
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposedValue)
            {
                SDL_gamecontroller.SDL_GameControllerClose(this._controller);
                this._controller = null;
                this._disposedValue = true;
            }
        }

        ~Controller()
        {
            this.Dispose(disposing: false);
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
