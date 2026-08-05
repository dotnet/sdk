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
internal unsafe struct IEnumSetupInstances : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x6380BCFF, 0x41D3, 0x4B2E, 0x8B, 0x2E, 0xBF, 0x8A, 0x68, 0x10, 0xC8, 0x48);

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
                0xFF, 0xBC, 0x80, 0x63,
                0xD3, 0x41,
                0x2E, 0x4B,
                0x8B, 0x2E, 0xBF, 0x8A, 0x68, 0x10, 0xC8, 0x48
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.Next(uint, ISetupInstance**, uint*)"/>
    public HRESULT Next(uint celt, ISetupInstance** rgelt, uint* pceltFetched)
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint, ISetupInstance**, uint*, HRESULT>)_lpVtbl[3])(pThis, celt, rgelt, pceltFetched);
    }

    /// <inheritdoc cref="Interface.Skip(uint)"/>
    public HRESULT Skip(uint celt)
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, uint, HRESULT>)_lpVtbl[4])(pThis, celt);
    }

    /// <inheritdoc cref="Interface.Reset"/>
    public HRESULT Reset()
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, HRESULT>)_lpVtbl[5])(pThis);
    }

    /// <inheritdoc cref="Interface.Clone(IEnumSetupInstances**)"/>
    public HRESULT Clone(IEnumSetupInstances** ppEnumInstances)
    {
        fixed (IEnumSetupInstances* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<IEnumSetupInstances*, IEnumSetupInstances**, HRESULT>)_lpVtbl[6])(pThis, ppEnumInstances);
    }

    /// <summary>
    ///  Enumerates Visual Studio setup instances.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Matches the COM enumeration pattern used throughout Windows APIs.
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("6380BCFF-41D3-4B2E-8B2E-BF8A6810C848")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Retrieves a specified number of setup instances in the enumeration sequence.
        /// </summary>
        [PreserveSig]
        HRESULT Next(uint celt, ISetupInstance** rgelt, uint* pceltFetched);

        /// <summary>
        ///  Skips a specified number of setup instances in the enumeration sequence.
        /// </summary>
        [PreserveSig]
        HRESULT Skip(uint celt);

        /// <summary>
        ///  Resets the enumeration sequence to the beginning.
        /// </summary>
        [PreserveSig]
        HRESULT Reset();

        /// <summary>
        ///  Creates a new enumerator that contains the same enumeration state as the current one.
        /// </summary>
        [PreserveSig]
        HRESULT Clone(IEnumSetupInstances** ppEnumInstances);
    }
}
