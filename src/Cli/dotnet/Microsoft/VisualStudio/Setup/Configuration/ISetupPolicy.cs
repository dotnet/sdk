// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Variant;

// For some reason the XML comment refs for IUnknown can't be resolved on .NET Framework without
// explicitly using the fully qualified name, which falls afoul of the simplify warning.
using ComIUnknown = Windows.Win32.System.Com.IUnknown;

namespace Microsoft.VisualStudio.Setup.Configuration;

/// <inheritdoc cref="Interface"/>
internal unsafe struct ISetupPolicy : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xE1DA4CBD, 0x64C4, 0x4C44, 0x82, 0x1D, 0x98, 0xFA, 0xB6, 0x4C, 0x4D, 0xA7);

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
                0xBD, 0x4C, 0xDA, 0xE1,
                0xC4, 0x64,
                0x44, 0x4C,
                0x82, 0x1D, 0x98, 0xFA, 0xB6, 0x4C, 0x4D, 0xA7
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupPolicy* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPolicy*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupPolicy* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPolicy*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupPolicy* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPolicy*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetSharedInstallationPath(BSTR*)"/>
    public HRESULT GetSharedInstallationPath(BSTR* pbstrSharedInstallationPath)
    {
        fixed (ISetupPolicy* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPolicy*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrSharedInstallationPath);
    }

    /// <inheritdoc cref="Interface.GetValue(PWSTR, VARIANT*)"/>
    public HRESULT GetValue(PWSTR pwszName, VARIANT* pvtValue)
    {
        fixed (ISetupPolicy* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPolicy*, PWSTR, VARIANT*, HRESULT>)_lpVtbl[4])(pThis, pwszName, pvtValue);
    }

    /// <summary>
    ///  Provides access to Visual Studio setup policies.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppolicy"/></para>
    /// </remarks>
    [ComImport]
    [Guid("E1DA4CBD-64C4-4C44-821D-98FAB64C4DA7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets the value of the SharedInstallationPath policy.
        /// </summary>
        /// <param name="pbstrSharedInstallationPath">The value of the SharedInstallationPath policy.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppolicy.getsharedinstallationpath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetSharedInstallationPath(BSTR* pbstrSharedInstallationPath);

        /// <summary>
        ///  Gets the value of a named policy.
        /// </summary>
        /// <param name="pwszName">The name of the policy to get.</param>
        /// <param name="pvtValue">The value of the named policy.</param>
        /// <returns>
        ///  Standard <see cref="HRESULT"/> indicating success or failure, including <see cref="HRESULT.E_NOTSUPPORTED"/>
        ///  if the policy is not supported by this implementation.
        /// </returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppolicy.getvalue">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetValue(PWSTR pwszName, VARIANT* pvtValue);
    }
}
