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
internal unsafe struct ISetupHelper : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x42b21b78, 0x6192, 0x463e, 0x87, 0xbf, 0xd5, 0x77, 0x83, 0x8f, 0x1d, 0x5c);

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
                0x78, 0x1b, 0xb2, 0x42,
                0x92, 0x61,
                0x3e, 0x46,
                0x87, 0xbf, 0xd5, 0x77, 0x83, 0x8f, 0x1d, 0x5c
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupHelper* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupHelper*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupHelper* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupHelper*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupHelper* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupHelper*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.ParseVersion(PWSTR, ulong*)"/>
    public HRESULT ParseVersion(PWSTR pwszVersion, ulong* pullVersion)
    {
        fixed (ISetupHelper* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupHelper*, PWSTR, ulong*, HRESULT>)_lpVtbl[3])(pThis, pwszVersion, pullVersion);
    }

    /// <inheritdoc cref="Interface.ParseVersionRange(PWSTR, ulong*, ulong*)"/>
    public HRESULT ParseVersionRange(PWSTR pwszVersionRange, ulong* pullMinVersion, ulong* pullMaxVersion)
    {
        fixed (ISetupHelper* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupHelper*, PWSTR, ulong*, ulong*, HRESULT>)_lpVtbl[4])(pThis, pwszVersionRange, pullMinVersion, pullMaxVersion);
    }

    /// <summary>
    ///  Provides helper methods for parsing version information.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuphelper">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("42b21b78-6192-463e-87bf-d577838f1d5c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Parses a dotted quad version string into a 64-bit unsigned integer.
        /// </summary>
        /// <param name="pwszVersion">The dotted quad version string to parse, e.g. 1.2.3.4.</param>
        /// <param name="pullVersion">A 64-bit unsigned integer representing the version. You can compare this to other versions.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuphelper.parseversion">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>
        ///  Standard <see cref="HRESULT"/> indicating success or failure, including <see cref="HRESULT.E_INVALIDARG"/>
        ///  if the version is not valid.
        /// </returns>
        [PreserveSig]
        HRESULT ParseVersion(PWSTR pwszVersion, ulong* pullVersion);

        /// <summary>
        ///  Parses a dotted quad version string into a 64-bit unsigned integer.
        /// </summary>
        /// <param name="pwszVersionRange">The string containing 1 or 2 dotted quad version strings to parse, e.g. [1.0,) that means 1.0.0.0 or newer.</param>
        /// <param name="pullMinVersion">A 64-bit unsigned integer representing the minimum version, which may be 0. You can compare this to other versions.</param>
        /// <param name="pullMaxVersion">A 64-bit unsigned integer representing the maximum version, which may be MAXULONGLONG. You can compare this to other versions.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuphelper.parseversionrange">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard HRESULT indicating success or failure, including E_INVALIDARG if the version range is not valid.</returns>
        [PreserveSig]
        HRESULT ParseVersionRange(PWSTR pwszVersionRange, ulong* pullMinVersion, ulong* pullMaxVersion);
    }
}
