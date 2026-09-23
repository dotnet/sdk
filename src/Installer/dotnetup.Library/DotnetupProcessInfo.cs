// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Caches loaded-image values before the executable can be renamed or replaced.</summary>
internal static class DotnetupProcessInfo
{
    public static string? ExecutablePath { get; } = Environment.ProcessPath;
    public static string Version { get; } = Parser.Version;
    public static bool IsDirectExecution { get; } = IsDotnetupExecutable(
        ExecutablePath,
        Assembly.GetEntryAssembly()?.GetName().Name,
        RuntimeFeature.IsDynamicCodeSupported);

    internal static bool IsDotnetupExecutable(string? executablePath, string? entryAssemblyName, bool isDynamicCodeSupported)
        => !string.IsNullOrEmpty(executablePath)
            && !isDynamicCodeSupported
            && string.Equals(entryAssemblyName, "dotnetup", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileNameWithoutExtension(executablePath), "dotnet", StringComparison.OrdinalIgnoreCase);
}