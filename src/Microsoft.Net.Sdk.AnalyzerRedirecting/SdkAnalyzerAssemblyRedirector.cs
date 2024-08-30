// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using AnalyzerInfo = (int MajorVersion, string PathSuffix, string FullPath);

namespace Microsoft.Net.Sdk.AnalyzerRedirecting;

[Export(typeof(IAnalyzerAssemblyRedirector))]
public sealed class SdkAnalyzerAssemblyRedirector : IAnalyzerAssemblyRedirector
{
    private readonly string? _insertedAnalyzersDirectory;
    private readonly Lazy<ImmutableDictionary<string, List<AnalyzerInfo>>> _analyzerMap;

    [ImportingConstructor]
    public SdkAnalyzerAssemblyRedirector()
        : this(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "SDK", "RuntimeAnalyzers"))) { }

    // Internal for testing.
    internal SdkAnalyzerAssemblyRedirector(string? insertedAnalyzersDirectory)
    {
        _insertedAnalyzersDirectory = insertedAnalyzersDirectory;
        _analyzerMap = new(CreateAnalyzerMap);
    }

    /// <summary>
    /// Map from analyzer assembly name (file name without extension) to a list of matching analyzers.
    /// </summary>
    private ImmutableDictionary<string, List<AnalyzerInfo>> AnalyzerMap => _analyzerMap.Value;

    private ImmutableDictionary<string, List<AnalyzerInfo>> CreateAnalyzerMap()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, List<AnalyzerInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (string topLevelDirectory in Directory.EnumerateDirectories(_insertedAnalyzersDirectory))
        {
            foreach (string versionDirectory in Directory.EnumerateDirectories(topLevelDirectory))
            {
                foreach (string analyzerPath in Directory.EnumerateFiles(versionDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    if (!analyzerPath.StartsWith(versionDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string version = Path.GetFileName(versionDirectory);
                    string majorVersionStr = version.IndexOf('.') is >= 0 and var index ? version.Substring(0, index) : version;
                    if (!int.TryParse(majorVersionStr, out int majorVersion))
                    {
                        continue;
                    }

                    string analyzerName = Path.GetFileNameWithoutExtension(analyzerPath);
                    string pathSuffix = analyzerPath.Substring(versionDirectory.Length + (EndsWithSlash(versionDirectory) ? 0 : 1));
                    pathSuffix = Path.GetDirectoryName(pathSuffix);

                    if (builder.TryGetValue(analyzerName, out var existing))
                    {
                        existing.Add((majorVersion, pathSuffix, analyzerPath));
                    }
                    else
                    {
                        builder.Add(analyzerName, new() { (majorVersion, pathSuffix, analyzerPath) });
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    public string? RedirectPath(string fullPath)
    {
        if (AnalyzerMap.TryGetValue(Path.GetFileNameWithoutExtension(fullPath), out var analyzers))
        {
            foreach (var analyzer in analyzers)
            {
                var directoryPath = Path.GetDirectoryName(fullPath);
                if (endsWithIgnoringTrailingSlashes(directoryPath, analyzer.PathSuffix) &&
                    hasMajorVersion(directoryPath, analyzer.PathSuffix, analyzer.MajorVersion))
                {
                    return analyzer.FullPath;
                }
            }
        }

        return null;

        static bool hasMajorVersion(string directoryPath, string pathSuffix, int majorVersion)
        {
            // Find the version number in the directory path which is in the directory name before the path suffix.
            int index = directoryPath.LastIndexOf(pathSuffix, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }
            string version = Path.GetFileName(Path.GetDirectoryName(directoryPath.Substring(0, index)));

            // Check that the major version part matches.
            return version.IndexOf('.') is >= 0 and var dotIndex &&
                int.TryParse(version.Substring(0, dotIndex), out int versionMajor) &&
                versionMajor == majorVersion;
        }

        static bool endsWithIgnoringTrailingSlashes(string s, string suffix)
        {
            var sEndsWithSlash = EndsWithSlash(s);
            var suffixEndsWithSlash = EndsWithSlash(suffix);
            var index = s.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            return index >= 0 && index + suffix.Length - (suffixEndsWithSlash ? 1 : 0) == s.Length - (sEndsWithSlash ? 1 : 0);
        }
    }

    private static bool EndsWithSlash(string s) => !string.IsNullOrEmpty(s) && s[s.Length - 1] is '/' or '\\';
}
