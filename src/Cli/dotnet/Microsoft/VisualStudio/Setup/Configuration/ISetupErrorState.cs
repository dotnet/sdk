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
internal unsafe struct ISetupErrorState : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x46DCCD94, 0xA287, 0x476A, 0x85, 0x1E, 0xDF, 0xBC, 0x2F, 0xFD, 0xBC, 0x20);

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
                0x94, 0xCD, 0xDC, 0x46,
                0x87, 0xA2,
                0x6A, 0x47,
                0x85, 0x1E, 0xDF, 0xBC, 0x2F, 0xFD, 0xBC, 0x20
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupErrorState* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupErrorState* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupErrorState* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetFailedPackages"/>
    public HRESULT GetFailedPackages(SAFEARRAY** ppsaFailedPackages)
    {
        fixed (ISetupErrorState* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState*, SAFEARRAY**, HRESULT>)_lpVtbl[3])(pThis, ppsaFailedPackages);
    }

    /// <inheritdoc cref="Interface.GetSkippedPackages"/>
    public HRESULT GetSkippedPackages(SAFEARRAY** ppsaSkippedPackages)
    {
        fixed (ISetupErrorState* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupErrorState*, SAFEARRAY**, HRESULT>)_lpVtbl[4])(pThis, ppsaSkippedPackages);
    }

    /// <summary>
    ///  Information about the error state of an instance.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorstate">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("46DCCD94-A287-476A-851E-DFBC2FFDBC20")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets an array of failed package references.
        /// </summary>
        /// <param name="ppsaFailedPackages">
        ///  Pointer to an array of <see cref="ISetupFailedPackageReference"/>, if packages have failed.
        /// </param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorstate.getfailedpackages">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetFailedPackages(SAFEARRAY** ppsaFailedPackages);

        /// <summary>
        ///  Gets an array of skipped package references.
        /// </summary>
        /// <param name="ppsaSkippedPackages">Pointer to an array of <see cref="ISetupPackageReference"/>, if packages have been skipped.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuperrorstate.getskippedpackages">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetSkippedPackages(SAFEARRAY** ppsaSkippedPackages);
    }
}
