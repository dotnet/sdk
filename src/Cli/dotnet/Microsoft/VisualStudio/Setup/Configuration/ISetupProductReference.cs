// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;

// For some reason the XML comment refs for IUnknown can't be resolved on .NET Framework without
// explicitly using the fully qualified name, which falls afoul of the simplify warning.
using ComIUnknown = Windows.Win32.System.Com.IUnknown;

namespace Microsoft.VisualStudio.Setup.Configuration;

/// <inheritdoc cref="Interface"/>
internal unsafe struct ISetupProductReference : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xa170b5ef, 0x223d, 0x492b, 0xb2, 0xd4, 0x94, 0x50, 0x32, 0x98, 0x06, 0x85);

#if NETFRAMEWORK
    readonly Guid IComIID.Guid => Guid;
#else
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0xef, 0xb5, 0x70, 0xa1,
                0x3d, 0x22,
                0x2b, 0x49,
                0xb2, 0xd4, 0x94, 0x50, 0x32, 0x98, 0x06, 0x85
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, uint>)_lpVtbl[2])(pThis);
    }

    // ISetupPackageReference methods
    /// <inheritdoc cref="ISetupPackageReference.Interface.GetId(BSTR*)"/>
    public HRESULT GetId(BSTR* pbstrId)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion(BSTR*)"/>
    public HRESULT GetVersion(BSTR* pbstrVersion)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrVersion);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip(BSTR*)"/>
    public HRESULT GetChip(BSTR* pbstrChip)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrChip);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage(BSTR*)"/>
    public HRESULT GetLanguage(BSTR* pbstrLanguage)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLanguage);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch(BSTR*)"/>
    public HRESULT GetBranch(BSTR* pbstrBranch)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrBranch);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetType(BSTR*)"/>
    public HRESULT GetType(BSTR* pbstrType)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[8])(pThis, pbstrType);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId(BSTR*)"/>
    public HRESULT GetUniqueId(BSTR* pbstrUniqueId)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, BSTR*, HRESULT>)_lpVtbl[9])(pThis, pbstrUniqueId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension(VARIANT_BOOL*)"/>
    public HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, VARIANT_BOOL*, HRESULT>)_lpVtbl[10])(pThis, pfIsExtension);
    }

    // ISetupProductReference specific methods
    /// <inheritdoc cref="Interface.GetIsInstalled(VARIANT_BOOL*)"/>
    public HRESULT GetIsInstalled(VARIANT_BOOL* pfIsInstalled)
    {
        fixed (ISetupProductReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference*, VARIANT_BOOL*, HRESULT>)_lpVtbl[11])(pThis, pfIsInstalled);
    }

    /// <summary>
    ///  Represents a reference to a Visual Studio product.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupproductreference"/></para>
    /// </remarks>
    [ComImport]
    [Guid("a170b5ef-223d-492b-b2d4-945032980685")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupPackageReference.Interface
    {
        /// <inheritdoc cref="ISetupPackageReference.Interface.GetId(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetId(BSTR* pbstrId);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetVersion(BSTR* pbstrVersion);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetChip(BSTR* pbstrChip);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetLanguage(BSTR* pbstrLanguage);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetBranch(BSTR* pbstrBranch);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetType(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetType(BSTR* pbstrType);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetUniqueId(BSTR* pbstrUniqueId);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension(VARIANT_BOOL*)"/>
        [PreserveSig]
        new HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension);

        /// <summary>
        ///  Gets a value indicating whether the product package is installed.
        /// </summary>
        /// <param name="pfIsInstalled">A value indicating whether the product package is installed.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupproductreference.getisinstalled">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetIsInstalled(VARIANT_BOOL* pfIsInstalled);
    }
}
