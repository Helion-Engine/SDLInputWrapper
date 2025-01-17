﻿namespace SDLControllerWrapper
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public partial class SDLControllerWrapper
    {
#if LINUX
        internal const string LibraryName = "libSDL2.so";
#else
        internal const string LibraryName = "SDL2.dll";
#endif

#if !LINUX && !WINDOWS
        private static bool RegisteredResolver;
        private static IntPtr m_dllHandle = IntPtr.Zero;

        static SDLControllerWrapper()
        {
            RegisteredResolver = false;
            RegisterDllResolver();
        }

        internal static void RegisterDllResolver()
        {
            if (!RegisteredResolver)
            {
                NativeLibrary.SetDllImportResolver(typeof(SDLControllerWrapper).Assembly, ImportResolver);
                RegisteredResolver = true;
            }
        }

        private static string GetRuntimePath()
        {
#pragma warning disable IDE0046 // if/else collapsing produces very dense code here
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.Is64BitProcess)
                return "runtimes\\win-x64\\native\\";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Environment.Is64BitProcess)
                return "runtimes/linux-x64/native/";

            throw new NotSupportedException("This library does not support the current OS.");
#pragma warning restore IDE0046
        }

        private static string[] GetExpectedLibraryNames()
        {
#pragma warning disable IDE0046 // if/else collapsing produces very dense code here
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ["SDL2.dll"];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return ["libSDL2.so", "libSDL2-2.0.so"];

            throw new NotSupportedException("This library does not support the current OS.");
#pragma warning restore IDE0046
        }

        private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (libraryName == LibraryName)
            {
                if (m_dllHandle != IntPtr.Zero)
                {
                    return m_dllHandle;
                }

                string runtimePath = GetRuntimePath();
                string[] libraryNames = GetExpectedLibraryNames();

                foreach (string library in libraryNames)
                {
                    // e.g. appdir/libsdl2.so
                    if (NativeLibrary.TryLoad($"{baseDirectory}{library}", out m_dllHandle))
                    {
                        return m_dllHandle;
                    }

                    // e.g. appdir/runtimes/linux-x64/native/libsdl2.so
                    if (NativeLibrary.TryLoad($"{baseDirectory}{runtimePath}{library}", out m_dllHandle))
                    {
                        return m_dllHandle;
                    }
                }

                foreach (string primaryLibrary in libraryNames)
                {
                    // default runtime search paths
                    if (NativeLibrary.TryLoad(primaryLibrary, out m_dllHandle))
                    {
                        return m_dllHandle;
                    }
                }

                throw new DllNotFoundException($"Could not load a suitable substitute for DllImport {libraryName}.");
            }

            return IntPtr.Zero;
        }
#endif
    }
}
