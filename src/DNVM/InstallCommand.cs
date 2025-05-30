
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Semver;
using Serde;
using Serde.Json;
using Spectre.Console;
using StaticCs;
using Zio;

namespace Dnvm;

public static partial class InstallCommand
{
    public enum Result
    {
        Success = 0,
        CouldntFetchReleaseIndex,
        UnknownChannel,
        ManifestFileCorrupted,
        ManifestIOError,
        InstallError
    }

    public sealed record Options
    {
        public required SemVersion SdkVersion { get; init; }
        public bool Force { get; init; } = false;
        public SdkDirName? SdkDir { get; init; } = null;
        public bool Verbose { get; init; } = false;
    }

    public static Task<Result> Run(DnvmEnv env, Logger logger, DnvmSubCommand.InstallArgs args)
    {
        return Run(env, logger, new Options
        {
            SdkVersion = args.SdkVersion,
            Force = args.Force ?? false,
            SdkDir = args.SdkDir,
            Verbose = args.Verbose ?? false,
        });
    }

    public static async Task<Result> Run(DnvmEnv env, Logger logger, Options options)
    {
        var sdkDir = options.SdkDir ?? DnvmEnv.DefaultSdkDirName;

        Manifest manifest;
        try
        {
            manifest = await ManifestUtils.ReadOrCreateManifest(env);
        }
        catch (InvalidDataException)
        {
            logger.Error("Manifest file corrupted");
            return Result.ManifestFileCorrupted;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error("Error reading manifest file: " + e.Message);
            return Result.ManifestIOError;
        }

        var sdkVersion = options.SdkVersion;
        var channel = new Channel.VersionedMajorMinor(sdkVersion.Major, sdkVersion.Minor);

        if (!options.Force && ManifestUtils.IsSdkInstalled(manifest, sdkVersion, sdkDir))
        {
            logger.Log($"Version {sdkVersion} is already installed in directory '{sdkDir.Name}'." +
                " Skipping installation. To install anyway, pass --force.");
            return Result.Success;
        }

        DotnetReleasesIndex versionIndex;
        try
        {
            versionIndex = await DotnetReleasesIndex.FetchLatestIndex(env.HttpClient, env.DotnetFeedUrls);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error($"Could not fetch the releases index: {e.Message}");
            return Result.CouldntFetchReleaseIndex;
        }

        var result = await TryGetReleaseFromIndex(env.HttpClient, versionIndex, channel, sdkVersion)
            ?? await TryGetReleaseFromServer(env, sdkVersion);
        if (result is not ({ } sdkComponent, { } release))
        {
            logger.Error($"SDK version '{sdkVersion}' could not be found in .NET releases index or server.");
            return Result.UnknownChannel;
        }

        var installError = await InstallSdk(env, manifest, sdkComponent, release, sdkDir, logger);
        if (installError is not Result<Manifest, InstallError>.Ok)
        {
            return Result.InstallError;
        }

        return Result.Success;
    }

    internal static async Task<(ChannelReleaseIndex.Component, ChannelReleaseIndex.Release)?> TryGetReleaseFromIndex(
        ScopedHttpClient httpClient,
        DotnetReleasesIndex versionIndex,
        Channel channel,
        SemVersion sdkVersion)
    {
        if (versionIndex.GetChannelIndex(channel) is { } channelIndex)
        {
            var channelReleaseIndexText = await httpClient.GetStringAsync(channelIndex.ChannelReleaseIndexUrl);
            var releaseIndex = JsonSerializer.Deserialize<ChannelReleaseIndex>(channelReleaseIndexText);
            var result =
                from r in releaseIndex.Releases
                from sdk in r.Sdks
                where sdk.Version == sdkVersion
                select (sdk, r);

            return result.FirstOrDefault();
        }

        return null;
    }

    private static async Task<(ChannelReleaseIndex.Component, ChannelReleaseIndex.Release)?> TryGetReleaseFromServer(
        DnvmEnv env,
        SemVersion sdkVersion)
    {
        foreach (var feedUrl in env.DotnetFeedUrls)
        {
            var downloadUrl = $"/Sdk/{sdkVersion}/productCommit-{Utilities.CurrentRID}.json";
            try
            {
                var productCommitData = JsonSerializer.Deserialize<CommitData>(await env.HttpClient.GetStringAsync(feedUrl.TrimEnd('/') + downloadUrl));
                if (productCommitData.Installer.Version != sdkVersion)
                {
                    throw new InvalidOperationException("Fetched product commit data does not match requested SDK version");
                }
                var archiveName = ConstructArchiveName(versionString: sdkVersion.ToString(), Utilities.CurrentRID, Utilities.ZipSuffix);
                var sdk = new ChannelReleaseIndex.Component
                {
                    Version = sdkVersion,
                    Files = [ new ChannelReleaseIndex.File
                    {
                        Name = archiveName,
                        Url = MakeSdkUrl(feedUrl, sdkVersion, archiveName),
                        Rid = Utilities.CurrentRID.ToString(),
                    }]
                };

                var release = new ChannelReleaseIndex.Release
                {
                    Sdk = sdk,
                    AspNetCore = new ChannelReleaseIndex.Component
                    {
                        Version = productCommitData.Aspnetcore.Version,
                        Files = []
                    },
                    Runtime = new()
                    {
                        Version = productCommitData.Runtime.Version,
                        Files = []
                    },
                    ReleaseVersion = productCommitData.Runtime.Version,
                    Sdks = [sdk],
                    WindowsDesktop = new()
                    {
                        Version = productCommitData.Windowsdesktop.Version,
                        Files = []
                    },
                };
                return (sdk, release);
            }
            catch
            {
                continue;
            }
        }
        return null;
    }

