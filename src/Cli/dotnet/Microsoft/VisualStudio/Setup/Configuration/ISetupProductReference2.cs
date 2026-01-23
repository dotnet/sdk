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
internal unsafe struct ISetupProductReference2 : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x279a5db3, 0x7503, 0x444b, 0xb3, 0x4d, 0x30, 0x8f, 0x96, 0x1b, 0x9a, 0x06);

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
                0xb3, 0x5d, 0x9a, 0x27,
                0x03, 0x75,
                0x4b, 0x44,
                0xb3, 0x4d, 0x30, 0x8f, 0x96, 0x1b, 0x9a, 0x06
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, uint>)_lpVtbl[2])(pThis);
    }

    // ISetupPackageReference methods
    /// <inheritdoc cref="ISetupPackageReference.Interface.GetId(BSTR*)"/>
    public HRESULT GetId(BSTR* pbstrId)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion(BSTR*)"/>
    public HRESULT GetVersion(BSTR* pbstrVersion)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrVersion);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip(BSTR*)"/>
    public HRESULT GetChip(BSTR* pbstrChip)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrChip);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage(BSTR*)"/>
    public HRESULT GetLanguage(BSTR* pbstrLanguage)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLanguage);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch(BSTR*)"/>
    public HRESULT GetBranch(BSTR* pbstrBranch)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrBranch);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetType(BSTR*)"/>
    public HRESULT GetType(BSTR* pbstrType)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[8])(pThis, pbstrType);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId(BSTR*)"/>
    public HRESULT GetUniqueId(BSTR* pbstrUniqueId)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, BSTR*, HRESULT>)_lpVtbl[9])(pThis, pbstrUniqueId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension(VARIANT_BOOL*)"/>
    public HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, VARIANT_BOOL*, HRESULT>)_lpVtbl[10])(pThis, pfIsExtension);
    }

    // ISetupProductReference methods
    /// <inheritdoc cref="ISetupProductReference.Interface.GetIsInstalled(VARIANT_BOOL*)"/>
    public HRESULT GetIsInstalled(VARIANT_BOOL* pfIsInstalled)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, VARIANT_BOOL*, HRESULT>)_lpVtbl[11])(pThis, pfIsInstalled);
    }

    // ISetupProductReference2 specific methods
    /// <inheritdoc cref="Interface.GetSupportsExtensions(VARIANT_BOOL*)"/>
    public HRESULT GetSupportsExtensions(VARIANT_BOOL* pfSupportsExtensions)
    {
        fixed (ISetupProductReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupProductReference2*, VARIANT_BOOL*, HRESULT>)_lpVtbl[12])(pThis, pfSupportsExtensions);
    }

    /// <summary>
    ///  Extends <see cref="ISetupProductReference"/> with additional product information.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupproductreference2"/></para>
    /// </remarks>
    [ComImport]
    [Guid("279a5db3-7503-444b-b34d-308f961b9a06")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupProductReference.Interface
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

        /// <inheritdoc cref="ISetupProductReference.Interface.GetIsInstalled(VARIANT_BOOL*)"/>
        [PreserveSig]
        new HRESULT GetIsInstalled(VARIANT_BOOL* pfIsInstalled);

        /// <summary>
        ///  Gets a value indicating whether the product supports extensions.
        /// </summary>
        /// <param name="pfSupportsExtensions">A value indicating whether the product supports extensions.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupproductreference2.getsupportsextensions">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetSupportsExtensions(VARIANT_BOOL* pfSupportsExtensions);
    }
}
