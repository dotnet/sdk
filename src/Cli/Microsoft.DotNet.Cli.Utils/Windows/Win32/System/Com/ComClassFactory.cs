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
    private ComClassFactory(IClassFactory* classFactory) => _classFactory = classFactory;

    /// <inheritdoc cref="TryCreate(Guid, out ComClassFactory?, out HRESULT)"/>
    public static bool TryCreate(Guid classId, out ComClassFactory? factory)
        => TryCreate(classId, out factory, out _);

    /// <summary>
    ///  Attempts to create a class factory for the given class ID.
    /// </summary>
    /// <param name="classId">The guid of the class to create (CLSID).</param>
    /// <param name="result">The result of <see cref="PInvoke.CoGetClassObject"/>.</param>
    /// <returns><see langword="true"/> when the factory was successfully created.</returns>
    public static bool TryCreate(Guid classId, [NotNullWhen(true)] out ComClassFactory? factory, out HRESULT result)
    {
        IClassFactory* classFactory;
        Guid iid = IClassFactory.IID_Guid;

        result = PInvoke.CoGetClassObject(
            &classId,
            CLSCTX.CLSCTX_INPROC_SERVER,
            (void*)null,
            &iid,
            (void**)&classFactory);


        if (result.Failed || classFactory is null)
        {
            factory = null;
            return false;
        }

        factory = new ComClassFactory(classFactory);
        return true;
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
