// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Caches loaded-image values before the executable can be renamed or replaced.</summary>
internal static class DotnetupProcessInfo
{
    public static string? ExecutablePath { get; } = Environment.ProcessPath;
    public static string BuildIdentity { get; } = DotnetupBuildIdentity.Current;
}