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
internal unsafe struct ISetupConfiguration2 : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x26AAB78C, 0x4A60, 0x49D6, 0xAF, 0x3B, 0x3C, 0x35, 0xBC, 0x93, 0x36, 0x5D);

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
                0x8C, 0xB7, 0xAA, 0x26,
                0x60, 0x4A,
                0xD6, 0x49,
                0xAF, 0x3B, 0x3C, 0x35, 0xBC, 0x93, 0x36, 0x5D
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="ISetupConfiguration.Interface.EnumInstances"/>
    public HRESULT EnumInstances(IEnumSetupInstances** ppEnumInstances)
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, IEnumSetupInstances**, HRESULT>)_lpVtbl[3])(pThis, ppEnumInstances);
    }

    /// <inheritdoc cref="ISetupConfiguration.Interface.GetInstanceForCurrentProcess"/>
    public HRESULT GetInstanceForCurrentProcess(ISetupInstance** ppInstance)
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, ISetupInstance**, HRESULT>)_lpVtbl[4])(pThis, ppInstance);
    }

    /// <inheritdoc cref="ISetupConfiguration.Interface.GetInstanceForPath"/>
    public HRESULT GetInstanceForPath(PCWSTR path, ISetupInstance** ppInstance)
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, PCWSTR, ISetupInstance**, HRESULT>)_lpVtbl[5])(pThis, path, ppInstance);
    }

    /// <inheritdoc cref="Interface.EnumAllInstances(IEnumSetupInstances**)"/>
    public HRESULT EnumAllInstances(IEnumSetupInstances** ppEnumInstances)
    {
        fixed (ISetupConfiguration2* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupConfiguration2*, IEnumSetupInstances**, HRESULT>)_lpVtbl[6])(pThis, ppEnumInstances);
    }

    /// <summary>
    ///  Extends <see cref="ISetupConfiguration"/> to enumerate all Visual Studio instances.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Instances returned by this interface may include those that are incomplete or not normally discoverable.
    ///  </para>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupconfiguration2">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("26AAB78C-4A60-49D6-AF3B-3C35BC93365D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface : ISetupConfiguration.Interface
    {
        /// <inheritdoc cref="ISetupConfiguration.Interface.EnumInstances"/>
        [PreserveSig]
        new HRESULT EnumInstances(IEnumSetupInstances** ppEnumInstances);

        /// <inheritdoc cref="ISetupConfiguration.Interface.GetInstanceForCurrentProcess"/>
        [PreserveSig]
        new HRESULT GetInstanceForCurrentProcess(ISetupInstance** ppInstance);

        /// <inheritdoc cref="ISetupConfiguration.Interface.GetInstanceForPath"/>
        [PreserveSig]
        new HRESULT GetInstanceForPath(PCWSTR path, ISetupInstance** ppInstance);

        /// <summary>
        ///  Enumerates all instances, including those that may not be discoverable using <see cref="ISetupConfiguration.Interface.EnumInstances"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupconfiguration2.enumallinstances">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT EnumAllInstances(IEnumSetupInstances** ppEnumInstances);
    }
}
