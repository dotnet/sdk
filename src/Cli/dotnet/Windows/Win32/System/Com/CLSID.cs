// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    ///  42843719-DB4C-46C2-8E7C-64F1816EFD5B
    /// </remarks>
    internal static Guid SetupConfiguration2 { get; } = new(0x42843719, 0xdb4c, 0x46c2, 0x8e, 0x7c, 0x64, 0xf1, 0x81, 0x6e, 0xfd, 0x5b);

    /// <summary>
    ///  Visual Studio Setup Configuration COM component.
    /// </summary>
    /// <remarks>
    ///  177F0C4A-1CD3-4DE7-A32C-71DBBB9FA36D
    /// </remarks>
    internal static Guid SetupConfiguration { get; } = new(0x177f0c4a, 0x1cd3, 0x4de7, 0xa3, 0x2c, 0x71, 0xdb, 0xbb, 0x9f, 0xa3, 0x6d);
}
