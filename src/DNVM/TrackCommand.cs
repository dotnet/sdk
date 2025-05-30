
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Semver;
using Serde.Json;
using StaticCs;
using Zio;
using static Dnvm.Utilities;

namespace Dnvm;

public sealed class TrackCommand
{
    public sealed record Options
    {
        public required Channel Channel { get; init; }
        /// <summary>
        /// URL to the dotnet feed containing the releases index and SDKs.
        /// </summary>
        public string? FeedUrl { get; init; }
        public bool Verbose { get; init; } = false;
        public bool Force { get; init; } = false;
        /// <summary>
        /// Answer yes to every question or use the defaults.
        /// </summary>
        public bool Yes { get; init; } = false;
        public bool Prereqs { get; init; } = false;
        public SdkDirName SdkDir { get; init; } = DnvmEnv.DefaultSdkDirName;
    }

    private readonly DnvmEnv _env;

    private readonly Logger _logger;
    private readonly Options _opts;
    private readonly IEnumerable<string> _feedUrls;

    public enum Result
    {
        Success = 0,
        UnknownChannel,
        InstallLocationNotWritable,
        NotASingleFile,
        InstallFailed,
        SelfInstallFailed,
        ManifestIOError,
        ManifestFileCorrupted,
        ChannelAlreadyTracked,
        CouldntFetchIndex
    }

    public TrackCommand(DnvmEnv env, Logger logger, Options opts)
    {
        _env = env;
        _logger = logger;
        _opts = opts;
        if (_opts.Verbose)
        {
            _logger.LogLevel = LogLevel.Info;
        }
        _feedUrls = opts.FeedUrl is not null
            ? [ opts.FeedUrl.TrimEnd('/') ]
            : env.DotnetFeedUrls;
    }

    public static Task<Result> Run(DnvmEnv env, Logger logger, Options opts)
    {
        return new TrackCommand(env, logger, opts).Run();
    }

    public static Task<Result> Run(DnvmEnv env, Logger logger, DnvmSubCommand.TrackArgs args)
    {
        var sdkDir = args.SdkDir == null
            ? DnvmEnv.DefaultSdkDirName
            : new SdkDirName(args.SdkDir);
        var opts = new Options
        {
            Channel = args.Channel,
            FeedUrl = args.FeedUrl,
            Verbose = args.Verbose ?? false,
            Force = args.Force ?? false,
            Yes = args.Yes ?? false,
            Prereqs = args.Prereqs ?? false,
            SdkDir = sdkDir
        };
        return Run(env, logger, opts);
    }

    public async Task<Result> Run()
    {
        var dnvmHome = _env.RealPath(UPath.Root);
        var sdkInstallPath = Path.Combine(dnvmHome, _opts.SdkDir.Name);
        try
        {
            Directory.CreateDirectory(dnvmHome);
            Directory.CreateDirectory(sdkInstallPath);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.Error($"Cannot write to install location. Ensure you have appropriate permissions.");
            return Result.InstallLocationNotWritable;
        }

        Manifest manifest;
        try
        {
            manifest = await ManifestUtils.ReadOrCreateManifest(_env);
        }
        catch (InvalidDataException)
        {
            _logger.Error("Manifest file corrupted");
            return Result.ManifestFileCorrupted;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.Error("Error reading manifest file: " + e.Message);
            return Result.ManifestIOError;
        }

        var channel = _opts.Channel;
        if (manifest.TrackedChannels().Any(c => c.ChannelName == channel))
        {
            _logger.Log($"Channel '{channel.GetDisplayName()}' is already being tracked." +
                " Did you mean to run 'dnvm update'?");
            return Result.ChannelAlreadyTracked;
        }

        manifest = manifest.TrackChannel(new RegisteredChannel {
            ChannelName = channel,
            SdkDirName = _opts.SdkDir,
            InstalledSdkVersions = EqArray<SemVersion>.Empty
        });

        return await InstallLatestFromChannel(
            manifest,
            _env,
            _logger,
            channel,
            _opts.Force,
            _feedUrls,
            _opts.SdkDir);
    }

