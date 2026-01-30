// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.Foundation;

namespace Windows.Win32.System.Com;

/// <summary>
///  Wraps an <see cref="IClassFactory"/> from a dynamically loaded assembly.
/// </summary>
internal sealed unsafe class ComClassFactory : IDisposable
{
    private readonly IClassFactory* _classFactory;

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
    }
}
