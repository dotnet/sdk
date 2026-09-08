// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.Versioning;

/// <summary>
/// Base type for all platform-specific API attributes.
/// </summary>
internal abstract class OSPlatformAttribute : Attribute
{
    private protected OSPlatformAttribute(string platformName)
    {
        PlatformName = platformName;
    }
    public string PlatformName { get; }
}
