// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Configurable path resolver that supports environment variable-based configuration.
/// Enables portable/relocated CLI scenarios by reading DOTNET_ROOT and DOTNET_SDK_ROOT.
/// </summary>
/// <remarks>
/// Configuration priority (highest to lowest):
/// 1. Constructor parameters (for testing/explicit configuration)
/// 2. Environment variables (DOTNET_ROOT, DOTNET_SDK_ROOT)
/// 3. Discovery fallbacks (same as StandardLayoutPathResolver)
///
/// Required environment variables for portable scenarios:
/// - DOTNET_ROOT: Root installation directory
/// - DOTNET_SDK_ROOT: Versioned SDK tools directory
///
/// The dotnet executable path is always auto-discovered from the running process.
/// </remarks>
public class ConfigurablePathResolver : IPathResolver
{
    /// <summary>
    /// Creates a new configurable path resolver.
    /// </summary>
    /// <param name="dotnetRoot">
    /// Override for dotnet root. If null, reads from DOTNET_ROOT environment variable,
    /// then falls back to discovering from process path.
    /// </param>
    /// <param name="sdkRoot">
    /// Override for SDK root. If null, reads from DOTNET_SDK_ROOT environment variable,
    /// then falls back to AppContext.BaseDirectory.
    /// </param>
    public ConfigurablePathResolver(string? dotnetRoot = null, string? sdkRoot = null)
    {
        // We ARE the dotnet process - get our executable path
        // This is the same for all configurations
        DotnetExecutable = GetCurrentProcessPath();

        // Priority: explicit param > env var > discovery
        DotnetRoot = dotnetRoot
            ?? Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? DiscoverDotnetRoot();

        SdkRoot = sdkRoot
            ?? Environment.GetEnvironmentVariable("DOTNET_SDK_ROOT")
            ?? AppContext.BaseDirectory;
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

    private string DiscoverDotnetRoot()
    {
        // Fallback: use the directory containing the dotnet executable
        string? root = Path.GetDirectoryName(DotnetExecutable);

        if (string.IsNullOrEmpty(root))
        {
            throw new InvalidOperationException(
                "Cannot discover dotnet root. Set DOTNET_ROOT environment variable or " +
                "ensure dotnet executable path is valid.");
        }

        return root;
    }
}
