// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Text.Json;
using Microsoft.CodeAnalysis.Workspaces.AnalyzerRedirecting;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

// Example:
// FullPath: "C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\8.0.8\analyzers\dotnet\System.Windows.Forms.Analyzers.dll"
// ProductVersion: "8.0.8"
// PathSuffix: "analyzers\dotnet"
using AnalyzerInfo = (string FullPath, string ProductVersion, string PathSuffix);

namespace Microsoft.Net.Sdk.AnalyzerRedirecting;

/// <summary>
/// See <c>documentation/general/analyzer-redirecting.md</c>.
/// </summary>
[Export(typeof(IAnalyzerAssemblyRedirector))]
public sealed class SdkAnalyzerAssemblyRedirector : IAnalyzerAssemblyRedirector
{
    private readonly IVsActivityLog? _log;

    private readonly bool _enabled;

    private readonly string? _insertedAnalyzersDirectory;

    /// <summary>
    /// Map from analyzer assembly name (file name without extension) to a list of matching analyzers.
    /// </summary>
    private readonly ImmutableDictionary<string, List<AnalyzerInfo>> _analyzerMap;

    [ImportingConstructor]
    public SdkAnalyzerAssemblyRedirector(SVsServiceProvider serviceProvider) : this(
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\DotNetRuntimeAnalyzers")),
        serviceProvider.GetService<SVsActivityLog, IVsActivityLog>())
    {
    }

    // Internal for testing.
    internal SdkAnalyzerAssemblyRedirector(string? insertedAnalyzersDirectory, IVsActivityLog? log = null)
    {
        _log = log;
        var enable = Environment.GetEnvironmentVariable("DOTNET_ANALYZER_REDIRECTING");
        _enabled = !"0".Equals(enable, StringComparison.OrdinalIgnoreCase) && !"false".Equals(enable, StringComparison.OrdinalIgnoreCase);
        _insertedAnalyzersDirectory = insertedAnalyzersDirectory;
        _analyzerMap = CreateAnalyzerMap();
    }

    private ImmutableDictionary<string, List<AnalyzerInfo>> CreateAnalyzerMap()
    {
        if (!_enabled)
        {
            Log("Analyzer redirecting is disabled.");
            return ImmutableDictionary<string, List<AnalyzerInfo>>.Empty;
        }

        var metadataFilePath = Path.Combine(_insertedAnalyzersDirectory, "metadata.json");
        if (!File.Exists(metadataFilePath))
        {
            Log($"File does not exist: {metadataFilePath}");
            return ImmutableDictionary<string, List<AnalyzerInfo>>.Empty;
        }

        var versions = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(metadataFilePath));
        if (versions is null || versions.Count == 0)
        {
            Log($"Versions are empty: {metadataFilePath}");
            return ImmutableDictionary<string, List<AnalyzerInfo>>.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, List<AnalyzerInfo>>(StringComparer.OrdinalIgnoreCase);

        // Expects layout like:
        // VsInstallDir\DotNetRuntimeAnalyzers\WindowsDesktopAnalyzers\analyzers\dotnet\System.Windows.Forms.Analyzers.dll
        //                                     ~~~~~~~~~~~~~~~~~~~~~~~                                                     = topLevelDirectory
        //                                                             ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ = analyzerPath

        foreach (string topLevelDirectory in Directory.EnumerateDirectories(_insertedAnalyzersDirectory))
        {
            foreach (string analyzerPath in Directory.EnumerateFiles(topLevelDirectory, "*.dll", SearchOption.AllDirectories))
            {
                if (!analyzerPath.StartsWith(topLevelDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string subsetName = Path.GetFileName(topLevelDirectory);
                if (!versions.TryGetValue(subsetName, out string version))
                {
                    continue;
                }

                string analyzerName = Path.GetFileNameWithoutExtension(analyzerPath);
                string pathSuffix = analyzerPath.Substring(topLevelDirectory.Length + 1 /* slash */);
                pathSuffix = Path.GetDirectoryName(pathSuffix);

                AnalyzerInfo analyzer = new() { FullPath = analyzerPath, ProductVersion = version, PathSuffix = pathSuffix };

                if (builder.TryGetValue(analyzerName, out var existing))
                {
                    existing.Add(analyzer);
                }
                else
                {
                    builder.Add(analyzerName, [analyzer]);
                }
            }
        }

        Log($"Loaded analyzer map ({builder.Count}): {_insertedAnalyzersDirectory}");

        return builder.ToImmutable();
    }

    public string? RedirectPath(string fullPath)
    {
        if (_enabled && _analyzerMap.TryGetValue(Path.GetFileNameWithoutExtension(fullPath), out var analyzers))
        {
            foreach (AnalyzerInfo analyzer in analyzers)
            {
                var directoryPath = Path.GetDirectoryName(fullPath);

                // Note that both paths we compare here are normalized via netfx's Path.GetDirectoryName.
                if (directoryPath.EndsWith(analyzer.PathSuffix, StringComparison.OrdinalIgnoreCase) &&
                    majorAndMinorVersionsMatch(directoryPath, analyzer.PathSuffix, analyzer.ProductVersion))
                {
                    return analyzer.FullPath;
                }
            }
        }

        return null;

        static bool majorAndMinorVersionsMatch(string directoryPath, string pathSuffix, string version)
        {
            // Find the version number in the directory path - it is in the directory name before the path suffix.
            // Example:
            // "C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\8.0.8\analyzers\dotnet\" = directoryPath
            //                                                                       ~~~~~~~~~~~~~~~~   = pathSuffix
            //                                                                 ~~~~~                    = directoryPathVersion
            // This can match also a NuGet package because the version number is at the same position:
            // "C:\.nuget\packages\Microsoft.WindowsDesktop.App.Ref\8.0.8\analyzers\dotnet\"

            int index = directoryPath.LastIndexOf(pathSuffix, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            string directoryPathVersion = Path.GetFileName(Path.GetDirectoryName(directoryPath.Substring(0, index)));

            return areVersionMajorMinorPartEqual(directoryPathVersion, version);
        }

        static bool areVersionMajorMinorPartEqual(string version1, string version2)
        {
            int firstDotIndex = version1.IndexOf('.');
            if (firstDotIndex < 0)
            {
                return false;
            }

            int secondDotIndex = version1.IndexOf('.', firstDotIndex + 1);
            if (secondDotIndex < 0)
            {
                return false;
            }

            return 0 == string.Compare(version1, 0, version2, 0, secondDotIndex, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void Log(string message)
    {
        _log?.LogEntry(
            (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION,
            nameof(SdkAnalyzerAssemblyRedirector),
            message);
    }
}
