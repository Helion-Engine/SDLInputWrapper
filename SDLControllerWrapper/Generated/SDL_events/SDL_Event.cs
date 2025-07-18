namespace SDLControllerWrapper.Generated.SDL_events
{
    using global::SDLControllerWrapper.Generated.Shared;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    public unsafe partial struct SDL_Event
    {
        [FieldOffset(0)]
        [NativeTypeName("Uint32")]
        public uint type;

        [FieldOffset(0)]
        public SDL_ControllerDeviceEvent cdevice;

        [FieldOffset(0)]
        public SDL_ControllerSensorEvent csensor;

        [FieldOffset(0)]
        [NativeTypeName("Uint8[56]")]
        public fixed byte padding[56];
    }
}
