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
internal unsafe struct ISetupInstance2 : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x89143C9A, 0x05AF, 0x49B0, 0xB7, 0x17, 0x72, 0xE2, 0x18, 0xA2, 0x18, 0x5C);

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
                0x9A, 0x3C, 0x14, 0x89,
                0xAF, 0x05,
                0xB0, 0x49,
                0xB7, 0x17, 0x72, 0xE2, 0x18, 0xA2, 0x18, 0x5C
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetInstanceId(BSTR*)"/>
    public HRESULT GetInstanceId(BSTR* pbstrInstanceId)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrInstanceId);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetInstallDate(FILETIME*)"/>
    public HRESULT GetInstallDate(FILETIME* pInstallDate)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, FILETIME*, HRESULT>)_lpVtbl[4])(pThis, pInstallDate);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetInstallationName(BSTR*)"/>
    public HRESULT GetInstallationName(BSTR* pbstrInstallationName)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrInstallationName);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetInstallationPath(BSTR*)"/>
    public HRESULT GetInstallationPath(BSTR* pbstrInstallationPath)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrInstallationPath);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetInstallationVersion(BSTR*)"/>
    public HRESULT GetInstallationVersion(BSTR* pbstrInstallationVersion)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrInstallationVersion);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetDisplayName(uint, BSTR*)"/>
    public HRESULT GetDisplayName(uint lcid, BSTR* pbstrDisplayName)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint, BSTR*, HRESULT>)_lpVtbl[8])(pThis, lcid, pbstrDisplayName);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.GetDescription(uint, BSTR*)"/>
    public HRESULT GetDescription(uint lcid, BSTR* pbstrDescription)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint, BSTR*, HRESULT>)_lpVtbl[9])(pThis, lcid, pbstrDescription);
    }

    /// <inheritdoc cref="ISetupInstance.Interface.ResolvePath(PWSTR, BSTR*)"/>
    public HRESULT ResolvePath(PWSTR pwszRelativePath, BSTR* pbstrAbsolutePath)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, PWSTR, BSTR*, HRESULT>)_lpVtbl[10])(pThis, pwszRelativePath, pbstrAbsolutePath);
    }

    /// <inheritdoc cref="Interface.GetState"/>
    public HRESULT GetState(uint* pState)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, uint*, HRESULT>)_lpVtbl[11])(pThis, pState);
    }

    /// <inheritdoc cref="Interface.GetPackages"/>
    public HRESULT GetPackages(SAFEARRAY** ppsaPackages)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, SAFEARRAY**, HRESULT>)_lpVtbl[12])(pThis, ppsaPackages);
    }

    /// <inheritdoc cref="Interface.GetProduct"/>
    public HRESULT GetProduct(ISetupPackageReference** ppPackage)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, ISetupPackageReference**, HRESULT>)_lpVtbl[13])(pThis, ppPackage);
    }

    /// <inheritdoc cref="Interface.GetProductPath"/>
    public HRESULT GetProductPath(BSTR* pbstrProductPath)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, BSTR*, HRESULT>)_lpVtbl[14])(pThis, pbstrProductPath);
    }

    /// <inheritdoc cref="Interface.GetErrors"/>
    public HRESULT GetErrors(ISetupErrorState** ppErrorState)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, ISetupErrorState**, HRESULT>)_lpVtbl[15])(pThis, ppErrorState);
    }

    /// <inheritdoc cref="Interface.IsLaunchable"/>
    public HRESULT IsLaunchable(VARIANT_BOOL* pfLaunchable)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, VARIANT_BOOL*, HRESULT>)_lpVtbl[16])(pThis, pfLaunchable);
    }

    /// <inheritdoc cref="Interface.IsComplete"/>
    public HRESULT IsComplete(VARIANT_BOOL* pfComplete)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, VARIANT_BOOL*, HRESULT>)_lpVtbl[17])(pThis, pfComplete);
    }

    /// <inheritdoc cref="Interface.GetProperties"/>
    public HRESULT GetProperties(ISetupPropertyStore** ppPropertyStore)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, ISetupPropertyStore**, HRESULT>)_lpVtbl[18])(pThis, ppPropertyStore);
    }

    /// <inheritdoc cref="Interface.GetEnginePath"/>
    public HRESULT GetEnginePath(BSTR* pbstrEnginePath)
    {
        fixed (ISetupInstance2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance2*, BSTR*, HRESULT>)_lpVtbl[19])(pThis, pbstrEnginePath);
    }

    /// <summary>
    ///  Information about an instance of a product.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   You can enumerate all properties of basic types by casting to an <see cref="ISetupPropertyStore"/>.
    ///  </para>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("89143C9A-05AF-49B0-B717-72E218A2185C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupInstance.Interface
    {
        /// <inheritdoc cref="ISetupInstance.Interface.GetInstanceId"/>
        [PreserveSig]
        new HRESULT GetInstanceId(BSTR* pbstrInstanceId);

        /// <inheritdoc cref="ISetupInstance.Interface.GetInstallDate"/>
        [PreserveSig]
        new HRESULT GetInstallDate(FILETIME* pInstallDate);

        /// <inheritdoc cref="ISetupInstance.Interface.GetInstallationName"/>
        [PreserveSig]
        new HRESULT GetInstallationName(BSTR* pbstrInstallationName);

        /// <inheritdoc cref="ISetupInstance.Interface.GetInstallationPath"/>
        [PreserveSig]
        new HRESULT GetInstallationPath(BSTR* pbstrInstallationPath);

        /// <inheritdoc cref="ISetupInstance.Interface.GetInstallationVersion"/>
        [PreserveSig]
        new HRESULT GetInstallationVersion(BSTR* pbstrInstallationVersion);

        /// <inheritdoc cref="ISetupInstance.Interface.GetDisplayName"/>
        [PreserveSig]
        new HRESULT GetDisplayName(uint lcid, BSTR* pbstrDisplayName);

        /// <inheritdoc cref="ISetupInstance.Interface.GetDescription"/>
        [PreserveSig]
        new HRESULT GetDescription(uint lcid, BSTR* pbstrDescription);

        /// <inheritdoc cref="ISetupInstance.Interface.ResolvePath"/>
        [PreserveSig]
        new HRESULT ResolvePath(PWSTR pwszRelativePath, BSTR* pbstrAbsolutePath);

        /// <summary>
        ///  Gets the state of the instance.
        /// </summary>
        /// <param name="pState">Pointer to receive the state of the instance.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.getstate">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetState(InstanceState* pState);

        /// <summary>
        ///  Gets an array of package references registered to the instance.
        /// </summary>
        /// <param name="ppsaPackages">Pointer to receive an array of <see cref="ISetupPackageReference"/> pointers.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.getpackages">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>
        ///  Standard <see cref="HRESULT"/> indicating success or failure, including <see cref="HRESULT.E_FILENOTFOUND"/>
        ///  if the instance state does not exist and <see cref="HRESULT.E_NOTFOUND"/> if the packages property is not defined.
        /// </returns>
        [PreserveSig]
        HRESULT GetPackages(SAFEARRAY** ppsaPackages);

        /// <summary>
        ///  Gets a package reference to the product registered to the instance.
        /// </summary>
        /// <param name="ppPackage">Pointer to receive the product package reference.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.getproduct">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>
        ///  Standard <see cref="HRESULT"/> indicating success or failure, including <see cref="HRESULT.E_FILENOTFOUND"/>
        ///  if the instance state does not exist and <see cref="HRESULT.E_NOTFOUND"/> if the packages property is not defined.
        /// </returns>
        [PreserveSig]
        HRESULT GetProduct(ISetupPackageReference** ppPackage);

        /// <summary>
        ///  Gets the relative path to the product application, if available.
        /// </summary>
        /// <param name="pbstrProductPath">Pointer to receive the product path.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.getproductpath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetProductPath(BSTR* pbstrProductPath);

        /// <summary>
        ///  Gets the error state of the instance, if available.
        /// </summary>
        /// <param name="ppErrorState">Pointer to receive the error state interface.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.geterrors">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetErrors(ISetupErrorState** ppErrorState);

        /// <summary>
        ///  Gets a value indicating whether the instance can be launched.
        /// </summary>
        /// <param name="pfLaunchable">Pointer to receive whether the instance is launchable.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.islaunchable">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT IsLaunchable(VARIANT_BOOL* pfLaunchable);

        /// <summary>
        ///  Gets a value indicating whether the instance is complete.
        /// </summary>
        /// <param name="pfComplete">Pointer to receive whether the instance is complete.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.iscomplete">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT IsComplete(VARIANT_BOOL* pfComplete);

        /// <summary>
        ///  Gets product-specific properties.
        /// </summary>
        /// <param name="ppPropertyStore">Pointer to receive the property store interface.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.getproperties">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetProperties(ISetupPropertyStore** ppPropertyStore);

        /// <summary>
        ///  Gets the directory path to the setup engine that installed the instance.
        /// </summary>
        /// <param name="pbstrEnginePath">Pointer to receive the engine path.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance2.getenginepath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetEnginePath(BSTR* pbstrEnginePath);
    }
}
