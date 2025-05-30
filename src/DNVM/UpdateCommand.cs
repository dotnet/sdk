using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Semver;
using Serde.Json;
using Spectre.Console;
using StaticCs;
using static Dnvm.UpdateCommand.Result;

namespace Dnvm;

public sealed partial class UpdateCommand
{
    public sealed record Options
    {
        /// <summary>
        /// URL to the dnvm releases.json file listing the latest releases and their download
        /// locations.
        /// </summary>
        public string? DnvmReleasesUrl { get; init; }
        public string? FeedUrl { get; init; }
        public bool Verbose { get; init; } = false;
        public bool Self { get; init; } = false;
        /// <summary>
        /// Implicitly answers 'yes' to every question.
        /// </summary>
        public bool Yes { get; init; } = false;
    }

    private readonly DnvmEnv _env;
    private readonly Logger _logger;
    private readonly Options _opts;
    private readonly IEnumerable<string> _feedUrls;
    private readonly string _releasesUrl;

    public UpdateCommand(DnvmEnv env, Logger logger, Options opts)
    {
        _logger = logger;
        _opts = opts;
        if (_opts.Verbose)
        {
            _logger.LogLevel = LogLevel.Info;
        }
        _feedUrls = _opts.FeedUrl is not null
            ? [ _opts.FeedUrl.TrimEnd('/') ]
            : env.DotnetFeedUrls;
        _releasesUrl = _opts.DnvmReleasesUrl ?? env.DnvmReleasesUrl;
        _env = env;
    }

    public static Task<Result> Run(DnvmEnv env, Logger logger, DnvmSubCommand.UpdateArgs args)
    {
        return Run(env, logger, new Options
        {
            DnvmReleasesUrl = args.DnvmReleasesUrl,
            FeedUrl = args.FeedUrl,
            Verbose = args.Verbose ?? false,
            Self = args.Self ?? false,
            Yes = args.Yes ?? false
        });
    }

    public static Task<Result> Run(DnvmEnv env, Logger logger, Options options)
    {
        return new UpdateCommand(env, logger, options).Run();
    }

    public enum Result
    {
        Success,
        CouldntFetchIndex,
        NotASingleFile,
        SelfUpdateFailed,
        UpdateFailed
    }

    public async Task<Result> Run()
    {
        var manifest = await ManifestUtils.ReadOrCreateManifest(_env);
        if (_opts.Self)
        {
            return await UpdateSelf(manifest);
        }

        DotnetReleasesIndex releaseIndex;
        try
        {
            releaseIndex = await DotnetReleasesIndex.FetchLatestIndex(_env.HttpClient, _feedUrls);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.Error($"Could not fetch the releases index: {e.Message}");
            return CouldntFetchIndex;
        }

        return await UpdateSdks(
            _env,
            _logger,
            releaseIndex,
            manifest,
            _opts.Yes,
            _releasesUrl);
    }

