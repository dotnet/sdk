// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
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

        List<PackageSource> configuredSources =
            [.. new PackageSourceProvider(settings).LoadPackageSources()];
        List<PackageSource> sources = sourceFeedOverrides?.Any() == true
            ? CreatePackageSources(sourceFeedOverrides, configuredSources, basePath, invalidSource)
            : [.. configuredSources.Where(source => source.IsEnabled)];

        AddPackageSources(sources, additionalSourceFeeds, configuredSources, basePath, invalidSource);
        ValidateSources(sources);
        return new NuGetSourceConfiguration(settings, sources);
    }

    private static List<PackageSource> CreatePackageSources(
        IEnumerable<string> sourceFeeds,
        IReadOnlyList<PackageSource> configuredSources,
        string basePath,
        Action<string>? invalidSource)
    {
        List<PackageSource> sources = [];
        AddPackageSources(sources, sourceFeeds, configuredSources, basePath, invalidSource);
        return sources;
    }

    private static void AddPackageSources(
        List<PackageSource> sources,
        IEnumerable<string>? sourceFeeds,
        IReadOnlyList<PackageSource> configuredSources,
        string basePath,
        Action<string>? invalidSource)
    {
        if (sourceFeeds is null)
        {
            return;
        }

        foreach (string sourceFeed in sourceFeeds)
        {
            if (string.IsNullOrWhiteSpace(sourceFeed))
            {
                continue;
            }

            string source = NormalizeSource(sourceFeed, basePath);
            PackageSource packageSource = ResolveSource(configuredSources, sourceFeed, source);
            if (packageSource.TrySourceAsUri is null)
            {
                invalidSource?.Invoke(sourceFeed);
                continue;
            }

            if (!sources.Any(existingSource => SourcesEqual(existingSource, packageSource)))
            {
                sources.Add(packageSource);
            }
        }
    }

    private static PackageSource ResolveSource(
        IReadOnlyList<PackageSource> configuredSources,
        string sourceFeed,
        string normalizedSource)
        => configuredSources.FirstOrDefault(source =>
            source.Name.Equals(sourceFeed, StringComparison.OrdinalIgnoreCase) ||
            SourcesEqual(source, new PackageSource(normalizedSource)))
        ?? new PackageSource(normalizedSource);

    private static bool SourcesEqual(PackageSource first, PackageSource second)
    {
        Uri? firstUri = first.TrySourceAsUri;
        Uri? secondUri = second.TrySourceAsUri;
        if (firstUri is not null && secondUri is not null)
        {
            if (firstUri.IsFile && secondUri.IsFile)
            {
                return GetPathComparer().Equals(firstUri.LocalPath, secondUri.LocalPath);
            }

            return firstUri.Equals(secondUri);
        }

        return GetPathComparer().Equals(first.Source, second.Source);
    }

    private static StringComparer GetPathComparer()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void ValidateSources(IEnumerable<PackageSource> sources)
    {
        foreach (PackageSource source in sources)
        {
            if (source.IsHttp && !source.IsHttps && !source.AllowInsecureConnections)
            {
                throw new GracefulException(string.Format(
                    LocalizableStrings.Error_NU1302_HttpSourceUsed,
                    source.Source));
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
