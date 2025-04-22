
namespace Microsoft.DotNet.Cli;

internal unsafe readonly struct HostFxr
{
    private readonly IntPtr _libHandle;
    private readonly delegate*<
        int, // argc
        byte**, // argv
        IntPtr, // hostfxr_initialize_parameters* parameters
        IntPtr*, // out hostfxr_handle* host_context_handle
        int> _initForCmdLine;
    private readonly delegate*<IntPtr, int> _runApp;

    private HostFxr(
        IntPtr libHandle,
        delegate*<int, byte**, IntPtr, IntPtr*, int> initForCmdLine,
        delegate*<IntPtr, int> runApp)
    {
        _libHandle = libHandle;
        _initForCmdLine = initForCmdLine;
        _runApp = runApp;
    }

    public static HostFxr Load(string hostFxrPath)
    {
        if (string.IsNullOrEmpty(hostFxrPath))
        {
            throw new ArgumentException("Hostfxr path cannot be null or empty.", nameof(hostFxrPath));
        }

        if (Path.GetFileNameWithoutExtension(hostFxrPath) is not ("libhostfxr" or "hostfxr"))
        {
            throw new ArgumentException("The provided path is not a valid hostfxr file.", nameof(hostFxrPath));
        }

        var libHandle = NativeLibrary.Load(hostFxrPath);
        var initForCmdLine = (delegate*<int, byte**, IntPtr, IntPtr*, int>)NativeLibrary.GetExport(libHandle, "hostfxr_initialize_for_dotnet_command_line");
        var runApp = (delegate*<IntPtr, int>)NativeLibrary.GetExport(libHandle, "hostfxr_run_app");

        return new HostFxr(libHandle, initForCmdLine, runApp);
    }

    public int Run(string dotnetPath, params string[] args)
    {
        string[] fullArgs = [dotnetPath, ..args];
        if (TryInitForCmdLine(fullArgs, out var ctxHandle) != 0)
        {
            throw new InvalidOperationException("Failed to initialize hostfxr");
        }
        return _runApp(ctxHandle);
    }

    private int TryInitForCmdLine(string[] args, out IntPtr ctxHandle)
    {
        int argc = args.Length;
        byte** argv = (byte**)Marshal.AllocHGlobal(argc * sizeof(byte*));
        try
        {
            for (int i = 0; i < argc; i++)
            {
                var argPtr = (byte*)Marshal.StringToHGlobalAnsi(args[i]);
                argv[i] = argPtr;
            }

            // Call the hostfxr function
            IntPtr tmpHandle = IntPtr.Zero;
            int rc = _initForCmdLine(argc, argv, IntPtr.Zero, &tmpHandle);
            ctxHandle = tmpHandle;
            return rc;
        }
        finally
        {
            for (int i = 0; i < argc; i++)
            {
                Marshal.FreeHGlobal((IntPtr)argv[i]);
            }
            Marshal.FreeHGlobal((IntPtr)argv);
        }
    }
}