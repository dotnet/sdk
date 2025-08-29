// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET9_0_OR_GREATER
using GeneratedWhenPossibleComInterfaceAttribute = System.Runtime.InteropServices.Marshalling.GeneratedComInterfaceAttribute;
#else
using GeneratedWhenPossibleComInterfaceAttribute = System.Runtime.InteropServices.ComImportAttribute;
#endif

using System.Runtime.CompilerServices;

#if NET
using System.Runtime.InteropServices.Marshalling;
#endif

#if NET9_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
#endif

namespace Microsoft.DotNet.Cli.Utils;

#if NET
[GeneratedComInterface(StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
#else
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComConversionLoss]
#endif
[Guid("79EAC9EE-BAF9-11CE-8C82-00AA004BA90B")]
// UrlMonTypeLib.IInternetSecurityManager
internal partial interface IInternetSecurityManager
{
    void SetSecuritySite([MarshalAs(UnmanagedType.Interface)] in IInternetSecurityMgrSite pSite);

    void GetSecuritySite([MarshalAs(UnmanagedType.Interface)] out IInternetSecurityMgrSite ppSite);

    void MapUrlToZone(in string pwszUrl, out int pdwZone, in int dwFlags);

    void GetSecurityId(in string pwszUrl, out byte pbSecurityId, ref int pcbSecurityId, [ComAliasName("UrlMonTypeLib.ULONG_PTR")] in int dwReserved);

    void ProcessUrlAction(in string pwszUrl, in int dwAction, out byte pPolicy, in int cbPolicy, ref byte pContext, in int cbContext, in int dwFlags, in int dwReserved);

    void QueryCustomPolicy(in string pwszUrl, ref Guid guidKey, out IntPtr ppPolicy, out int pcbPolicy, ref byte pContext, in int cbContext, in int dwReserved);

    void SetZoneMapping(in int dwZone, in string lpszPattern, in int dwFlags);

    void GetZoneMappings(in int dwZone, [MarshalAs(UnmanagedType.Interface)] out IEnumString ppenumString, in int dwFlags);
}

#if NET
[GeneratedComClass]
internal partial class InternetSecurityManager : IInternetSecurityManager
{
    public void SetSecuritySite([MarshalAs(UnmanagedType.Interface)] in IInternetSecurityMgrSite pSite) => throw new NotImplementedException();
    public void GetSecuritySite([MarshalAs(UnmanagedType.Interface)] out IInternetSecurityMgrSite ppSite) => throw new NotImplementedException();
    public void MapUrlToZone(in string pwszUrl, out int pdwZone, in int dwFlags) => throw new NotImplementedException();
    public void GetSecurityId(in string pwszUrl, out byte pbSecurityId, ref int pcbSecurityId, in int dwReserved) => throw new NotImplementedException();
    public void ProcessUrlAction(in string pwszUrl, in int dwAction, out byte pPolicy, in int cbPolicy, ref byte pContext, in int cbContext, in int dwFlags, in int dwReserved) => throw new NotImplementedException();
    public void QueryCustomPolicy(in string pwszUrl, ref Guid guidKey, out IntPtr ppPolicy, out int pcbPolicy, ref byte pContext, in int cbContext, in int dwReserved) => throw new NotImplementedException();
    public void SetZoneMapping(in int dwZone, in string lpszPattern, in int dwFlags) => throw new NotImplementedException();
    public void GetZoneMappings(in int dwZone, [MarshalAs(UnmanagedType.Interface)] out IEnumString ppenumString, in int dwFlags) => throw new NotImplementedException();
}
#endif

// UrlMonTypeLib.IInternetSecurityMgrSite

#if NET
[GeneratedComInterface]
#else
[ComImport]
[ComConversionLoss]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#endif
[Guid("79EAC9ED-BAF9-11CE-8C82-00AA004BA90B")]
partial interface IInternetSecurityMgrSite
{
    void GetWindow([ComAliasName("UrlMonTypeLib.wireHWND")] out IntPtr phwnd);

    void EnableModeless(in int fEnable);
}


[StructLayout(LayoutKind.Sequential, Pack = 4)]
struct GUID
{
    public int Data1;

    public ushort Data2;

    public ushort Data3;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Data4;
}

#if NET
[GeneratedComInterface(StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
#else
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#endif
[Guid("00000101-0000-0000-C000-000000000046")]
partial interface IEnumString
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    void RemoteNext(in int celt, out string rgelt, out int pceltFetched);

    [MethodImpl(MethodImplOptions.InternalCall)]
    void Skip(in int celt);

    [MethodImpl(MethodImplOptions.InternalCall)]
    void Reset();

    [MethodImpl(MethodImplOptions.InternalCall)]
    void Clone([MarshalAs(UnmanagedType.Interface)] out IEnumString ppenum);
}
