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
internal unsafe struct ISetupLocalizedProperties : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xF4BD7382, 0xFE27, 0x4AB4, 0xB9, 0x74, 0x99, 0x05, 0xB2, 0xA1, 0x48, 0xB0);

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
                0x82, 0x73, 0xBD, 0xF4,
                0x27, 0xFE,
                0xB4, 0x4A,
                0xB9, 0x74, 0x99, 0x05, 0xB2, 0xA1, 0x48, 0xB0
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupLocalizedProperties* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedProperties*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupLocalizedProperties* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedProperties*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupLocalizedProperties* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedProperties*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetLocalizedProperties(ISetupLocalizedPropertyStore**)"/>
    public HRESULT GetLocalizedProperties(ISetupLocalizedPropertyStore** ppLocalizedProperties)
    {
        fixed (ISetupLocalizedProperties* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedProperties*, ISetupLocalizedPropertyStore**, HRESULT>)_lpVtbl[3])(pThis, ppLocalizedProperties);
    }

    /// <inheritdoc cref="Interface.GetLocalizedChannelProperties(ISetupLocalizedPropertyStore**)"/>
    public HRESULT GetLocalizedChannelProperties(ISetupLocalizedPropertyStore** ppLocalizedChannelProperties)
    {
        fixed (ISetupLocalizedProperties* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedProperties*, ISetupLocalizedPropertyStore**, HRESULT>)_lpVtbl[4])(pThis, ppLocalizedChannelProperties);
    }

    /// <summary>
    ///  Provides access to localized properties for a Visual Studio instance.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuplocalizedproperties"/></para>
    /// </remarks>
    [ComImport]
    [Guid("F4BD7382-FE27-4AB4-B974-9905B2A148B0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets localized product-specific properties.
        /// </summary>
        /// <param name="ppLocalizedProperties">
        ///  A pointer to an instance of <see cref="ISetupLocalizedPropertyStore"/>. This may be <see langword="null"/>
        ///  if no properties are defined.
        /// </param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuplocalizedproperties.getlocalizedproperties">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetLocalizedProperties(ISetupLocalizedPropertyStore** ppLocalizedProperties);

        /// <summary>
        ///  Gets localized channel-specific properties.
        /// </summary>
        /// <param name="ppLocalizedChannelProperties">
        ///  A pointer to an instance of <see cref="ISetupLocalizedPropertyStore"/>. This may be <see langword="null"/>
        ///  if no channel properties are defined.
        /// </param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuplocalizedproperties.getlocalizedchannelproperties">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetLocalizedChannelProperties(ISetupLocalizedPropertyStore** ppLocalizedChannelProperties);
    }
}
