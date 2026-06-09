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
internal unsafe struct ISetupFailedPackageReference : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xE73559CD, 0x7003, 0x4022, 0xB1, 0x34, 0x27, 0xDC, 0x65, 0x0B, 0x28, 0x0F);

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
                0xCD, 0x59, 0x35, 0xE7,
                0x03, 0x70,
                0x22, 0x40,
                0xB1, 0x34, 0x27, 0xDC, 0x65, 0x0B, 0x28, 0x0F
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetId"/>
    public HRESULT GetId(BSTR* pbstrId)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion"/>
    public HRESULT GetVersion(BSTR* pbstrVersion)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrVersion);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip"/>
    public HRESULT GetChip(BSTR* pbstrChip)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrChip);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage"/>
    public HRESULT GetLanguage(BSTR* pbstrLanguage)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLanguage);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch"/>
    public HRESULT GetBranch(BSTR* pbstrBranch)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrBranch);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetType"/>
    public HRESULT GetType(BSTR* pbstrType)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[8])(pThis, pbstrType);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId"/>
    public HRESULT GetUniqueId(BSTR* pbstrUniqueId)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, BSTR*, HRESULT>)_lpVtbl[9])(pThis, pbstrUniqueId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension"/>
    public HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension)
    {
        fixed (ISetupFailedPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference*, VARIANT_BOOL*, HRESULT>)_lpVtbl[10])(pThis, pfIsExtension);
    }

    /// <summary>
    ///  A reference to a failed package.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   You can enumerate all properties of basic types by casting to an <see cref="ISetupPropertyStore"/>.
    ///  </para>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("E73559CD-7003-4022-B134-27DC650B280F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupPackageReference.Interface
    {
        /// <inheritdoc cref="ISetupPackageReference.Interface.GetId"/>
        [PreserveSig]
        new HRESULT GetId(BSTR* pbstrId);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion"/>
        [PreserveSig]
        new HRESULT GetVersion(BSTR* pbstrVersion);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip"/>
        [PreserveSig]
        new HRESULT GetChip(BSTR* pbstrChip);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage"/>
        [PreserveSig]
        new HRESULT GetLanguage(BSTR* pbstrLanguage);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch"/>
        [PreserveSig]
        new HRESULT GetBranch(BSTR* pbstrBranch);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetType"/>
        [PreserveSig]
        new HRESULT GetType(BSTR* pbstrType);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId"/>
        [PreserveSig]
        new HRESULT GetUniqueId(BSTR* pbstrUniqueId);

        /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension"/>
        [PreserveSig]
        new HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension);
    }
}
