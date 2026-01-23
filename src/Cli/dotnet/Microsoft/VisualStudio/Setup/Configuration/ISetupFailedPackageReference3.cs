// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

// For some reason the XML comment refs for IUnknown can't be resolved on .NET Framework without
// explicitly using the fully qualified name, which falls afoul of the simplify warning.
using ComIUnknown = Windows.Win32.System.Com.IUnknown;

namespace Microsoft.VisualStudio.Setup.Configuration;

/// <inheritdoc cref="Interface"/>
internal unsafe struct ISetupFailedPackageReference3 : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xEBC3AE68, 0xAD15, 0x44E8, 0x83, 0x77, 0x39, 0xDB, 0xF0, 0x31, 0x6F, 0x6C);

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
                0x68, 0xAE, 0xC3, 0xEB,
                0x15, 0xAD,
                0xE8, 0x44,
                0x83, 0x77, 0x39, 0xDB, 0xF0, 0x31, 0x6F, 0x6C
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, uint>)_lpVtbl[2])(pThis);
    }

    // ISetupPackageReference methods
    /// <inheritdoc cref="ISetupPackageReference.Interface.GetId(BSTR*)"/>
    public HRESULT GetId(BSTR* pbstrId)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion(BSTR*)"/>
    public HRESULT GetVersion(BSTR* pbstrVersion)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrVersion);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip(BSTR*)"/>
    public HRESULT GetChip(BSTR* pbstrChip)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrChip);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage(BSTR*)"/>
    public HRESULT GetLanguage(BSTR* pbstrLanguage)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLanguage);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch(BSTR*)"/>
    public HRESULT GetBranch(BSTR* pbstrBranch)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrBranch);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetType(BSTR*)"/>
    public HRESULT GetType(BSTR* pbstrType)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[8])(pThis, pbstrType);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId(BSTR*)"/>
    public HRESULT GetUniqueId(BSTR* pbstrUniqueId)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[9])(pThis, pbstrUniqueId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension(VARIANT_BOOL*)"/>
    public HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, VARIANT_BOOL*, HRESULT>)_lpVtbl[10])(pThis, pfIsExtension);
    }

    // ISetupFailedPackageReference2 methods
    /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetLogFilePath(BSTR*)"/>
    public HRESULT GetLogFilePath(BSTR* pbstrLogFilePath)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[11])(pThis, pbstrLogFilePath);
    }

    /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetDescription(BSTR*)"/>
    public HRESULT GetDescription(BSTR* pbstrDescription)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[12])(pThis, pbstrDescription);
    }

    /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetSignature(BSTR*)"/>
    public HRESULT GetSignature(BSTR* pbstrSignature)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[13])(pThis, pbstrSignature);
    }

    /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetDetails(SAFEARRAY*)"/>
    public HRESULT GetDetails(SAFEARRAY* ppsaDetails)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, SAFEARRAY*, HRESULT>)_lpVtbl[14])(pThis, ppsaDetails);
    }

    /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetAffectedPackages(SAFEARRAY*)"/>
    public HRESULT GetAffectedPackages(SAFEARRAY* ppsaAffectedPackages)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, SAFEARRAY*, HRESULT>)_lpVtbl[15])(pThis, ppsaAffectedPackages);
    }

    // ISetupFailedPackageReference3 specific methods
    /// <inheritdoc cref="Interface.GetAction(BSTR*)"/>
    public HRESULT GetAction(BSTR* pbstrAction)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[16])(pThis, pbstrAction);
    }

    /// <inheritdoc cref="Interface.GetReturnCode(BSTR*)"/>
    public HRESULT GetReturnCode(BSTR* pbstrReturnCode)
    {
        fixed (ISetupFailedPackageReference3* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference3*, BSTR*, HRESULT>)_lpVtbl[17])(pThis, pbstrReturnCode);
    }

    /// <summary>
    ///  Extends <see cref="ISetupFailedPackageReference2"/> with action and return code information.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference3"/></para>
    /// </remarks>
    [ComImport]
    [Guid("EBC3AE68-AD15-44E8-8377-39DBF0316F6C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupFailedPackageReference2.Interface
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

        /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetLogFilePath(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetLogFilePath(BSTR* pbstrLogFilePath);

        /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetDescription(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetDescription(BSTR* pbstrDescription);

        /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetSignature(BSTR*)"/>
        [PreserveSig]
        new HRESULT GetSignature(BSTR* pbstrSignature);

        /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetDetails(SAFEARRAY*)"/>
        [PreserveSig]
        new HRESULT GetDetails(SAFEARRAY* ppsaDetails);

        /// <inheritdoc cref="ISetupFailedPackageReference2.Interface.GetAffectedPackages(SAFEARRAY*)"/>
        [PreserveSig]
        new HRESULT GetAffectedPackages(SAFEARRAY* ppsaAffectedPackages);

        /// <summary>
        ///  Gets the action attempted when the package failed.
        /// </summary>
        /// <param name="pbstrAction">The action, eg: Install, Download, etc.</param>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference3.getaction"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetAction(BSTR* pbstrAction);

        /// <summary>
        ///  Gets the return code of the failure.
        /// </summary>
        /// <param name="pbstrReturnCode">The return code.</param>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference3.getreturncode"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetReturnCode(BSTR* pbstrReturnCode);
    }
}
