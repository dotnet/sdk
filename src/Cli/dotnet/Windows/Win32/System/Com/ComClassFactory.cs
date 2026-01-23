// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.Foundation;

namespace Windows.Win32.System.Com;

/// <summary>
///  Wraps an <see cref="IClassFactory"/> from a dynamically loaded assembly.
/// </summary>
internal sealed unsafe class ComClassFactory : IDisposable
{
    private readonly HMODULE _module;
    private readonly bool _unloadModule;
    private readonly IClassFactory* _classFactory;

    private delegate HRESULT DllGetClassObjectProc(
        Guid* rclsid,
        Guid* riid,
        void** ppv);

    /// <summary>
    ///  The class ID.
    /// </summary>
    public Guid ClassId { get; }

    private const string ExportMethodName = "DllGetClassObject";

    /// <summary>
    /// Initializes a new instance of the <see cref="ComClassFactory"/> class.
    /// </summary>
    public ComClassFactory(
        string filePath,
        Guid classId) : this(HMODULE.LoadModule(filePath), classId)
    {
        _unloadModule = true;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ComClassFactory"/> class.
    /// </summary>
    public ComClassFactory(
        HMODULE module,
        Guid classId)
    {
        _module = module;
        ClassId = classId;

        // Dynamically get the class factory method.

        // HRESULT DllGetClassObject(
        //   [in] REFCLSID rclsid,
        //   [in] REFIID riid,
        //   [out] LPVOID* ppv
        // );

        FARPROC proc = PInvoke.GetProcAddress(module, ExportMethodName);

        if (proc.IsNull)
        {
            Error.ThrowLastError();
        }

        IClassFactory* classFactory;
        Guid iid = IClassFactory.IID_Guid;

#if NETFRAMEWORK
        // In .NET Framework we need to use the delegate type to call the method.
        Marshal.GetDelegateForFunctionPointer<DllGetClassObjectProc>(proc.Value)(
            &classId,
            &iid,
            (void**)&classFactory).ThrowOnFailure();
#else
        ((delegate* unmanaged<Guid*, Guid*, void**, HRESULT>)proc.Value)(
            &classId,
            &iid,
            (void**)&classFactory).ThrowOnFailure();
#endif

        _classFactory = classFactory;
    }

    /// <summary>
    ///  Creates a class factory for a registered COM class with the given class ID.
    /// </summary>
    public ComClassFactory(Guid classId)
    {
        IClassFactory* classFactory;
        Guid iid = IClassFactory.IID_Guid;

        PInvoke.CoGetClassObject(
            &classId,
            CLSCTX.CLSCTX_INPROC_SERVER,
            (void*)null,
            &iid,
            (void**)&classFactory).ThrowOnFailure();

        _classFactory = classFactory;
    }

    /// <summary>
    ///  Tries to create an instance of the given <typeparamref name="TInterface"/>. Throws if unsuccessful.
    /// </summary>
    public ComScope<TInterface> CreateInstance<TInterface>()
        where TInterface : unmanaged, IComIID
    {
        ComScope<TInterface> scope = TryCreateInstance<TInterface>(out HRESULT result);
        result.ThrowOnFailure();
        return scope;
    }

    /// <summary>
    ///  Tries to create the interface for the given <typeparamref name="TInterface"/>.
    /// </summary>
    public ComScope<TInterface> TryCreateInstance<TInterface>(out HRESULT result)
        where TInterface : unmanaged, IComIID
    {
        Guid iid = IID.Get<TInterface>();
        ComScope<TInterface> scope = default;
        result = _classFactory->CreateInstance(null, &iid, scope);
        return scope;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        _classFactory->Release();
        if (_unloadModule && !_module.IsNull)
        {
            PInvoke.FreeLibrary(_module);
        }
    }
}
