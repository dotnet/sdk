// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Sdk.Razor.Tests;
public partial class StaticWebAssetsBaselineFactory
{
    [GeneratedRegex("""(.*\.)([0123456789abcdefghijklmnopqrstuvwxyz]{10})(\.bundle\.scp\.css)((?:\.gz)|(?:\.br))?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScopedProjectBundleRegex();

    [GeneratedRegex("""(.*\.)([0123456789abcdefghijklmnopqrstuvwxyz]{10})(\.styles\.css)((?:\.gz)|(?:\.br))?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScopedAppBundleRegex();

    [GeneratedRegex("""fingerprint-site(\.)([0123456789abcdefghijklmnopqrstuvwxyz]{10})(\.css)((?:\.gz)|(?:\.br))?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FingerprintedSiteCssRegex();

    [GeneratedRegex("""(?:#\[\.{fingerprint=[0123456789abcdefghijklmnopqrstuvwxyz]{10}\}](\?|\!)?)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedFingerprintExpression();

    [GeneratedRegex("""(.*\.)([0123456789abcdefghijklmnopqrstuvwxyz]{10})(\.lib\.module\.js)((?:\.gz)|(?:\.br))?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JSInitializerRegex();

    private static readonly IList<(Regex expression, string replacement)> WellKnownFileNamePatternsAndReplacements =
    [
        (ScopedProjectBundleRegex(),"$1__fingerprint__$3$4"),
        (ScopedAppBundleRegex(),"$1__fingerprint__$3$4"),
        (JSInitializerRegex(), "$1__fingerprint__$3$4"),
        (EmbeddedFingerprintExpression(), "#[.{fingerprint=__fingerprint__}]$1"),
        (FingerprintedSiteCssRegex(), "fingerprint-site$1__fingerprint__$3$4"),
    ];

    public static StaticWebAssetsBaselineFactory Instance { get; } = new();

    public IList<string> KnownExtensions { get; } =
    [
        // Keep this list of most specific to less specific
        ".dll.gz",
        ".dll.br",
        ".dll",
        ".wasm.gz",
        ".wasm.br",
        ".wasm",
        ".js.gz",
        ".js.br",
        ".js",
        ".html",
        ".pdb",
    ];

    public IList<string> KnownFilePrefixesWithHashOrVersion { get; } =
    [
        "blazor.web.",
        "blazor.server",
        "dotnet.runtime",
        "dotnet.native",
        "dotnet"
    ];

    public void ToTemplate(
        StaticWebAssetsManifest manifest,
        string projectRoot,
        string restorePath,
        string runtimeIdentifier)
    {
        manifest.Hash = "__hash__";
        var assetsByIdentity = manifest.Assets.ToDictionary(a => a.Identity);
        var endpointsByAssetFile = manifest.Endpoints.GroupBy(e => e.AssetFile).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var asset in manifest.Assets)
        {
            var relatedEndpoints = endpointsByAssetFile.GetValueOrDefault(asset.Identity);
            TemplatizeAsset(projectRoot, restorePath, runtimeIdentifier, asset);
            foreach (var endpoint in relatedEndpoints ?? [])
            {
                endpoint.AssetFile = asset.Identity;
            }
            if (asset.AssetTraitName == "Content-Encoding")
            {
                var basePath = asset.BasePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var relativePath = asset.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                var identity = asset.Identity.Replace('\\', Path.DirectorySeparatorChar);
                var originalItemSpec = asset.OriginalItemSpec.Replace('\\', Path.DirectorySeparatorChar);

                asset.Identity = Path.Combine(Path.GetDirectoryName(identity), basePath, relativePath);
                asset.Identity = asset.Identity.Replace(Path.DirectorySeparatorChar, '\\');
                foreach (var endpoint in relatedEndpoints ?? [])
                {
                    endpoint.AssetFile = asset.Identity;
                }
                asset.OriginalItemSpec = Path.Combine(Path.GetDirectoryName(originalItemSpec), basePath, relativePath);
                asset.OriginalItemSpec = asset.OriginalItemSpec.Replace(Path.DirectorySeparatorChar, '\\');
            }
            else if ((asset.Identity.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || asset.Identity.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                && asset.AssetTraitName == "" && asset.RelatedAsset == "")
            {
                // Old .NET 5.0 implementation
                var identity = asset.Identity.Replace('\\', Path.DirectorySeparatorChar);
                var originalItemSpec = asset.OriginalItemSpec.Replace('\\', Path.DirectorySeparatorChar);

                asset.Identity = Path.Combine(Path.GetDirectoryName(identity), Path.GetFileName(originalItemSpec) + Path.GetExtension(identity))
                    .Replace(Path.DirectorySeparatorChar, '\\');
            }
        }

        foreach (var endpoint in manifest.Endpoints)
        {
            foreach (var header in endpoint.ResponseHeaders)
            {
                switch (header.Name)
                {
                    case "Content-Length":
                        header.Value = "__content-length__";
                        break;
                    case "ETag":
                        header.Value = "__etag__";
                        break;
                    case "Last-Modified":
                        header.Value = "__last-modified__";
                        break;
                    case "Link":
                        var cleaned = new List<string>();
                        var values = header.Value.Split(',').Select(v => v.Trim());
                        foreach (var value in values)
                        {
                            var segments = value.Split(';').Select(v => v.Trim()).ToArray();
                            var file = segments[0][1..^1];
                            segments[0] = $"<{ReplaceFileName(file).Replace('\\', '/')}>";
                            cleaned.Add(string.Join("; ", segments));
                        }
                        header.Value = string.Join(", ", cleaned);

                        break;
                    default:
                        break;
                }
            }

            foreach (var property in endpoint.EndpointProperties)
            {
                switch (property.Name)
                {
                    case "fingerprint":
                        property.Value = "__fingerprint__";
                        endpoint.Route = endpoint.Route.Replace(property.Value, $"__{property.Name}__");
                        break;
                    case "integrity":
                        property.Value = "__integrity__";
                        break;
                    default:
                        break;
                }

                ReplaceFileName(endpoint.Route);
            }

            foreach (var selector in endpoint.Selectors)
            {
                selector.Quality = "__quality__";
            }

            endpoint.Route = TemplatizeFilePath(endpoint.Route, null, null, null, null, null).Replace("\\", "/");

            endpoint.AssetFile = TemplatizeFilePath(
                endpoint.AssetFile,
                restorePath,
                projectRoot,
                null,
                null,
                runtimeIdentifier);
        }

        foreach (var discovery in manifest.DiscoveryPatterns)
        {
            discovery.ContentRoot = discovery.ContentRoot.Replace(projectRoot, "${ProjectPath}", StringComparison.OrdinalIgnoreCase);
            discovery.ContentRoot = discovery.ContentRoot.Replace(Path.DirectorySeparatorChar, '\\');

            discovery.Name = discovery.Name.Replace(Path.DirectorySeparatorChar, '\\');
            discovery.Pattern = discovery.Pattern.Replace(Path.DirectorySeparatorChar, '\\');
        }

        foreach (var relatedManifest in manifest.ReferencedProjectsConfiguration)
        {
            relatedManifest.Identity = relatedManifest.Identity.Replace(projectRoot, "${ProjectPath}").Replace(Path.DirectorySeparatorChar, '\\');
        }

        // Sor everything now to ensure we produce stable baselines independent of the machine they were generated on.
        Array.Sort(manifest.DiscoveryPatterns, (l, r) => StringComparer.Ordinal.Compare(l.Name, r.Name));
        Array.Sort(manifest.Assets);
        foreach (var endpoint in manifest.Endpoints)
        {
            Array.Sort(endpoint.Selectors);
            Array.Sort(endpoint.EndpointProperties);
            Array.Sort(endpoint.ResponseHeaders);
        }
        Array.Sort(manifest.Endpoints);

        Array.Sort(manifest.ReferencedProjectsConfiguration, (l, r) => StringComparer.Ordinal.Compare(l.Identity, r.Identity));
    }

    private void TemplatizeAsset(string projectRoot, string restorePath, string runtimeIdentifier, StaticWebAsset asset)
    {
        asset.Identity = TemplatizeFilePath(
            asset.Identity,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);

        asset.RelativePath = TemplatizeFilePath(
            asset.RelativePath,
            null,
            null,
            null,
            null,
            runtimeIdentifier).Replace('\\', '/');

        asset.ContentRoot = TemplatizeFilePath(
            asset.ContentRoot,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);

        asset.RelatedAsset = TemplatizeFilePath(
            asset.RelatedAsset,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);

        asset.OriginalItemSpec = TemplatizeFilePath(
            asset.OriginalItemSpec,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);

        asset.Fingerprint = string.IsNullOrEmpty(asset.Fingerprint) ? asset.Fingerprint : "__fingerprint__";
        asset.Integrity = string.IsNullOrEmpty(asset.Integrity) ? asset.Integrity : "__integrity__";
    }

    internal IEnumerable<string> TemplatizeExpectedFiles(
        IEnumerable<string> files,
        string restorePath,
        string projectPath,
        string intermediateOutputPath,
        string buildOrPublishFolder)
    {
        foreach (var file in files)
        {
            var updated = TemplatizeFilePath(
                file,
                restorePath,
                projectPath,
                intermediateOutputPath,
                buildOrPublishFolder,
                null);

            yield return updated;
        }
    }

    public string TemplatizeFilePath(
        string file,
        string restorePath,
        string projectPath,
        string intermediateOutputPath,
        string buildOrPublishFolder,
        string runtimeIdentifier)
    {
        var updated = file switch
        {
            var processed when file.StartsWith('$') => processed,
            var fromBuildOrPublishPath when buildOrPublishFolder is not null && file.StartsWith(buildOrPublishFolder, StringComparison.OrdinalIgnoreCase) =>
                TemplatizeBuildOrPublishPath(buildOrPublishFolder, fromBuildOrPublishPath),
            var fromIntermediateOutputPath when intermediateOutputPath is not null && file.StartsWith(intermediateOutputPath, StringComparison.OrdinalIgnoreCase) =>
                TemplatizeIntermediatePath(intermediateOutputPath, fromIntermediateOutputPath),
            var fromPackage when restorePath is not null && file.StartsWith(restorePath, StringComparison.OrdinalIgnoreCase) =>
                TemplatizeNugetPath(restorePath, fromPackage),
            var fromProject when projectPath is not null && file.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase) =>
                TemplatizeProjectPath(projectPath, fromProject, runtimeIdentifier),
            _ =>
                ReplaceSegments(file, (i, segments) => i switch
                {
                    2 when segments[0] is "obj" or "bin" => "${Tfm}",
                    var last when i == segments.Length - 1 => RemovePossibleHash(segments[last]),
                    _ => segments[i]
                })
        };

        return ReplaceFileName(updated).Replace('/', '\\');
    }

    private static string ReplaceFileName(string path)
    {
        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        foreach (var (expression, replacement) in WellKnownFileNamePatternsAndReplacements)
        {
            if (expression.IsMatch(fileName))
            {
                fileName = expression.Replace(fileName, replacement);
                return Path.Combine(directory, fileName);
            }
        }

        return path;
    }

    private string TemplatizeBuildOrPublishPath(string outputPath, string file)
    {
        file = file.Replace(outputPath, "${OutputPath}")
            .Replace('\\', '/');

        file = ReplaceSegments(file, (i, segments) => i switch
        {
            _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
            _ => segments[i],
        });

        return file;
    }

    private string TemplatizeIntermediatePath(string intermediatePath, string file)
    {
        file = file.Replace(intermediatePath, "${IntermediateOutputPath}")
            .Replace('\\', '/');

        file = ReplaceSegments(file, (i, segments) => i switch
        {
            3 when segments[1] is "obj" or "bin" => "${Tfm}",
            _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
            _ => segments[i]
        });

        return file;
    }

    private string TemplatizeProjectPath(string projectPath, string file, string runtimeIdentifier)
    {
        file = file.Replace(projectPath, "${ProjectPath}")
            .Replace('\\', '/');

        file = ReplaceSegments(file, (i, segments) => i switch
        {
            3 when segments[1] is "obj" or "bin" => "${Tfm}",
            4 when segments[2] is "obj" or "bin" => "${Tfm}",
            4 when segments[1] is "obj" or "bin" && segments[4] == runtimeIdentifier => "${Rid}",
            5 when segments[2] is "obj" or "bin" && segments[5] == runtimeIdentifier => "${Rid}",
            _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
            _ => segments[i]
        });

        return file;
    }

    private string TemplatizeNugetPath(string restorePath, string file)
    {
        file = file.Replace(restorePath, "${RestorePath}", StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/');
        if (file.Contains("runtimes"))
        {
            file = ReplaceSegments(file, (i, segments) => i switch
            {
                2 => "${RuntimeVersion}",
                6 when !file.Contains("native") => "${Tfm}",
                _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
                _ => segments[i],
            });
        }
        else
        {
            file = ReplaceSegments(file, (i, segments) => i switch
            {
                2 => "${PackageVersion}",
                4 when IsFramework(segments[4]) => "${Tfm}",
                _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
                _ => segments[i],
            });
        }

        return file;

        static bool IsFramework(string segment)
        {
            try
            {
                var tfm = NuGetFramework.ParseFolder(segment);

                return tfm.Framework is FrameworkConstants.FrameworkIdentifiers.NetCoreApp or
                    FrameworkConstants.FrameworkIdentifiers.NetStandard or
                    FrameworkConstants.FrameworkIdentifiers.NetCore or
                    FrameworkConstants.FrameworkIdentifiers.Net;
            }
            catch
            {
                return false;
            }
        }
    }

    private static string ReplaceSegments(string file, Func<int, string[], string> selector)
    {
        var segments = file.Split('\\', '/');
        var newSegments = new List<string>();

        // Segments have the following shape `${RestorePath}/PackageName/PackageVersion/lib/Tfm/dll`.
        // We want to replace PackageVersion and Tfm with tokens so that they do not cause issues.
        for (var i = 0; i < segments.Length; i++)
        {
            newSegments.Add(selector(i, segments));
        }

        return string.Join(Path.DirectorySeparatorChar, newSegments);
    }

    private string RemovePossibleHash(string fileNameAndExtension)
    {
        var filename = KnownFilePrefixesWithHashOrVersion.FirstOrDefault(p => fileNameAndExtension.StartsWith(p));
        if (filename != null && filename.EndsWith("."))
        {
            filename = filename[..^1];
        }
        var extension = KnownExtensions.FirstOrDefault(f => fileNameAndExtension.EndsWith(f, StringComparison.OrdinalIgnoreCase));
        if (filename != null && extension != null)
        {
            fileNameAndExtension = filename + extension;
        }

        return fileNameAndExtension;
    }
}
