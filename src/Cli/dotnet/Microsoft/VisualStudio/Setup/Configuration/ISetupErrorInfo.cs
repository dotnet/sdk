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
internal unsafe struct ISetupErrorInfo : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x2A2F3292, 0x958E, 0x4905, 0xB3, 0x6E, 0x01, 0x3B, 0xE8, 0x4E, 0x27, 0xAB);

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
                0x92, 0x32, 0x2F, 0x2A,
                0x8E, 0x95,
                0x05, 0x49,
                0xB3, 0x6E, 0x01, 0x3B, 0xE8, 0x4E, 0x27, 0xAB
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupErrorInfo* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorInfo*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupErrorInfo* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorInfo*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupErrorInfo* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorInfo*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetErrorHResult"/>
    public HRESULT GetErrorHResult(HRESULT* phrError)
    {
        fixed (ISetupErrorInfo* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorInfo*, HRESULT*, HRESULT>)_lpVtbl[3])(pThis, phrError);
    }

    /// <inheritdoc cref="Interface.GetErrorClassName"/>
    public HRESULT GetErrorClassName(BSTR* pbstrErrorClassName)
    {
        fixed (ISetupErrorInfo* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorInfo*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrErrorClassName);
    }

    /// <inheritdoc cref="Interface.GetErrorMessage"/>
    public HRESULT GetErrorMessage(BSTR* pbstrErrorMessage)
    {
        fixed (ISetupErrorInfo* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorInfo*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrErrorMessage);
    }

    /// <summary>
    ///  Information about errors that occurred during install of an instance.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   May also implement <see cref="ISetupPropertyStore"/>.
    ///  </para>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorinfo"/></para>
    /// </remarks>
    [ComImport]
    [Guid("2A2F3292-958E-4905-B36E-013BE84E27AB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets the HRESULT of the error.
        /// </summary>
        /// <param name="phrError">Pointer to receive the error HRESULT.</param>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorinfo.geterrorhresult"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetErrorHResult(HRESULT* phrError);

        /// <summary>
        ///  Gets the class name of the error (exception).
        /// </summary>
        /// <param name="pbstrErrorClassName">Pointer to receive the error class name.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorinfo.geterrorclassname"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetErrorClassName(BSTR* pbstrErrorClassName);

        /// <summary>
        ///  Gets the error message.
        /// </summary>
        /// <param name="pbstrErrorMessage">Pointer to receive the error message.</param>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorinfo.geterrormessage"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetErrorMessage(BSTR* pbstrErrorMessage);
    }
}
