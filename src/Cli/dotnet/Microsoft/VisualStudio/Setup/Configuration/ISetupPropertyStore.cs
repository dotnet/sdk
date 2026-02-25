// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;

// For some reason the XML comment refs for IUnknown can't be resolved on .NET Framework without
// explicitly using the fully qualified name, which falls afoul of the simplify warning.
using ComIUnknown = Windows.Win32.System.Com.IUnknown;

namespace Microsoft.VisualStudio.Setup.Configuration;

/// <inheritdoc cref="Interface"/>
internal unsafe struct ISetupPropertyStore : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xC601C175, 0xA3BE, 0x44BC, 0x91, 0xF6, 0x45, 0x68, 0xD2, 0x30, 0xFC, 0x83);

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
                0x75, 0xC1, 0x01, 0xC6,
                0xBE, 0xA3,
                0xBC, 0x44,
                0x91, 0xF6, 0x45, 0x68, 0xD2, 0x30, 0xFC, 0x83
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPropertyStore*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPropertyStore*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPropertyStore*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetNames"/>
    public HRESULT GetNames(SAFEARRAY** ppsaNames)
    {
        fixed (ISetupPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPropertyStore*, SAFEARRAY**, HRESULT>)_lpVtbl[3])(pThis, ppsaNames);
    }

    /// <inheritdoc cref="Interface.GetValue"/>
    public HRESULT GetValue(PWSTR pwszName, VARIANT* pvtValue)
    {
        fixed (ISetupPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPropertyStore*, PWSTR, VARIANT*, HRESULT>)_lpVtbl[4])(pThis, pwszName, pvtValue);
    }

    /// <summary>
    ///  Provides named properties.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   You can get this from an <see cref="ISetupInstance"/>, <see cref="ISetupPackageReference"/>, or derivative.
    ///  </para>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppropertystore">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("C601C175-A3BE-44BC-91F6-4568D230FC83")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface Interface
    {
        /// <summary>
        ///  Gets an array of property names in this property store.
        /// </summary>
        /// <param name="ppsaNames">Pointer to an array of property names as BSTRs.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppropertystore.getnames">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetNames(SAFEARRAY** ppsaNames);

        /// <summary>
        ///  Gets the value of a named property in this property store.
        /// </summary>
        /// <param name="pwszName">The name of the property to get.</param>
        /// <param name="pvtValue">Pointer to receive the value of the property.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppropertystore.getvalue">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetValue(PWSTR pwszName, VARIANT* pvtValue);
    }
}
