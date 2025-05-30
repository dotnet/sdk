
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Semver;
using Serde;
using Serde.Json;
using StaticCs;
using Zio;
using static Dnvm.InstallCommand;
using RollForwardOptions =  Dnvm.GlobalJsonSubset.SdkSubset.RollForwardOptions;

namespace Dnvm;

[GenerateDeserialize]
internal sealed partial record GlobalJsonSubset
{
    public SdkSubset? Sdk { get; init; }

    [GenerateDeserialize]
    public sealed partial record SdkSubset
    {
        [SerdeMemberOptions(DeserializeProxy = typeof(NullableRefProxy.De<SemVersion, SemVersionProxy>))]
        public SemVersion? Version { get; init; }
        public RollForwardOptions? RollForward { get; init; }
        public bool? AllowPrerelease { get; init; }

        [GenerateDeserialize]
        [Closed]
        public enum RollForwardOptions
        {
            /// <summary>
            /// Uses the specified version.
            ///
            /// If not found, rolls forward to the latest patch level.
            ///
            /// If not found, fails.
            /// </summary>
            Patch,
            /// <summary>
            /// Uses the latest patch level for the specified major, minor, and feature band.
            ///
            /// If not found, rolls forward to the next higher feature band within the same
            /// major/minor and uses the latest patch level for that feature band.
            ///
            /// If not found, fails.
            /// </summary>
            Feature,
            /// <summary>
            /// Uses the latest patch level for the specified major, minor, and feature band.
            ///
            /// If not found, rolls forward to the next higher feature band within the same
            /// major/minor version and uses the latest patch level for that feature band.
            ///
            /// If not found, rolls forward to the next higher minor and feature band within the
            /// same major and uses the latest patch level for that feature band.
            ///
            /// If not found, fails.
            /// </summary>
            Minor,
            /// <summary>
            /// Uses the latest patch level for the specified major, minor, and feature band.
            ///
            /// If not found, rolls forward to the next higher feature band within the same
            /// major/minor version and uses the latest patch level for that feature band.
            ///
            /// If not found, rolls forward to the next higher minor and feature band within the
            /// same major and uses the latest patch level for that feature band.
            ///
            /// If not found, rolls forward to the next higher major, minor, and feature band
            /// and uses the latest patch level for that feature band.
            ///
            /// If not found, fails.
            /// </summary>
            Major,
            /// <summary>
            /// Uses the latest installed patch level that matches the requested major, minor,
            /// and feature band with a patch level that's greater than or equal to the
            /// specified value.
            ///
            /// If not found, fails.
            /// </summary>
            LatestPatch,
            /// <summary>
            /// Uses the highest installed feature band and patch level that matches the
            /// requested major and minor with a feature band and patch level that's greater
            /// than or equal to the specified value.
            ///
            /// If not found, fails.
            /// </summary>
            LatestFeature,
            /// <summary>
            /// Uses the highest installed minor, feature band, and patch level that matches the
            /// requested major with a minor, feature band, and patch level that's greater than
            /// or equal to the specified value.
            ///
            /// If not found, fails.
            /// </summary>
            LatestMinor,
            /// <summary>
            /// Uses the highest installed .NET SDK with a version that's greater than or equal
            /// to the specified value.
            ///
            /// If not found, fail.
            /// </summary>
            LatestMajor,
            /// <summary>
            /// Doesn't roll forward. An exact match is required.
            /// </summary>
            Disable,
        }
    }
}

public static partial class RestoreCommand
{
    public enum Error
    {
        NoGlobalJson = 1,
        IoError = 2,
        NoSdkSection = 3,
        NoVersion = 4,
        CouldntFetchReleaseIndex = 5,
        CantFindRequestedVersion = 6,
        ManifestFileCorrupted = 7,
        ManifestIOError = 8,
    }

    public sealed record Options
    {
        public bool Local { get; init; }

        public bool Force { get; init; }

        public bool Verbose { get; init; }
    }

    public static Task<Result<SemVersion, Error>> Run(DnvmEnv env, Logger logger, DnvmSubCommand.RestoreArgs args)
    {
        return Run(env, logger, new Options
        {
            Local = args.Local ?? false,
            Force = args.Force ?? false,
            Verbose = args.Verbose ?? false,
        });
    }

