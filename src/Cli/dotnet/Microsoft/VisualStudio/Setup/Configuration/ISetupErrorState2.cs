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
internal unsafe struct ISetupErrorState2 : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x9871385B, 0xCA69, 0x48F2, 0xBC, 0x1F, 0x7A, 0x37, 0xCB, 0xF0, 0xB1, 0xEF);

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
                0x5B, 0x38, 0x71, 0x98,
                0x69, 0xCA,
                0xF2, 0x48,
                0xBC, 0x1F, 0x7A, 0x37, 0xCB, 0xF0, 0xB1, 0xEF
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="ISetupErrorState.Interface.GetFailedPackages"/>
    public HRESULT GetFailedPackages(SAFEARRAY** ppsaFailedPackages)
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, SAFEARRAY**, HRESULT>)_lpVtbl[3])(pThis, ppsaFailedPackages);
    }

    /// <inheritdoc cref="ISetupErrorState.Interface.GetSkippedPackages"/>
    public HRESULT GetSkippedPackages(SAFEARRAY** ppsaSkippedPackages)
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, SAFEARRAY**, HRESULT>)_lpVtbl[4])(pThis, ppsaSkippedPackages);
    }

    /// <inheritdoc cref="Interface.GetErrorLogFilePath"/>
    public HRESULT GetErrorLogFilePath(BSTR* pbstrErrorLogFilePath)
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrErrorLogFilePath);
    }

    /// <inheritdoc cref="Interface.GetLogFilePath"/>
    public HRESULT GetLogFilePath(BSTR* pbstrLogFilePath)
    {
        fixed (ISetupErrorState2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState2*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLogFilePath);
    }

    /// <summary>
    ///  Information about the error state of an instance.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorstate2">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("9871385B-CA69-48F2-BC1F-7A37CBF0B1EF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupErrorState.Interface
    {
        /// <inheritdoc cref="ISetupErrorState.Interface.GetFailedPackages"/>
        [PreserveSig]
        new HRESULT GetFailedPackages(SAFEARRAY** ppsaFailedPackages);

        /// <inheritdoc cref="ISetupErrorState.Interface.GetSkippedPackages"/>
        [PreserveSig]
        new HRESULT GetSkippedPackages(SAFEARRAY** ppsaSkippedPackages);

        /// <summary>
        ///  Gets the path to the error log.
        /// </summary>
        /// <param name="pbstrErrorLogFilePath">Pointer to receive the error log file path.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorstate2.geterrorlogfilepath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetErrorLogFilePath(BSTR* pbstrErrorLogFilePath);

        /// <summary>
        ///  Gets the path to the main setup log.
        /// </summary>
        /// <param name="pbstrLogFilePath">Pointer to receive the main log file path.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorstate2.getlogfilepath">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetLogFilePath(BSTR* pbstrLogFilePath);
    }
}
