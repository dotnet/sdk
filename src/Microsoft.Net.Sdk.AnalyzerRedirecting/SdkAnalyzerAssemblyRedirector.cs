// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Workspaces.AnalyzerRedirecting;

// Example:
// FullPath: "C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\8.0.8\analyzers\dotnet\System.Windows.Forms.Analyzers.dll"
// ProductVersion: "8.0.8"
// PathSuffix: "analyzers\dotnet"
using AnalyzerInfo = (string FullPath, string ProductVersion, string PathSuffix);

namespace Microsoft.Net.Sdk.AnalyzerRedirecting;

[Export(typeof(IAnalyzerAssemblyRedirector))]
public sealed class SdkAnalyzerAssemblyRedirector : IAnalyzerAssemblyRedirector
{
    private readonly string? _insertedAnalyzersDirectory;

    /// <summary>
    /// Map from analyzer assembly name (file name without extension) to a list of matching analyzers.
    /// </summary>
    private readonly ImmutableDictionary<string, List<AnalyzerInfo>> _analyzerMap;

    [ImportingConstructor]
    public SdkAnalyzerAssemblyRedirector()
        : this(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\DotNetRuntimeAnalyzers"))) { }

    // Internal for testing.
    internal SdkAnalyzerAssemblyRedirector(string? insertedAnalyzersDirectory)
    {
        _insertedAnalyzersDirectory = insertedAnalyzersDirectory;
        _analyzerMap = CreateAnalyzerMap();
    }

    private ImmutableDictionary<string, List<AnalyzerInfo>> CreateAnalyzerMap()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, List<AnalyzerInfo>>(StringComparer.OrdinalIgnoreCase);

        // Expects layout like:
        // VsInstallDir\SDK\RuntimeAnalyzers\WindowsDesktopAnalyzers\8.0.8\analyzers\dotnet\System.Windows.Forms.Analyzers.dll
        //                                   ~~~~~~~~~~~~~~~~~~~~~~~                                                           = topLevelDirectory
        //                                                           ~~~~~                                                     = versionDirectory
        //                                                                 ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ = analyzerPath

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
                    string analyzerName = Path.GetFileNameWithoutExtension(analyzerPath);
                    string pathSuffix = analyzerPath.Substring(versionDirectory.Length + 1 /* slash */);
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
        }

        return builder.ToImmutable();
    }

    public string? RedirectPath(string fullPath)
    {
        if (_analyzerMap.TryGetValue(Path.GetFileNameWithoutExtension(fullPath), out var analyzers))
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
}