    public static async Task<Result<SemVersion, Error>> Run(DnvmEnv env, Logger logger, Options options)
    {
        UPath? globalJsonPathOpt = null;
        UPath cwd = env.Cwd;
        while (true)
        {
            var testPath = cwd / "global.json";
            if (env.CwdFs.FileExists(testPath))
            {
                globalJsonPathOpt = testPath;
                break;
            }
            if (cwd == UPath.Root)
            {
                break;
            }
            cwd = cwd.GetDirectory();
        }

        if (globalJsonPathOpt is not {} globalJsonPath)
        {
            logger.Error("No global.json found in the current directory or any of its parents.");
            return Error.NoGlobalJson;
        }

        GlobalJsonSubset json;
        try
        {
            var text = env.CwdFs.ReadAllText(globalJsonPath);
            json = JsonSerializer.Deserialize<GlobalJsonSubset>(text);
        }
        catch (IOException e)
        {
            logger.Error("Failed to read global.json: " + e.Message);
            return Error.IoError;
        }

        if (json.Sdk is not {} sdk)
        {
            logger.Error("global.json does not contain an SDK section.");
            return Error.NoSdkSection;
        }

        if (sdk.Version is not {} version)
        {
            logger.Error("SDK section in global.json does not contain a version.");
            return Error.NoVersion;
        }

        var rollForward = sdk.RollForward ?? GlobalJsonSubset.SdkSubset.RollForwardOptions.LatestPatch;
        var allowPrerelease = sdk.AllowPrerelease ?? true;

        DotnetReleasesIndex versionIndex;
        try
        {
            versionIndex = await DotnetReleasesIndex.FetchLatestIndex(env.HttpClient, env.DotnetFeedUrls);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error($"Could not fetch the releases index: {e.Message}");
            return Error.CouldntFetchReleaseIndex;
        }

        // Find the best SDK to match the roll-forward policy.

        var installDir = globalJsonPath.GetDirectory() / ".dotnet";

        var releases = await GetSortedReleases(env.HttpClient, versionIndex, allowPrerelease, sdk.Version.ToMajorMinor());

        ChannelReleaseIndex.Release? Search(Comparison<SemVersion> comparer, bool preferExact)
        {
            if (preferExact && BinarySearchLatest(releases, version, SemVersion.CompareSortOrder) is { } exactMatch)
            {
                return exactMatch;
            }
            return BinarySearchLatest(releases, version, comparer);
        }

        ChannelReleaseIndex.Release? release = rollForward switch
        {
            RollForwardOptions.Patch => Search(LatestPatchComparison, preferExact: true),
            RollForwardOptions.Feature => Search(LatestFeatureComparison, preferExact: true),
            RollForwardOptions.Minor => Search(LatestMinorComparison, preferExact: true),
            RollForwardOptions.Major => Search(LatestMajorComparison, preferExact: true),
            RollForwardOptions.LatestPatch => Search(LatestPatchComparison, preferExact: false),
            RollForwardOptions.LatestFeature => Search(LatestFeatureComparison, preferExact: false),
            RollForwardOptions.LatestMinor => Search(LatestMinorComparison, preferExact: false),
            RollForwardOptions.LatestMajor => Search(LatestMajorComparison, preferExact: false),
            RollForwardOptions.Disable => Search(SemVersion.CompareSortOrder, preferExact: false),
        };

        if (release is null)
        {
            logger.Error("No SDK found that matches the requested version.");
            return Error.CantFindRequestedVersion;
        }

        logger.Log($"Found version {sdk.Version} in global.json. Selected version {release.Sdk.Version} based on roll forward rules.");
        var downloadUrl = release.Sdk.Files.Single(f => f.Rid == Utilities.CurrentRID.ToString() && f.Url.EndsWith(Utilities.ZipSuffix)).Url;

        if (options.Local)
        {
            var error = await InstallCommand.InstallSdkToDir(
                curMuxerVersion: null, // we shouldn't have a muxer yet, and we can overwrite it if we do
                release.Sdk.Version,
                env.HttpClient,
                downloadUrl,
                env.CwdFs,
                installDir,
                env.TempFs,
                logger
            );
            if (error is not null)
            {
                return Error.IoError;
            }
        }
        else
        {
            Manifest manifest;
            try
            {
                manifest = await ManifestUtils.ReadOrCreateManifest(env);
            }
            catch (InvalidDataException)
            {
                logger.Error("Manifest file corrupted");
                return Error.ManifestFileCorrupted;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.Error("Error reading manifest file: " + e.Message);
                return Error.ManifestIOError;
            }

            if (!options.Force && ManifestUtils.IsSdkInstalled(manifest, release.Sdk.Version, manifest.CurrentSdkDir))
            {
                logger.Log($"Version {release.Sdk.Version} is already installed in directory '{manifest.CurrentSdkDir.Name}'." +
                    " Skipping installation. To install anyway, pass --force.");
                return release.Sdk.Version;
            }

            var error = await InstallCommand.InstallSdk(env, manifest, release.Sdk, release, manifest.CurrentSdkDir, logger);
            if (error is not Result<Manifest, InstallError>.Ok)
            {
                return Error.IoError;
            }
        }

        return release.Sdk.Version;
    }

