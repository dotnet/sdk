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
internal unsafe struct ISetupLocalizedPropertyStore : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0x5BB53126, 0xE0D5, 0x43DF, 0x80, 0xF1, 0x6B, 0x16, 0x1E, 0x5C, 0x6F, 0x6C);

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
                0x26, 0x31, 0xB5, 0x5B,
                0xD5, 0xE0,
                0xDF, 0x43,
                0x80, 0xF1, 0x6B, 0x16, 0x1E, 0x5C, 0x6F, 0x6C
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupLocalizedPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedPropertyStore*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupLocalizedPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedPropertyStore*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupLocalizedPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedPropertyStore*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetNames(uint, SAFEARRAY**)"/>
    public HRESULT GetNames(uint lcid, SAFEARRAY** ppsaNames)
    {
        fixed (ISetupLocalizedPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedPropertyStore*, uint, SAFEARRAY**, HRESULT>)_lpVtbl[3])(pThis, lcid, ppsaNames);
    }

    /// <inheritdoc cref="Interface.GetValue(PWSTR, uint, VARIANT*)"/>
    public HRESULT GetValue(char* pwszName, uint lcid, VARIANT* pvtValue)
    {
        fixed (ISetupLocalizedPropertyStore* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupLocalizedPropertyStore*, PWSTR, uint, VARIANT*, HRESULT>)_lpVtbl[4])(pThis, pwszName, lcid, pvtValue);
    }

    /// <summary>
    ///  Provides access to localized properties with locale support.
    /// </summary>
    /// <remarks>
    ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuplocalizedpropertystore"/></para>
    /// </remarks>
    [ComImport]
    [Guid("5BB53126-E0D5-43DF-80F1-6B161E5C6F6C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets an array of property names in this property store.
        /// </summary>
        /// <param name="lcid">The LCID for the property names.</param>
        /// <param name="ppsaNames">Pointer to an array of property names as <see cref="BSTR"/>s.</param>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuplocalizedpropertystore.getnames">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetNames(uint lcid, SAFEARRAY** ppsaNames);

        /// <summary>
        ///  Gets the value of a named property in this property store.
        /// </summary>
        /// <param name="pwszName">The name of the property to get.</param>
        /// <param name="lcid">The LCID for the property.</param>
        /// <param name="pvtValue">The value of the property.</param>
        /// <returns>
        ///  Standard <see cref="HRESULT"/> indicating success or failure, including <see cref="HRESULT.E_NOTFOUND"/>
        ///  if the property is not defined or <see cref="HRESULT.E_NOTSUPPORTED"/> if the property type is not supported.
        /// </returns>
        /// <remarks>
        ///  <para><see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuplocalizedpropertystore.getvalue"/></para>
        /// </remarks>
        [PreserveSig]
        HRESULT GetValue(PWSTR pwszName, uint lcid, VARIANT* pvtValue);
    }
}
