// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Global access point for the default path resolver instance.
/// </summary>
/// <remarks>
/// This provides a transitional mechanism for legacy code to access the path resolver
/// without requiring dependency injection throughout the entire codebase.
///
/// New code should prefer constructor injection of IPathResolver where possible.
///
/// Usage:
/// - Set Default once at application startup (Program.cs)
/// - Access via PathResolver.Default throughout the codebase
/// </remarks>
public static class PathResolver
{
    private static IPathResolver? s_default;

    /// <summary>
    /// Gets or sets the default path resolver instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to get the default instance before it has been set.
    /// </exception>
    public static IPathResolver Default
    {
        get
        {
            if (s_default == null)
            {
                throw new InvalidOperationException(
                    "PathResolver has not been initialized. " +
                    "Call PathResolver.Initialize() or set PathResolver.Default in Program.Main.");
            }
            return s_default;
        }
        set => s_default = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Initializes the default path resolver based on environment configuration.
    /// </summary>
    /// <remarks>
    /// This method should be called once at application startup.
    ///
    /// If DOTNET_ROOT or DOTNET_SDK_ROOT environment variables are set,
    /// uses ConfigurablePathResolver to enable portable scenarios.
    /// Otherwise, uses StandardLayoutPathResolver for standard SDK layout.
    /// </remarks>
    public static void Initialize()
    {
        // Check if environment variables are set for portable configuration
        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        string? sdkRoot = Environment.GetEnvironmentVariable("DOTNET_SDK_ROOT");

        if (!string.IsNullOrEmpty(dotnetRoot) || !string.IsNullOrEmpty(sdkRoot))
        {
            // User has configured portable layout via environment variables
            s_default = new ConfigurablePathResolver(dotnetRoot, sdkRoot);
        }
        else
        {
            // Standard SDK layout
            s_default = new StandardLayoutPathResolver();
        }
    }

    /// <summary>
    /// Initializes the default path resolver with an explicit instance.
    /// </summary>
    /// <param name="resolver">The path resolver instance to use as default</param>
    /// <remarks>
    /// This overload is useful for testing or when you need explicit control
    /// over the path resolver configuration.
    /// </remarks>
    public static void Initialize(IPathResolver resolver)
    {
        s_default = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// Resets the default path resolver (primarily for testing).
    /// </summary>
    internal static void Reset()
    {
        s_default = null;
    }
}