    /// <summary>
    /// Compares equal if the major, minor, and feature band versions are equal, and the patch
    /// version is greater or equal.
    /// </summary>
    private static int LatestPatchComparison(SemVersion a, SemVersion b)
    {
        if (a.Major != b.Major)
        {
            return a.Major.CompareTo(b.Major);
        }
        if (a.Minor != b.Minor)
        {
            return a.Minor.CompareTo(b.Minor);
        }
        // This is where dotnet differs from semver. The semver patch version is the latest
        // number in the version string, but dotnet expects SDKs versions to end in xnn, where
        // x is the feature band and nn is the patch level.
        if ((a.Patch / 100) != (b.Patch / 100))
        {
            return (a.Patch / 100).CompareTo(b.Patch / 100);
        }
        // If the patch version is >= we will consider the versions 'equal', meaning that they are
        // compatible.
        return ((a.Patch % 100) >= (b.Patch % 100)) ? 0 : -1;
    }

    /// <summary>
    /// Compares equal if the major and minor versions are equal, and the feature band and patch
    /// version are greater or equal.
    /// </summary>
    private static int LatestFeatureComparison(SemVersion a, SemVersion b)
    {
        if (a.Major != b.Major)
        {
            return a.Major.CompareTo(b.Major);
        }
        if (a.Minor != b.Minor)
        {
            return a.Minor.CompareTo(b.Minor);
        }
        if (a.Patch != b.Patch)
        {
            return a.Patch >= b.Patch ? 0 : -1;
        }
        return 0;
    }

    /// <summary>
    /// Compares equal if the major versions are equal, and the minor versions are greater or equal.
    /// </summary>
    private static int LatestMinorComparison(SemVersion a, SemVersion b)
    {
        if (a.Major != b.Major)
        {
            return a.Major.CompareTo(b.Major);
        }
        if (a.Minor != b.Minor)
        {
            return a.Minor >= b.Minor ? 0 : -1;
        }
        if (a.Patch != b.Patch)
        {
            return a.Patch >= b.Patch ? 0 : -1;
        }
        return 0;
    }

    /// <summary>
    /// Compares equal if the major versions are greater than or equal.
    /// </summary>
    private static int LatestMajorComparison(SemVersion a, SemVersion b)
    {
        if (a.Major != b.Major)
        {
            return a.Major >= b.Major ? 0 : -1;
        }
        if (a.Minor != b.Minor)
        {
            return a.Minor >= b.Minor ? 0 : -1;
        }
        if (a.Patch != b.Patch)
        {
            return a.Patch >= b.Patch ? 0 : -1;
        }
        return 0;
    }

    /// <summary>
    /// Finds the release with the given SDK version. If multiple releases have the same SDK version,
    /// the one with the newest SDK version is returned.
    /// </summary>
    /// <param name="releases">A list of the releases in descending sort order.</param>
    /// <param name="version">Version of the SDK to find.</param>
    /// <param name="comparer">Custom comparison for versions.</param>
    /// <returns>The SDK with the newest matching version, or null if no matching version is found.</returns>
    private static ChannelReleaseIndex.Release? BinarySearchLatest(
        List<ChannelReleaseIndex.Release> releases,
        SemVersion version,
        Comparison<SemVersion> comparer)
    {
        // Note: the list is sorted in descending order.
        int left = 0;
        int right = releases.Count;
        while (left < right)
        {
            int mid = left + (right - left) / 2;
            if (comparer(releases[mid].Sdk.Version, version) > 0)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }
        return left < releases.Count && comparer(releases[left].Sdk.Version, version) == 0 ? releases[left] : null;
    }

    /// <summary>
    /// Returns list of releases sorted (descending) by SDK version. If <paramref name="minMajorMinor"/> is not
    /// null, only releases for that major+minor version are returned.
    /// </summary>
    private static async Task<List<ChannelReleaseIndex.Release>> GetSortedReleases(
        ScopedHttpClient httpClient,
        DotnetReleasesIndex versionIndex,
        bool allowPrerelease,
        string minMajorMinor)
    {
        var releases = new List<ChannelReleaseIndex.Release>();
        foreach (var releaseIndex in versionIndex.ChannelIndices)
        {
            if (minMajorMinor is not null && double.Parse(minMajorMinor) > double.Parse(releaseIndex.MajorMinorVersion))
            {
                continue;
            }
            var index = JsonSerializer.Deserialize<ChannelReleaseIndex>(await httpClient.GetStringAsync(releaseIndex.ChannelReleaseIndexUrl));
            foreach (var release in index.Releases)
            {
                foreach (var sdk in release.Sdks)
                {
                    if (allowPrerelease || !sdk.Version.IsPrerelease)
                    {
                        releases.Add(release);
                    }
                }
            }
        }
        // Sort in descending order.
        releases.Sort((a, b) => b.Sdk.Version.CompareSortOrderTo(a.Sdk.Version));
        return releases;
    }
}