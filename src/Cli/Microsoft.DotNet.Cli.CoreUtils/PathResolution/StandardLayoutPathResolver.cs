// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Standard layout path resolver using current SDK discovery mechanisms.
/// This implementation maintains backward compatibility with existing behavior.
/// </summary>
/// <remarks>
/// Discovery strategy:
/// 1. DotnetExecutable: Current process path (Environment.ProcessPath)
/// 2. DotnetRoot: Parent directory of dotnet executable
/// 3. SdkRoot: Current SDK location (AppContext.BaseDirectory)
///
/// This assumes the standard .NET SDK layout where:
/// - CLI is at {dotnetRoot}/sdk/{version}/
/// - Dotnet executable is at {dotnetRoot}/dotnet
/// </remarks>
public class StandardLayoutPathResolver : IPathResolver
{
    public StandardLayoutPathResolver()
    {
        // We ARE the dotnet process - get our executable path
        DotnetExecutable = GetCurrentProcessPath();

        // In standard layout, dotnet root is the directory containing the executable
        DotnetRoot = Path.GetDirectoryName(DotnetExecutable)
            ?? throw new InvalidOperationException("Cannot determine dotnet root from executable path");

        // Current SDK is where we're executing from
        SdkRoot = AppContext.BaseDirectory;
    }

    public string DotnetRoot { get; }
    public string SdkRoot { get; }
    public string DotnetExecutable { get; }

    private static string GetCurrentProcessPath()
    {
#if NET6_0_OR_GREATER
        string? processPath = Environment.ProcessPath;
#else
        string? processPath = Process.GetCurrentProcess().MainModule?.FileName;
#endif

        if (string.IsNullOrEmpty(processPath))
        {
            throw new InvalidOperationException(
                "Cannot determine current process path. " +
                "Environment.ProcessPath is null or empty.");
        }

        return processPath;
    }
}