    public static async Task<Result> UpdateSdks(
        DnvmEnv env,
        Logger logger,
        DotnetReleasesIndex releasesIndex,
        Manifest manifest,
        bool yes,
        string releasesUrl)
    {
        logger.Log("Looking for available updates");
        // Check for dnvm updates
        if (await CheckForSelfUpdates(env.HttpClient, logger, releasesUrl, manifest.PreviewsEnabled) is (true, _))
        {
            logger.Warn("dnvm is out of date. Run 'dnvm update --self' to update dnvm.");
        }

        try
        {
            var updateResults = FindPotentialUpdates(manifest, releasesIndex);
            if (updateResults.Count > 0)
            {
                logger.Log("Found versions available for update");
                var table = new Table();
                table.AddColumn("Channel");
                table.AddColumn("Installed");
                table.AddColumn("Available");
                foreach (var (c, newestInstalled, newestAvailable, _) in updateResults)
                {
                    table.AddRow(c.ToString(), newestInstalled?.ToString() ?? "(none)", newestAvailable.LatestSdk);
                }
                logger.Console.Write(table);
                logger.Log("Install updates? [y/N]: ");
                var response = yes ? "y" : Console.ReadLine();
                if (response?.Trim().ToLowerInvariant() == "y")
                {
                    // Find releases
                    var releasesToInstall = new HashSet<(ChannelReleaseIndex.Component, ChannelReleaseIndex.Release, SdkDirName)>();
                    foreach (var (c, _, newestAvailable, sdkDir) in updateResults)
                    {
                        var latestSdkVersion = SemVersion.Parse(newestAvailable.LatestSdk, SemVersionStyles.Strict);

                        var result = await InstallCommand.TryGetReleaseFromIndex(env.HttpClient, releasesIndex, c, latestSdkVersion);
                        if (result is not ({} component, {} release))
                        {
                            logger.Error($"Index does not contain release for channel '{c}' with version '{latestSdkVersion}'."
                                + "This is either a bug or the .NET index is incorrect. Please a file a bug at https://github.com/dn-vm/dnvm.");
                            return UpdateFailed;
                        }
                        releasesToInstall.Add((component, release, sdkDir));
                    }

                    // Install releases
                    foreach (var (component, release, sdkDir) in releasesToInstall)
                    {
                        var latestSdkVersion = release.Sdk.Version;
                        var result = await InstallCommand.InstallSdk(
                            env,
                            manifest,
                            component,
                            release,
                            sdkDir,
                            logger);

                        if (result is not Result<Manifest, InstallCommand.InstallError>.Ok(var newManifest))
                        {
                            logger.Error($"Failed to install version '{latestSdkVersion}'");
                            return UpdateFailed;
                        }
                        manifest = newManifest;
                    }

                    // Update manifest for tracked channels
                    foreach (var (c, _, newestAvailable, _) in updateResults)
                    {
                        var latestSdkVersion = SemVersion.Parse(newestAvailable.LatestSdk, SemVersionStyles.Strict);


                        foreach (var oldTracked in manifest.RegisteredChannels.Where(t => t.ChannelName == c))
                        {
                            var newTracked = oldTracked with
                            {
                                InstalledSdkVersions = oldTracked.InstalledSdkVersions.Add(latestSdkVersion)
                            };
                            manifest = manifest with { RegisteredChannels = manifest.RegisteredChannels.Replace(oldTracked, newTracked) };
                        }
                    }
                }
            }
        }
        finally
        {
            logger.Info("Writing manifest");
            env.WriteManifest(manifest);
        }

        logger.Log("Successfully installed");
        return Success;
    }

    public static List<(Channel TrackedChannel, SemVersion? NewestInstalled, DotnetReleasesIndex.ChannelIndex NewestAvailable, SdkDirName SdkDir)> FindPotentialUpdates(
        Manifest manifest,
        DotnetReleasesIndex releaseIndex)
    {
        var list = new List<(Channel, SemVersion?, DotnetReleasesIndex.ChannelIndex, SdkDirName)>();
        foreach (var tracked in manifest.RegisteredChannels)
        {
            var newestInstalled = tracked.InstalledSdkVersions
                .Max(SemVersion.PrecedenceComparer);
            var release = releaseIndex.GetChannelIndex(tracked.ChannelName);
            if (release is { LatestSdk: var sdkVersion} &&
                SemVersion.TryParse(sdkVersion, SemVersionStyles.Strict, out var newestAvailable) &&
                SemVersion.ComparePrecedence(newestInstalled, newestAvailable) < 0)
            {
                list.Add((tracked.ChannelName, newestInstalled, release, tracked.SdkDirName));
            }
        }
        return list;
    }

    public async Task<Result> UpdateSelf(Manifest manifest)
    {
        if (!Utilities.IsSingleFile)
        {
            _logger.Error("Cannot self-update: the current executable is not deployed as a single file.");
            return Result.NotASingleFile;
        }

        DnvmReleases.Release release;
        switch (await CheckForSelfUpdates(_env.HttpClient, _logger, _releasesUrl, manifest.PreviewsEnabled))
        {
            case (false, null):
                return Result.SelfUpdateFailed;
            case (false, _):
                return Result.Success;
            case (true, {} r):
                release = r;
                break;
            default:
                throw ExceptionUtilities.Unreachable;
        }

        string artifactDownloadLink = GetReleaseLink(release);

        string tempArchiveDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        async Task HandleDownload(string tempDownloadPath)
        {
            _logger.Info("Extraction directory: " + tempArchiveDir);
            string? retMsg = await Utilities.ExtractArchiveToDir(tempDownloadPath, tempArchiveDir);
            if (retMsg != null)
            {
                _logger.Error("Extraction failed: " + retMsg);
            }
        }

        await DownloadBinaryToTempAndDelete(artifactDownloadLink, HandleDownload);
        _logger.Info($"{tempArchiveDir} contents: {string.Join(", ", Directory.GetFiles(tempArchiveDir))}");

        string dnvmTmpPath = Path.Combine(tempArchiveDir, Utilities.DnvmExeName);
        bool success = await ValidateBinary(_logger, dnvmTmpPath);
        if (!success)
        {
            return SelfUpdateFailed;
        }
        var exitCode = RunSelfInstall(_logger, dnvmTmpPath, Utilities.ProcessPath);
        return exitCode == 0 ? Success : SelfUpdateFailed;
    }

