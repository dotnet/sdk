// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Caches loaded-image values before the executable can be renamed or replaced.</summary>
internal static class DotnetupProcessInfo
{
    public static string? ExecutablePath { get; } = Environment.ProcessPath;
    public static string VersionMetadata { get; } = DotnetupVersionMetadata.Current;
    public static bool IsDirectExecution { get; } = IsDotnetupExecutable(ExecutablePath, Assembly.GetEntryAssembly()?.GetName().Name);

    internal static bool IsDotnetupExecutable(string? executablePath, string? entryAssemblyName)
        => !string.IsNullOrEmpty(executablePath)
            && string.Equals(entryAssemblyName, "dotnetup", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileNameWithoutExtension(executablePath), "dotnet", StringComparison.OrdinalIgnoreCase);
}