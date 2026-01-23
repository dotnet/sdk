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
internal unsafe struct ISetupInstanceCatalog : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x9AD8E40F, 0x39A2, 0x40F1, 0xBF, 0x64, 0x0A, 0x6C, 0x50, 0xDD, 0x9E, 0xEB);

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
                0x0F, 0xE4, 0xD8, 0x9A,
                0xA2, 0x39,
                0xF1, 0x40,
                0xBF, 0x64, 0x0A, 0x6C, 0x50, 0xDD, 0x9E, 0xEB
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupInstanceCatalog* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstanceCatalog*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupInstanceCatalog* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstanceCatalog*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupInstanceCatalog* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstanceCatalog*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetCatalogInfo(ISetupPropertyStore**)"/>
    public HRESULT GetCatalogInfo(ISetupPropertyStore** ppCatalogInfo)
    {
        fixed (ISetupInstanceCatalog* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstanceCatalog*, ISetupPropertyStore**, HRESULT>)_lpVtbl[3])(pThis, ppCatalogInfo);
    }

    /// <inheritdoc cref="Interface.IsPrerelease(VARIANT_BOOL*)"/>
    public HRESULT IsPrerelease(VARIANT_BOOL* pfIsPrerelease)
    {
        fixed (ISetupInstanceCatalog* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupInstanceCatalog*, VARIANT_BOOL*, HRESULT>)_lpVtbl[4])(pThis, pfIsPrerelease);
    }

    /// <summary>
    ///  Provides catalog information for a Visual Studio instance.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstancecatalog"/></para>
    /// </remarks>
    [ComImport]
    [Guid("9AD8E40F-39A2-40F1-BF64-0A6C50DD9EEB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets catalog information properties.
        /// </summary>
        /// <param name="ppCatalogInfo">A pointer to an instance of <see cref="ISetupPropertyStore"/>.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstancecatalog.getcataloginfo">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetCatalogInfo(ISetupPropertyStore** ppCatalogInfo);

        /// <summary>
        ///  Gets a value indicating whether the catalog is a prerelease.
        /// </summary>
        /// <param name="pfIsPrerelease">Whether the catalog for the instance is a prerelease version.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetupinstancecatalog.isprerelease">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT IsPrerelease(VARIANT_BOOL* pfIsPrerelease);
    }
}