    private static async Task<(bool UpdateAvailable, DnvmReleases.Release? Releases)> CheckForSelfUpdates(
        ScopedHttpClient httpClient,
        Logger logger,
        string releasesUrl,
        bool previewsEnabled)
    {
        logger.Log("Checking for updates to dnvm");
        logger.Info("Using dnvm releases URL: " + releasesUrl);

        string releasesJson;
        try
        {
            releasesJson = await httpClient.GetStringAsync(releasesUrl);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error($"Could not fetch releases from URL '{releasesUrl}': {e.Message}");
            return (false, null);
        }

        DnvmReleases releases;
        try
        {
            logger.Info("Releases JSON: " + releasesJson);
            releases = JsonSerializer.Deserialize<DnvmReleases>(releasesJson);
        }
        catch (Exception e)
        {
            logger.Error("Could not deserialize Releases JSON: " + e.Message);
            return (false, null);
        }

        var currentVersion = Program.SemVer;
        var release = releases.LatestVersion;
        var newest = SemVersion.Parse(releases.LatestVersion.Version, SemVersionStyles.Strict);
        var preview = releases.LatestPreview is not null
            ? SemVersion.Parse(releases.LatestPreview.Version, SemVersionStyles.Strict)
            : null;

        if (previewsEnabled && preview is not null && newest.ComparePrecedenceTo(preview) < 0)
        {
            logger.Log($"Preview version '{preview}' is newer than latest version '{newest}'");
            newest = preview;
            release = releases.LatestPreview;
        }

        if (currentVersion.ComparePrecedenceTo(newest) < 0)
        {
            logger.Log("Found newer version: " + newest);
            return (true, release);
        }
        else
        {
            logger.Log("No newer version found. Dnvm is up-to-date.");
            return (false, release);
        }
    }

    private string GetReleaseLink(DnvmReleases.Release release)
    {
        // Dnvm doesn't currently publish ARM64 binaries for any platform
        var rid = (Utilities.CurrentRID with {
            Arch = Architecture.X64
        }).ToString();
        var artifactDownloadLink = release.Artifacts[rid];
        _logger.Info("Artifact download link: " + artifactDownloadLink);
        return artifactDownloadLink;
    }

    private async Task DownloadBinaryToTempAndDelete(string uri, Func<string, Task> action)
    {
        string tempDownloadPath = Path.GetTempFileName();
        using (var tempFile = new FileStream(
            tempDownloadPath,
            FileMode.Open,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024 /* 64kB */,
            FileOptions.WriteThrough))
        {
            // We could have a timeout for downloading the file, but this is heavily dependent
            // on the user's download speed and if they suspend/resume the process. Instead
            // we'll rely on the user to cancel the download if it's taking too long.
            using var archiveHttpStream = await _env.HttpClient.GetStreamAsync(uri);
            await archiveHttpStream.CopyToAsync(tempFile);
            await tempFile.FlushAsync();
        }
        await action(tempDownloadPath);
    }

    public static async Task<bool> ValidateBinary(Logger? logger, string fileName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Utilities.ChmodExec(fileName);
        }

        // Run exe and make sure it's OK
        var testProc = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            ArgumentList = { "--help" },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (testProc is Process ps)
        {
            await testProc.WaitForExitAsync();
            var output = await ps.StandardOutput.ReadToEndAsync();
            string error = await ps.StandardError.ReadToEndAsync();
            const string usageString = "usage: ";
            if (ps.ExitCode != 0)
            {
                logger?.Error($"Could not run downloaded dnvm: {error}");
                return false;
            }
            else if (!output.Contains(usageString))
            {
                logger?.Error($"Downloaded dnvm did not contain \"{usageString}\": ");
                logger?.Log(output);
                return false;
            }
            return true;
        }
        return false;
    }

    public static int RunSelfInstall(Logger logger, string newFileName, string oldPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = newFileName,
            ArgumentList = { "selfinstall", "--update", "--dest-path", $"{oldPath}" }
        };
        if (logger.LogLevel >= LogLevel.Info)
        {
            psi.ArgumentList.Add("--verbose");
        }
        logger.Log("Running selfinstall: " + string.Join(" ", psi.ArgumentList));
        var proc = Process.Start(psi);
        proc!.WaitForExit();
        return proc.ExitCode;
    }
}