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
internal unsafe struct ISetupPackageReference : IComIID
{
    /// <inheritdoc cref="IComIID.Guid"/>
    public static Guid Guid { get; } = new(0xDA8D8A16, 0xB2B6, 0x4487, 0xA2, 0xF1, 0x59, 0x4C, 0xCC, 0xCD, 0x6B, 0xF5);

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
                0x16, 0x8A, 0x8D, 0xDA,
                0xB6, 0xB2,
                0x87, 0x44,
                0xA2, 0xF1, 0x59, 0x4C, 0xCC, 0xCD, 0x6B, 0xF5
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#endif

    private readonly void** _lpVtbl;

    /// <inheritdoc cref="ComIUnknown.QueryInterface(Guid*, void**)"/>
    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, Guid*, void**, HRESULT>)_lpVtbl[0])(pThis, riid, ppvObject);
    }

    /// <inheritdoc cref="ComIUnknown.AddRef"/>
    public uint AddRef()
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, uint>)_lpVtbl[1])(pThis);
    }

    /// <inheritdoc cref="ComIUnknown.Release"/>
    public uint Release()
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, uint>)_lpVtbl[2])(pThis);
    }

    /// <inheritdoc cref="Interface.GetId"/>
    public HRESULT GetId(BSTR* pbstrId)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[3])(pThis, pbstrId);
    }

    /// <inheritdoc cref="Interface.GetVersion"/>
    public HRESULT GetVersion(BSTR* pbstrVersion)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[4])(pThis, pbstrVersion);
    }

    /// <inheritdoc cref="Interface.GetChip"/>
    public HRESULT GetChip(BSTR* pbstrChip)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[5])(pThis, pbstrChip);
    }

    /// <inheritdoc cref="Interface.GetLanguage"/>
    public HRESULT GetLanguage(BSTR* pbstrLanguage)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[6])(pThis, pbstrLanguage);
    }

    /// <inheritdoc cref="Interface.GetBranch"/>
    public HRESULT GetBranch(BSTR* pbstrBranch)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[7])(pThis, pbstrBranch);
    }

    /// <inheritdoc cref="Interface.GetType"/>
    public HRESULT GetType(BSTR* pbstrType)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[8])(pThis, pbstrType);
    }

    /// <inheritdoc cref="Interface.GetUniqueId"/>
    public HRESULT GetUniqueId(BSTR* pbstrUniqueId)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, BSTR*, HRESULT>)_lpVtbl[9])(pThis, pbstrUniqueId);
    }

    /// <inheritdoc cref="Interface.GetIsExtension"/>
    public HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension)
    {
        fixed (ISetupPackageReference* pThis = &this)
            return ((delegate* unmanaged[Stdcall]<ISetupPackageReference*, VARIANT_BOOL*, HRESULT>)_lpVtbl[10])(pThis, pfIsExtension);
    }

    /// <summary>
    ///  A reference to a package.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   You can enumerate all properties of basic types by casting to an <see cref="ISetupPropertyStore"/>.
    ///  </para>
    ///  <para>
    ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference">
    ///    Official documentation.
    ///   </see>
    ///  </para>
    /// </remarks>
    [ComImport]
    [Guid("DA8D8A16-B2B6-4487-A2F1-594CCCCD6BF5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface Interface
    {
        /// <summary>
        ///  Gets the general package identifier.
        /// </summary>
        /// <param name="pbstrId">Pointer to receive the package identifier.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getid">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetId(BSTR* pbstrId);

        /// <summary>
        ///  Gets the version of the package.
        /// </summary>
        /// <param name="pbstrVersion">Pointer to receive the package version.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getversion">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetVersion(BSTR* pbstrVersion);

        /// <summary>
        ///  Gets the target process architecture of the package.
        /// </summary>
        /// <param name="pbstrChip">Pointer to receive the target architecture.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getchip">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetChip(BSTR* pbstrChip);

        /// <summary>
        ///  Gets the language and optional region identifier.
        /// </summary>
        /// <param name="pbstrLanguage">Pointer to receive the language identifier.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getlanguage">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetLanguage(BSTR* pbstrLanguage);

        /// <summary>
        ///  Gets the build branch of the package.
        /// </summary>
        /// <param name="pbstrBranch">Pointer to receive the build branch.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getbranch">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetBranch(BSTR* pbstrBranch);

        /// <summary>
        ///  Gets the type of the package.
        /// </summary>
        /// <param name="pbstrType">Pointer to receive the package type.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.gettype">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetType(BSTR* pbstrType);

        /// <summary>
        ///  Gets the unique identifier consisting of all defined tokens.
        /// </summary>
        /// <param name="pbstrUniqueId">Pointer to receive the unique identifier.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getuniqueid">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetUniqueId(BSTR* pbstrUniqueId);

        /// <summary>
        ///  Gets a value indicating whether the package refers to an external extension.
        /// </summary>
        /// <param name="pfIsExtension">Pointer to receive whether the package is an external extension.</param>
        /// <remarks>
        ///  <para>
        ///   <see href="https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.setup.configuration.isetuppackagereference.getisextension">
        ///    Official documentation.
        ///   </see>
        ///  </para>
        /// </remarks>
        /// <returns>Standard <see cref="HRESULT"/> indicating success or failure.</returns>
        [PreserveSig]
        HRESULT GetIsExtension(VARIANT_BOOL* pfIsExtension);
    }
}
