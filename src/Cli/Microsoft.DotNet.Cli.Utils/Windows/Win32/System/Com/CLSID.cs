// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Com.Urlmon;

namespace Windows.Win32.System.Com;

/// <summary>
///  Represents CLSIDs for various COM components.
/// </summary>
internal static class CLSID
{
    /// <summary>
    ///  Visual Studio Setup Configuration COM component.
    /// </summary>
    /// <remarks>
    ///  42843719-db4c-46c2-8e7c-64f1816efd5b
    /// </remarks>
    internal static Guid SetupConfiguration2 { get; } = new(0x42843719, 0xdb4c, 0x46c2, 0x8e, 0x7c, 0x64, 0xf1, 0x81, 0x6e, 0xfd, 0x5b);

    /// <summary>
    ///  Visual Studio Setup Configuration COM component.
    /// </summary>
    /// <remarks>
    ///  177f0c4a-1cd3-4de7-a32c-71dbbb9fa36d
    /// </remarks>
    internal static Guid SetupConfiguration { get; } = new(0x177f0c4a, 0x1cd3, 0x4de7, 0xa3, 0x2c, 0x71, 0xdb, 0xbb, 0x9f, 0xa3, 0x6d);

    /// <summary>
    ///  <see cref="IInternetSecurityManager"/>
    /// </summary>
    /// <remarks>
    ///  7b8a2d94-0ac9-11d1-896c-00c04fb6bfc4
    /// </remarks>
    internal static Guid InternetSecurityManager { get; } = new(0x7b8a2d94, 0x0ac9, 0x11d1, 0x89, 0x6c, 0x00, 0xc0, 0x4f, 0xb6, 0xbf, 0xc4);
}
