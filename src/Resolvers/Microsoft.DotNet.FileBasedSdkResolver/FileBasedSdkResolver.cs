// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.MSBuildSdkResolver;

/// <summary>
/// SDK resolver that resolves SDKs from local file paths (relative or absolute).
/// This is useful for testing custom SDKs during development without having to install them.
/// </summary>
public sealed class FileBasedSdkResolver : SdkResolver
{
    public override string Name => "Microsoft.DotNet.FileBasedSdkResolver";

    // Run before the default resolver (5000) but after workload resolver (4000)
    public override int Priority => 4500;

    public override SdkResult? Resolve(SdkReference sdkReference, SdkResolverContext context, SdkResultFactory factory)
    {
        // Check if the SDK name looks like a file path
        string sdkName = sdkReference.Name;

        if (string.IsNullOrEmpty(sdkName))
        {
            return null;
        }

        // Only process if it looks like a path (contains directory separators or starts with . or ..)
        if (!IsLikelyFilePath(sdkName))
        {
            return null;
        }

        // Resolve the path relative to the project directory
        string? baseDirectory = GetBaseDirectory(context);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            return null;
        }

        string resolvedPath;
        try
        {
            // Combine the base directory with the SDK path
            resolvedPath = Path.GetFullPath(Path.Combine(baseDirectory, sdkName));
        }
        catch
        {
            // Invalid path, let other resolvers handle it
            return null;
        }

        // Check if the directory exists
        if (!Directory.Exists(resolvedPath))
        {
            return factory.IndicateFailure(new[] { $"SDK directory '{resolvedPath}' does not exist." });
        }

        // Look for Sdk subdirectory (standard SDK layout)
        string sdkDirectory = Path.Combine(resolvedPath, "Sdk");
        if (!Directory.Exists(sdkDirectory))
        {
            // If no Sdk subdirectory, use the path directly
            sdkDirectory = resolvedPath;
        }

        // Verify that at least one of Sdk.props or Sdk.targets exists
        string propsFile = Path.Combine(sdkDirectory, "Sdk.props");
        string targetsFile = Path.Combine(sdkDirectory, "Sdk.targets");

        if (!File.Exists(propsFile) && !File.Exists(targetsFile))
        {
            return factory.IndicateFailure(new[] {
                $"SDK directory '{sdkDirectory}' does not contain Sdk.props or Sdk.targets files."
            });
        }

        // Return success with the resolved path
        return factory.IndicateSuccess(
            path: sdkDirectory,
            version: null, // File-based SDKs don't have a version
            warnings: null);
    }

    private static bool IsLikelyFilePath(string sdkName)
    {
        // Check if it contains path separators or starts with relative path indicators
        return sdkName.Contains('/') ||
               sdkName.Contains('\\') ||
               sdkName.StartsWith(".") ||
               sdkName.StartsWith("~") ||
               (sdkName.Length > 1 && sdkName[1] == ':'); // Windows absolute path like C:
    }

    private static string? GetBaseDirectory(SdkResolverContext context)
    {
        // Try solution directory first, then project directory
        if (!string.IsNullOrWhiteSpace(context.SolutionFilePath))
        {
            return Path.GetDirectoryName(context.SolutionFilePath);
        }
        else if (!string.IsNullOrWhiteSpace(context.ProjectFilePath))
        {
            return Path.GetDirectoryName(context.ProjectFilePath);
        }

        // Fallback to current directory
        return Environment.CurrentDirectory;
    }
}
