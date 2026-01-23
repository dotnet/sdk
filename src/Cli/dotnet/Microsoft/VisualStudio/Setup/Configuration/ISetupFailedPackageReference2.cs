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
internal unsafe struct ISetupFailedPackageReference2 : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x0FAD873E, 0xE874, 0x42E3, 0xB2, 0x68, 0x4F, 0xE2, 0xF0, 0x96, 0xB9, 0xCA);

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
                0x3E, 0x87, 0xAD, 0x0F,
                0x74, 0xE8,
                0xE3, 0x42,
                0xB2, 0x68, 0x4F, 0xE2, 0xF0, 0x96, 0xB9, 0xCA
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, uint>)_lpVtbl[2])(pThis);
    }

    // ISetupPackageReference methods
    /// <inheritdoc cref="ISetupPackageReference.Interface.GetId(BSTR*)"/>
    public HRESULT GetId(BSTR* pbstrId)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetVersion(BSTR*)"/>
    public HRESULT GetVersion(BSTR* pbstrVersion)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrVersion);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetChip(BSTR*)"/>
    public HRESULT GetChip(BSTR* pbstrChip)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrChip);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetLanguage(BSTR*)"/>
    public HRESULT GetLanguage(BSTR* pbstrLanguage)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLanguage);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetBranch(BSTR*)"/>
    public HRESULT GetBranch(BSTR* pbstrBranch)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrBranch);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetType(BSTR*)"/>
    public HRESULT GetType(BSTR* pbstrType)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[8])(pThis, pbstrType);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetUniqueId(BSTR*)"/>
    public HRESULT GetUniqueId(BSTR* pbstrUniqueId)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[9])(pThis, pbstrUniqueId);
    }

    /// <inheritdoc cref="ISetupPackageReference.Interface.GetIsExtension(VARIANT_BOOL*)"/>
    public HRESULT GetIsExtension(short* pfIsExtension)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, short*, HRESULT>)_lpVtbl[10])(pThis, pfIsExtension);
    }

    // ISetupFailedPackageReference2 specific methods
    /// <inheritdoc cref="Interface.GetLogFilePath(BSTR*)"/>
    public HRESULT GetLogFilePath(BSTR* pbstrLogFilePath)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[11])(pThis, pbstrLogFilePath);
    }

    /// <inheritdoc cref="Interface.GetDescription(BSTR*)"/>
    public HRESULT GetDescription(BSTR* pbstrDescription)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[12])(pThis, pbstrDescription);
    }

    /// <inheritdoc cref="Interface.GetSignature(BSTR*)"/>
    public HRESULT GetSignature(BSTR* pbstrSignature)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, BSTR*, HRESULT>)_lpVtbl[13])(pThis, pbstrSignature);
    }

    /// <inheritdoc cref="Interface.GetDetails(SAFEARRAY*)"/>
    public HRESULT GetDetails(SAFEARRAY* ppsaDetails)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, SAFEARRAY*, HRESULT>)_lpVtbl[14])(pThis, ppsaDetails);
    }

    /// <inheritdoc cref="Interface.GetAffectedPackages(SAFEARRAY*)"/>
    public HRESULT GetAffectedPackages(SAFEARRAY* ppsaAffectedPackages)
    {
        fixed (ISetupFailedPackageReference2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupFailedPackageReference2*, SAFEARRAY*, HRESULT>)_lpVtbl[15])(pThis, ppsaAffectedPackages);
    }

    /// <summary>
    ///  Extends <see cref="ISetupFailedPackageReference"/> with additional failure information.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference2"/></para>
    /// </remarks>
    [ComImport]
    [Guid("0FAD873E-E874-42E3-B268-4FE2F096B9CA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupFailedPackageReference.Interface
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
        ///  Gets the path to the optional package log.
        /// </summary>
        /// <param name="pbstrLogFilePath">The path to the optional package log.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference2.getlogfilepath"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetLogFilePath(BSTR* pbstrLogFilePath);

        /// <summary>
        ///  Gets the description of the package failure.
        /// </summary>
        /// <param name="pbstrDescription">The description of the package failure.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference2.getdescription"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetDescription(BSTR* pbstrDescription);

        /// <summary>
        ///  Gets the signature to use for feedback reporting.
        /// </summary>
        /// <param name="pbstrSignature">The signature to use for feedback reporting.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference2.getsignature"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetSignature(BSTR* pbstrSignature);

        /// <summary>
        ///  Gets the array of details for this package failure.
        /// </summary>
        /// <param name="ppsaDetails">Pointer to an array of details as <see cref="BSTR"/>s.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference2.getdetails"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetDetails(SAFEARRAY* ppsaDetails);

        /// <summary>
        ///  Gets an array of packages affected by this package failure.
        /// </summary>
        /// <param name="ppsaAffectedPackages">Pointer to an array of <see cref="ISetupPackageReference"/> for packages affected by this package failure. This may be NULL.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupfailedpackagereference2.getaffectedpackages"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetAffectedPackages(SAFEARRAY* ppsaAffectedPackages);
    }
}