    internal static async Task<Result> InstallLatestFromChannel(
        Manifest manifest,
        DnvmEnv dnvmEnv,
        Logger logger,
        Channel channel,
        bool force,
        IEnumerable<string> feedUrls,
        SdkDirName sdkDir)
    {
        DotnetReleasesIndex versionIndex;
        try
        {
            versionIndex = await DotnetReleasesIndex.FetchLatestIndex(dnvmEnv.HttpClient, feedUrls);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error($"Could not fetch the releases index: {e.Message}");
            return Result.CouldntFetchIndex;
        }

        RID rid = Utilities.CurrentRID;

        var latestChannelIndex = versionIndex.GetChannelIndex(channel);
        if (latestChannelIndex is null)
        {
            // If the channel index is missing there are two possibilities:
            //   1. The channel is not supported by the index
            //   2. The channel is supported but there are currently no builds available
            // We want to provide a helpful error message about the first case, but let the second
            // case go through without attempting to install anything.
            if (channel is Channel.VersionedMajorMinor or Channel.VersionedFeature)
            {
                logger.Error($"Could not find channel '{channel}' in the dotnet releases index.");
                return Result.UnknownChannel;
            }
            else
            {
                logger.Log($"""
No builds available for channel '{channel}'. This often happens for preview channels when the first
preview has not yet been released.
Proceeding without SDK installation.
""");
                dnvmEnv.WriteManifest(manifest);
                return Result.Success;
            }
        }

        // Proceed with SDK installation

        var latestSdkVersion = SemVersion.Parse(latestChannelIndex.LatestSdk, SemVersionStyles.Strict);
        logger.Log("Found latest version: " + latestSdkVersion);

        var result = await InstallCommand.TryGetReleaseFromIndex(dnvmEnv.HttpClient, versionIndex, channel, latestSdkVersion);
        if (result is not ({} component, {} release))
        {
            throw new InvalidOperationException($"Could not find release {latestSdkVersion} in index. This is a bug.");
        }

        if (!force && manifest.InstalledSdks.Any(s => s.SdkVersion == latestSdkVersion && s.SdkDirName == sdkDir))
        {
            logger.Log($"Version {latestSdkVersion} is already installed in directory '{sdkDir.Name}'." +
                " Skipping installation. To install anyway, pass --force.");
        }
        else
        {
            var installResult = await InstallCommand.InstallSdk(
                dnvmEnv,
                manifest,
                component,
                release,
                sdkDir,
                logger);

            if (installResult is not Result<Manifest, InstallCommand.InstallError>.Ok(var newManifest))
            {
                return Result.InstallFailed;
            }
            manifest = newManifest;
        }

        // Even if the SDK was already installed, we'll add it to the list of tracked versions
        // for this channel. Otherwise you end up with non-determinism where the order of channel
        // updates starts to matter. Consider if you track LTS and latest and the same version is in
        // both channels. One channel will "win" when installing it, and then the next will skip
        // installation. If the version is only added to one channel's tracking list, the result is
        // not deterministic. By adding it to both, it doesn't matter what order we update channels in.
        logger.Info($"Adding installed version '{latestSdkVersion}' to manifest.");
        var oldTracked = manifest.RegisteredChannels.Single(t => t.ChannelName == channel);
        if (oldTracked.InstalledSdkVersions.Contains(latestSdkVersion))
        {
            logger.Info("Version already tracked");
        }
        else
        {
            var newTracked = oldTracked with {
                InstalledSdkVersions = oldTracked.InstalledSdkVersions.Add(latestSdkVersion)
            };
            manifest = manifest with { RegisteredChannels = manifest.RegisteredChannels.Replace(oldTracked, newTracked) };
        }

        logger.Info("Writing manifest");
        dnvmEnv.WriteManifest(manifest);

        logger.Log("Successfully installed");

        return Result.Success;
    }
}
