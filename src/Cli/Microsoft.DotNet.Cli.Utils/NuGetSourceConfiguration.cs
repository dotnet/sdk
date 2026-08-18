// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.Utils;

public sealed class NuGetSourceConfiguration
{
    private NuGetSourceConfiguration(ISettings settings, IReadOnlyList<PackageSource> packageSources)
    {
        Settings = settings;
        PackageSources = packageSources;
    }

    public ISettings Settings { get; }

    public IReadOnlyList<PackageSource> PackageSources { get; }

    public static NuGetSourceConfiguration Load(
        string? nugetConfig = null,
        string? rootConfigDirectory = null,
        IEnumerable<string>? sourceFeedOverrides = null,
        IEnumerable<string>? additionalSourceFeeds = null,
        string? basePath = null,
        Action<string>? invalidSource = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        ISettings settings;
        if (!string.IsNullOrWhiteSpace(nugetConfig))
        {
            string configPath = GetFullPath(nugetConfig, basePath);
            settings = NuGet.Configuration.Settings.LoadSpecificSettings(
                Path.GetDirectoryName(configPath)!,
                Path.GetFileName(configPath));
        }
        else
        {
            settings = NuGet.Configuration.Settings.LoadDefaultSettings(rootConfigDirectory ?? basePath);
        }

        List<PackageSource> sources = sourceFeedOverrides?.Any() == true
            ? CreatePackageSources(sourceFeedOverrides, basePath, invalidSource)
            : [.. new PackageSourceProvider(settings).LoadPackageSources().Where(source => source.IsEnabled)];

        AddPackageSources(sources, additionalSourceFeeds, basePath, invalidSource);
        return new NuGetSourceConfiguration(settings, sources);
    }

    private static List<PackageSource> CreatePackageSources(
        IEnumerable<string> sourceFeeds,
        string basePath,
        Action<string>? invalidSource)
    {
        List<PackageSource> sources = [];
        AddPackageSources(sources, sourceFeeds, basePath, invalidSource);
        return sources;
    }

    private static void AddPackageSources(
        List<PackageSource> sources,
        IEnumerable<string>? sourceFeeds,
        string basePath,
        Action<string>? invalidSource)
    {
        if (sourceFeeds is null)
        {
            return;
        }

        HashSet<string> existingSources = new(
            sources.Select(source => NormalizeSource(source.Source, basePath)),
            StringComparer.OrdinalIgnoreCase);

        foreach (string sourceFeed in sourceFeeds)
        {
            if (string.IsNullOrWhiteSpace(sourceFeed))
            {
                continue;
            }

            string source = NormalizeSource(sourceFeed, basePath);
            PackageSource packageSource = new(source);
            if (packageSource.TrySourceAsUri is null)
            {
                invalidSource?.Invoke(sourceFeed);
                continue;
            }

            if (existingSources.Add(source))
            {
                sources.Add(packageSource);
            }
        }
    }

    private static string NormalizeSource(string source, string basePath)
    {
        if (!Uri.IsWellFormedUriString(source, UriKind.Absolute) && !Path.IsPathRooted(source))
        {
            return GetFullPath(source, basePath);
        }

        return source;
    }

    private static string GetFullPath(string path, string basePath)
        => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
}
