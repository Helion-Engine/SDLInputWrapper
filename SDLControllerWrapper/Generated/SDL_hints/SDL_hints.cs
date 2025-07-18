namespace SDLControllerWrapper.Generated.SDL_hints
{
    using global::SDLControllerWrapper.Generated.Shared;
    using System.Runtime.InteropServices;

    public static unsafe partial class SDL_hints
    {
        [DllImport(SDLControllerWrapper.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_SetHint([NativeTypeName("const char *")] sbyte* name, [NativeTypeName("const char *")] sbyte* value);
    }
}
