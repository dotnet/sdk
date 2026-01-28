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
internal unsafe struct ISetupInstance : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xB41463C3, 0x8866, 0x43B5, 0xBC, 0x33, 0x2B, 0x06, 0x76, 0xF7, 0xF4, 0x2E);

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
                0xC3, 0x63, 0x14, 0xB4,
                0x66, 0x88,
                0xB5, 0x43,
                0xBC, 0x33, 0x2B, 0x06, 0x76, 0xF7, 0xF4, 0x2E
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetInstanceId(BSTR*)"/>
    public HRESULT GetInstanceId(BSTR* pbstrInstanceId)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrInstanceId);
    }

    /// <inheritdoc cref="Interface.GetInstallDate(FILETIME*)"/>
    public HRESULT GetInstallDate(FILETIME* pInstallDate)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, FILETIME*, HRESULT>)_lpVtbl[4])(pThis, pInstallDate);
    }

    /// <inheritdoc cref="Interface.GetInstallationName(BSTR*)"/>
    public HRESULT GetInstallationName(BSTR* pbstrInstallationName)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrInstallationName);
    }

    /// <inheritdoc cref="Interface.GetInstallationPath(BSTR*)"/>
    public HRESULT GetInstallationPath(BSTR* pbstrInstallationPath)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrInstallationPath);
    }

    /// <inheritdoc cref="Interface.GetInstallationVersion(BSTR*)"/>
    public HRESULT GetInstallationVersion(BSTR* pbstrInstallationVersion)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrInstallationVersion);
    }

    /// <inheritdoc cref="Interface.GetDisplayName(uint, BSTR*)"/>
    public HRESULT GetDisplayName(uint lcid, BSTR* pbstrDisplayName)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint, BSTR*, HRESULT>)_lpVtbl[8])(pThis, lcid, pbstrDisplayName);
    }

    /// <inheritdoc cref="Interface.GetDescription(uint, BSTR*)"/>
    public HRESULT GetDescription(uint lcid, BSTR* pbstrDescription)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, uint, BSTR*, HRESULT>)_lpVtbl[9])(pThis, lcid, pbstrDescription);
    }

    /// <inheritdoc cref="Interface.ResolvePath(PWSTR, BSTR*)"/>
    public HRESULT ResolvePath(PWSTR pwszRelativePath, BSTR* pbstrAbsolutePath)
    {
        fixed (ISetupInstance* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstance*, PWSTR, BSTR*, HRESULT>)_lpVtbl[10])(pThis, pwszRelativePath, pbstrAbsolutePath);
    }

    /// <summary>
    ///  Represents a single Visual Studio installation instance.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("B41463C3-8866-43B5-BC33-2B0676F7F42E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets the unique identifier for the instance.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getinstanceid">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetInstanceId(BSTR* pbstrInstanceId);

        /// <summary>
        ///  Gets the date the instance was installed.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getinstalldate">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetInstallDate(FILETIME* pInstallDate);

        /// <summary>
        ///  Gets the installation name (for example, "Visual Studio Professional").
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getinstallationname">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetInstallationName(BSTR* pbstrInstallationName);

        /// <summary>
        ///  Gets the installation root path.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getinstallationpath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetInstallationPath(BSTR* pbstrInstallationPath);

        /// <summary>
        ///  Gets the installation version.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getinstallationversion">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetInstallationVersion(BSTR* pbstrInstallationVersion);

        /// <summary>
        ///  Gets the display name for the installation.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getdisplayname">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetDisplayName(uint lcid, BSTR* pbstrDisplayName);

        /// <summary>
        ///  Gets the description of the installation.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.getdescription">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetDescription(uint lcid, BSTR* pbstrDescription);

        /// <summary>
        ///  Resolves a path relative to the installation root.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstance.resolvepath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT ResolvePath(PWSTR pwszRelativePath, BSTR* pbstrAbsolutePath);
    }
}
