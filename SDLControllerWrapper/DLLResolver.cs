namespace SDLControllerWrapper
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public partial class SDLControllerWrapper
    {
        internal const string LibraryName = "SDL2.dll";

        private static bool RegisteredResolver;
        private static IntPtr m_dllHandle = IntPtr.Zero;
        private static string? m_loadedFileName = null; // really just here for debugging

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

        private static readonly string RuntimePath =
             OperatingSystem.IsLinux()
                 ? Environment.Is64BitProcess
                     ? "runtimes/linux-x64/native/"
                     : "runtimes/linux-x86/native/"
                 : OperatingSystem.IsWindows()
                     ? Environment.Is64BitProcess
                         ? "runtimes\\win-x64\\native\\"
                         : "runtimes\\win-x86\\native\\"
                     : throw new NotSupportedException("Unsupported OS platform");

        private static readonly string[] LibraryNames =
            OperatingSystem.IsLinux()
                ? ["libSDL2.so", "libSDL2-2.0.so"]
                : OperatingSystem.IsWindows()
                    ? ["SDL2.dll"]
                    : throw new NotSupportedException("Unsupported OS platform");

        private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (m_dllHandle != IntPtr.Zero)
            {
                // already loaded
                return m_dllHandle;
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Try appdir/filename, then appdir/runtimes/<platform>/filename, then LD_LIBRARY_PATH (Linux only), then "default behavior".
            return TryLoadFromPath(baseDirectory, out m_dllHandle, out m_loadedFileName)
                || TryLoadFromPath($"{baseDirectory}{RuntimePath}", out m_dllHandle, out m_loadedFileName)
                || (OperatingSystem.IsLinux()
                    && TryLoadFromPath(Environment.GetEnvironmentVariable("LD_LIBRARY_PATH"), out m_dllHandle, out m_loadedFileName))
                || TryLoadFromPath(null, out m_dllHandle, out m_loadedFileName)
                ? m_dllHandle
                : throw new DllNotFoundException($"Could not load a suitable substitute for DllImport {libraryName}.");
        }

        private static bool TryLoadFromPath(string? basePath, out IntPtr foundPtr, out string? foundPath)
        {
            foundPath = null;
            foundPtr = IntPtr.Zero;

            foreach (string library in LibraryNames)
            {
                string path = $"{basePath}{library}";
                if (NativeLibrary.TryLoad(path, out foundPtr))
                {
                    foundPath = path;
                    return true;
                }
            }

            return false;
        }
    }
}
