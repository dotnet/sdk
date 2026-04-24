// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli;

/// <summary>
///  Hosts the .NET runtime and provides the ability to load and invoke managed assemblies.
///  Designed to be initialized asynchronously so the runtime is ready when needed.
/// </summary>
internal sealed unsafe class ManagedHost : IDisposable
{
    private nint _hostContextHandle;
    private delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, int> _loadAssemblyAndGetFunctionPointer;
    private Task? _initTask;
    private readonly string _runtimeConfigPath;
    private readonly string _dotnetRoot;
    private bool _disposed;

    /// <summary>
    ///  Creates a new <see cref="ManagedHost"/> for the given runtime config.
    /// </summary>
    /// <param name="runtimeConfigPath">Path to the .runtimeconfig.json file.</param>
    /// <param name="dotnetRoot">Path to the .NET installation root.</param>
    public ManagedHost(string runtimeConfigPath, string dotnetRoot)
    {
        _runtimeConfigPath = runtimeConfigPath;
        _dotnetRoot = dotnetRoot;
    }

    /// <summary>
    ///  Starts initializing the .NET runtime asynchronously.
    /// </summary>
    public void StartInitialization()
    {
        _initTask = Task.Run(Initialize);
    }

    /// <summary>
    ///  Ensures the runtime is initialized, blocking if necessary.
    /// </summary>
    public void EnsureInitialized()
    {
        if (_initTask is null)
        {
            Initialize();
        }
        else
        {
            _initTask.GetAwaiter().GetResult();
        }
    }

    /// <summary>
    ///  Loads the specified assembly and invokes an [UnmanagedCallersOnly] method
    ///  with the default component entry point signature: int fn(nint args, int sizeBytes).
    /// </summary>
    /// <param name="assemblyPath">Full path to the managed assembly.</param>
    /// <param name="typeName">Fully qualified type name (e.g., "Microsoft.DotNet.Cli.Program, dotnet").</param>
    /// <param name="methodName">Method name to invoke.</param>
    /// <returns>The exit code returned by the managed method.</returns>
    public int InvokeMethod(string assemblyPath, string typeName, string methodName)
    {
        EnsureInitialized();

        if (_loadAssemblyAndGetFunctionPointer is null)
        {
            throw new InvalidOperationException("Runtime initialization failed - load_assembly_and_get_function_pointer is not available.");
        }

        nint functionPointer;

        nint assemblyPathNative = PlatformStringMarshaller.ConvertToUnmanaged(assemblyPath);
        nint typeNameNative = PlatformStringMarshaller.ConvertToUnmanaged(typeName);
        nint methodNameNative = PlatformStringMarshaller.ConvertToUnmanaged(methodName);

        try
        {
            // delegate_type_name = -1 means UNMANAGEDCALLERSONLY_METHOD, reserved = 0
            int result = _loadAssemblyAndGetFunctionPointer(
                assemblyPathNative,
                typeNameNative,
                methodNameNative,
                -1,
                0,
                (nint)(&functionPointer));

            if (result != 0)
            {
                throw new InvalidOperationException($"Failed to load assembly and get function pointer. HRESULT: 0x{result:X8}");
            }
        }
        finally
        {
            PlatformStringMarshaller.Free(assemblyPathNative);
            PlatformStringMarshaller.Free(typeNameNative);
            PlatformStringMarshaller.Free(methodNameNative);
        }

        var entryPoint = (delegate* unmanaged[Cdecl]<nint, int, int>)functionPointer;
        return entryPoint(0, 0);
    }

    /// <summary>
    ///  Runs the managed application using the hostfxr command-line hosting path.
    ///  This is the simplest way to invoke <c>dotnet.dll Program.Main(args)</c>.
    /// </summary>
    /// <param name="dotnetRoot">Path to the .NET installation root.</param>
    /// <param name="args">Command-line arguments (first element should be the app path).</param>
    /// <returns>The application exit code.</returns>
    public static int RunApp(string dotnetRoot, string[] args)
    {
        nint handle;

        var parameters = new Interop.hostfxr_initialize_parameters
        {
            size = sizeof(Interop.hostfxr_initialize_parameters),
            dotnet_root = PlatformStringMarshaller.ConvertToUnmanaged(dotnetRoot),
        };

        try
        {
            StatusCode result = Interop.hostfxr_initialize_for_dotnet_command_line(
                args.Length,
                args,
                in parameters,
                out handle);

            if (result != StatusCode.Success && handle == 0)
            {
                throw new InvalidOperationException($"hostfxr_initialize_for_dotnet_command_line failed. Status: {result} (0x{(uint)result:X8})");
            }

            try
            {
                StatusCode appResult = Interop.hostfxr_run_app(handle);
                return (int)appResult;
            }
            finally
            {
                Interop.hostfxr_close(handle);
            }
        }
        finally
        {
            PlatformStringMarshaller.Free(parameters.dotnet_root);
        }
    }

    private void Initialize()
    {
        nint dotnetRootNative = PlatformStringMarshaller.ConvertToUnmanaged(_dotnetRoot);
        nint runtimeConfigPathNative = PlatformStringMarshaller.ConvertToUnmanaged(_runtimeConfigPath);

        try
        {
            var parameters = new Interop.hostfxr_initialize_parameters
            {
                size = sizeof(Interop.hostfxr_initialize_parameters),
                dotnet_root = dotnetRootNative,
            };

            StatusCode result = Interop.hostfxr_initialize_for_runtime_config(
                runtimeConfigPathNative,
                in parameters,
                out _hostContextHandle);

            if (result != StatusCode.Success && _hostContextHandle == 0)
            {
                throw new InvalidOperationException($"hostfxr_initialize_for_runtime_config failed. Status: {result} (0x{(uint)result:X8})");
            }

            nint loadAssemblyDelegate;
            result = Interop.hostfxr_get_runtime_delegate(
                _hostContextHandle,
                Interop.hostfxr_delegate_type.hdt_load_assembly_and_get_function_pointer,
                out loadAssemblyDelegate);

            if (result != StatusCode.Success)
            {
                Interop.hostfxr_close(_hostContextHandle);
                _hostContextHandle = 0;
                throw new InvalidOperationException($"hostfxr_get_runtime_delegate failed. Status: {result} (0x{(uint)result:X8})");
            }

            _loadAssemblyAndGetFunctionPointer =
                (delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, int>)loadAssemblyDelegate;
        }
        finally
        {
            PlatformStringMarshaller.Free(dotnetRootNative);
            PlatformStringMarshaller.Free(runtimeConfigPathNative);
        }
    }

    public void Dispose()
    {
        if (!_disposed && _hostContextHandle != 0)
        {
            Interop.hostfxr_close(_hostContextHandle);
            _hostContextHandle = 0;
            _disposed = true;
        }
    }
}
