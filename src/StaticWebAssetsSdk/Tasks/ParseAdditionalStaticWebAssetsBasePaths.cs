// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Parses the AdditionalStaticWebAssetsBasePath property which is a semicolon-separated list of
/// content-root,base-path pairs and discovers the files in each content root.
/// </summary>
public class ParseAdditionalStaticWebAssetsBasePaths : Task
{
    /// <summary>
    /// The semicolon-separated list of content-root,base-path pairs.
    /// Format: content-root-1,base-path-1;content-root-2,base-path-2
    /// </summary>
    [Required]
    public string AdditionalStaticWebAssetsBasePaths { get; set; }

    /// <summary>
    /// The parsed content root and base path pairs as items with ContentRoot and BasePath metadata.
    /// </summary>
    [Output]
    public ITaskItem[] ParsedBasePaths { get; set; }

    /// <summary>
    /// The discovered files from all content roots with appropriate metadata for DefineStaticWebAssets.
    /// </summary>
    [Output]
    public ITaskItem[] DiscoveredFiles { get; set; }

    public override bool Execute()
    {
        var parsedPaths = new List<ITaskItem>();
        var discoveredFiles = new List<ITaskItem>();

        if (string.IsNullOrEmpty(AdditionalStaticWebAssetsBasePaths))
        {
            ParsedBasePaths = [];
            DiscoveredFiles = [];
            return true;
        }

        var pairs = AdditionalStaticWebAssetsBasePaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split(new[] { ',' }, 2, StringSplitOptions.None);

            if (parts.Length < 2)
            {
                Log.LogError(
                    "Invalid format for AdditionalStaticWebAssetsBasePath entry '{0}'. Expected format: 'content-root,base-path'.",
                    pair);
                continue;
            }

            var contentRoot = parts[0].Trim();
            var basePath = parts[1].Trim();

            if (string.IsNullOrEmpty(contentRoot))
            {
                Log.LogError(
                    "Content root is empty in AdditionalStaticWebAssetsBasePath entry '{0}'.",
                    pair);
                continue;
            }

            // Normalize content root path to end with directory separator
#if NET
            if (!contentRoot.EndsWith(Path.DirectorySeparatorChar) &&
                !contentRoot.EndsWith(Path.AltDirectorySeparatorChar))
#else
            if (!contentRoot.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !contentRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
#endif
            {
                contentRoot += Path.DirectorySeparatorChar;
            }

            // Make content root path absolute if it's relative
            if (!Path.IsPathRooted(contentRoot))
            {
                contentRoot = Path.GetFullPath(contentRoot);
            }

            var pathItem = new TaskItem(parsedPaths.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            pathItem.SetMetadata("ContentRoot", contentRoot);
            pathItem.SetMetadata("BasePath", basePath);

            Log.LogMessage(MessageImportance.Low,
                "Parsed additional static web asset base path: ContentRoot='{0}', BasePath='{1}'",
                contentRoot,
                basePath);

            parsedPaths.Add(pathItem);

            // Discover files from this content root
            if (Directory.Exists(contentRoot))
            {
                var files = Directory.GetFiles(contentRoot, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fullPath = Path.GetFullPath(file);
#if NET
                    var relativePath = Path.GetRelativePath(contentRoot, fullPath);
#else
                    // For .NET Framework, manually compute relative path
                    var relativePath = fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase)
                        ? fullPath.Substring(contentRoot.Length)
                        : fullPath;
#endif

                    // Normalize path separators
                    relativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');

                    var fileItem = new TaskItem(fullPath);
                    fileItem.SetMetadata("ContentRoot", contentRoot);
                    fileItem.SetMetadata("BasePath", basePath);
                    fileItem.SetMetadata("RelativePath", relativePath);

                    Log.LogMessage(MessageImportance.Low,
                        "Discovered file '{0}' with relative path '{1}' in content root '{2}'",
                        fullPath,
                        relativePath,
                        contentRoot);

                    discoveredFiles.Add(fileItem);
                }
            }
            else
            {
                Log.LogWarning(
                    "Content root directory '{0}' does not exist.",
                    contentRoot);
            }
        }

        ParsedBasePaths = [.. parsedPaths];
        DiscoveredFiles = [.. discoveredFiles];

        return !Log.HasLoggedErrors;
    }
}