    private static string MakeSdkUrl(string feedUrl, SemVersion version, string archiveName)
    {
        return feedUrl.TrimEnd('/') + $"/Sdk/{version}/{archiveName}";
    }

    [GenerateDeserialize]
    private partial record CommitData
    {
        public required Component Installer { get; init; }
        public required Component Sdk { get; init; }
        public required Component Aspnetcore { get; init; }
        public required Component Runtime { get; init; }
        public required Component Windowsdesktop { get; init; }

        [GenerateDeserialize]
        public partial record Component
        {
            [SerdeMemberOptions(DeserializeProxy = typeof(SemVersionProxy))]
            public required SemVersion Version { get; init;  }
        }
    }

    [Closed]
    internal enum InstallError
    {
        DownloadFailed,
        ExtractFailed
    }

    /// <summary>
    /// Install the given SDK inside the given directory, and update the manifest. Does not update the channel manifest.
    /// </summary>
    /// <exception cref="InvalidOperationException">Throws when manifest already contains the given SDK.</exception>
    internal static async Task<Result<Manifest, InstallError>> InstallSdk(
        DnvmEnv env,
        Manifest manifest,
        ChannelReleaseIndex.Component sdkComponent,
        ChannelReleaseIndex.Release release,
        SdkDirName sdkDir,
        Logger logger)
    {
        var sdkVersion = sdkComponent.Version;

        var ridString = Utilities.CurrentRID.ToString();
        var sdkInstallPath = UPath.Root / sdkDir.Name;
        var downloadFile = sdkComponent.Files.Single(f => f.Rid == ridString && f.Url.EndsWith(Utilities.ZipSuffix));
        var link = downloadFile.Url;
        logger.Info("Download link: " + link);

        logger.Log($"Downloading SDK {sdkVersion} for {ridString}");
        var curMuxerVersion = manifest.MuxerVersion(sdkDir);
        var err = await InstallSdkToDir(curMuxerVersion, release.Runtime.Version, env.HttpClient, link, env.DnvmHomeFs, sdkInstallPath, env.TempFs, logger);
        if (err is not null)
        {
            return err;
        }

        SelectCommand.SelectDir(logger, env, manifest.CurrentSdkDir, sdkDir);

        var result = JsonSerializer.Serialize(manifest);
        logger.Info("Existing manifest: " + result);

        if (!ManifestUtils.IsSdkInstalled(manifest, sdkVersion, sdkDir))
        {
            manifest = manifest with
            {
                InstalledSdks = manifest.InstalledSdks.Add(new InstalledSdk
                {
                    ReleaseVersion = release.ReleaseVersion,
                    RuntimeVersion = release.Runtime.Version,
                    AspNetVersion = release.AspNetCore.Version,
                    SdkVersion = sdkVersion,
                    SdkDirName = sdkDir,
                })
            };

            env.WriteManifest(manifest);
        }

        return manifest;
    }

    internal static async Task<InstallError?> InstallSdkToDir(
        SemVersion? curMuxerVersion,
        SemVersion runtimeVersion,
        ScopedHttpClient httpClient,
        string downloadUrl,
        IFileSystem destFs,
        UPath destPath,
        IFileSystem tempFs,
        Logger logger)
    {
        // The Release name does not contain a version
        string archiveName = ConstructArchiveName(versionString: null, Utilities.CurrentRID, Utilities.ZipSuffix);

        // Download and extract into a temp directory
        using var tempDir = new DirectoryResource(Directory.CreateTempSubdirectory().FullName);
        string archivePath = Path.Combine(tempDir.Path, archiveName);
        logger.Info("Archive path: " + archivePath);

        var downloadError = await logger.DownloadWithProgress(
            httpClient,
            archivePath,
            downloadUrl,
            "Downloading SDK");

        if (downloadError is not null)
        {
            logger.Error(downloadError);
            return InstallError.DownloadFailed;
        }

        logger.Log($"Installing to {destPath}");
        string? extractResult = await Utilities.ExtractSdkToDir(
            curMuxerVersion,
            runtimeVersion,
            archivePath,
            tempFs,
            destFs,
            destPath);
        File.Delete(archivePath);
        if (extractResult != null)
        {
            logger.Error("Extract failed: " + extractResult);
            return InstallError.ExtractFailed;
        }

        var dotnetExePath = destPath / Utilities.DotnetExeName;
        if (!OperatingSystem.IsWindows())
        {
            logger.Info("chmoding downloaded host");
            try
            {
                Utilities.ChmodExec(destFs, dotnetExePath);
            }
            catch (Exception e)
            {
                logger.Error("chmod failed: " + e.Message);
                return InstallError.ExtractFailed;
            }
        }

        return null;
    }

    internal static string ConstructArchiveName(
        string? versionString,
        RID rid,
        string suffix)
    {
        return versionString is null
            ? $"dotnet-sdk-{rid}{suffix}"
            : $"dotnet-sdk-{versionString}-{rid}{suffix}";
    }
}
