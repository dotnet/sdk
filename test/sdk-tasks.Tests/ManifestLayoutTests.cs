// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Versioning;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Tests that manifest builds copy the expected files, remove obsolete files, recover after
/// interrupted builds, and skip work when nothing has changed.
/// </summary>
[TestClass]
// In-process MSBuild shares process-global build state and the current directory with legacy layout tests.
[DoNotParallelize]
public class ManifestLayoutTests : SdkTest
{
    /// <summary>
    /// Verifies downloaded and built-in payloads share one layout whose unchanged rerun skips
    /// without rewriting files or enabling baselines.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsCopiesBothFamiliesAndSkipsWarmBuild()
    {
        ManifestProject project = CreateManifestProject();

        BuildObservation cold = project.Build();
        cold.RanLayout.Should().BeTrue();

        foreach (bool builtin in new[] { false, true })
        {
            foreach (string relativePath in PayloadFiles)
            {
                File.ReadAllText(project.Output(builtin, relativePath))
                    .Should().Be(File.ReadAllText(project.Source(builtin, relativePath)));
            }
        }

        Directory.GetFiles(project.LayoutRoot, "*", SearchOption.AllDirectories).Should().HaveCount(8);
        Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);

        BuildObservation warm = project.Build();
        warm.RanLayout.Should().BeFalse();

        AssertTimestamps(timestamps);
        Directory.GetFiles(project.LayoutRoot, "baseline.workloadset.json", SearchOption.AllDirectories).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies removing a localization glob member invalidates real NuGet packing even when
    /// generated files remain unchanged, and that subsequent packing and layout skip as up-to-date.
    /// </summary>
    /// <remarks>
    /// Retains the producer's NoTargets SDK and Pack graph with controlled outer build scaffolding.
    /// Checks package membership and decoded text against returned mappings and staged payloads.
    /// </remarks>
    [TestMethod]
    public void LayoutManifestsRepacksAfterRemovingLocalization()
    {
        ManifestProject project = CreateManifestProject();
        const string ProducerName = "Microsoft.NET.Workload.Mono.Toolchain.Current.Manifest";
        string generatedDirectory = project.UseBuiltinProducer(ProducerName);
        string producerDirectory = Path.Combine(project.Root, "repo", "src", "Workloads", "Manifests", ProducerName);
        string producerPath = Path.Combine(producerDirectory, ProducerName + ".proj");
        string removed = Directory.GetFiles(Path.Combine(producerDirectory, "localize"), "*.json").First();
        string removedRelativePath = Path.Combine("localize", Path.GetFileName(removed));
        string package = Path.Combine(project.Root, "nupkgs", $"{ProducerName}.{project.Builtin.Version}.nupkg");
        File.Exists(package).Should().BeFalse();

        Build("restore", producerPath, "Restore");
        Build("cold");
        AssertPackageMatchesLayout();
        string[] generatedFiles = Directory.GetFiles(generatedDirectory, "WorkloadManifest.*");
        generatedFiles.Should().HaveCount(3);
        Dictionary<string, DateTime> generatedTimestamps = generatedFiles.ToDictionary(path => path, File.GetLastWriteTimeUtc);
        AssertWarm("warm");

        File.Delete(removed);
        WaitForUtcNowToAdvance();
        Build("removal");
        AssertPackageMatchesLayout();
        File.Exists(project.Output(true, removedRelativePath)).Should().BeFalse();
        AssertTimestamps(generatedTimestamps);
        AssertWarm("removal-warm");

        void Build(string name, string? path = null, string target = "LayoutManifests", bool warm = false)
        {
            if (target != "Restore")
            {
                project.SyncDownloadedPackage();
            }

            string binlog = Path.Combine(project.Root, name + ".binlog");
            new DotnetCommand(Log, "msbuild", path ?? Path.Combine(project.Root, "manifest-layout.proj"),
                    $"/t:{target}", "/nr:false", $"/bl:{binlog}")
                .WithWorkingDirectory(project.Root)
                .WithEnvironmentVariable("NUGET_PACKAGES", Path.Combine(project.Root, "packages"))
                .Execute()
                .Should().Pass();
            if (target == "Restore")
            {
                return;
            }

            var logger = new LayoutLogger();
            var replay = new BinaryLogReplayEventSource();
            logger.Initialize(replay);
            replay.Replay(binlog, TestContext.CancellationToken);

            if (warm)
            {
                logger.SkippedTargets.Should().Contain(["GenerateNuspec", "LayoutManifests"]);
            }
            else
            {
                logger.StartedTargets.Should().Contain(["GenerateNuspec", "LayoutManifests"]);
                logger.SkippedTargets.Should().NotContain(["GenerateNuspec", "LayoutManifests"]);
            }

            logger.ExecutedTasks.Contains("PackTask").Should().Be(!warm);
        }

        void AssertWarm(string name)
        {
            Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);
            timestamps.Add(package, File.GetLastWriteTimeUtc(package));
            WaitForUtcNowToAdvance();
            Build(name, warm: true);
            AssertTimestamps(timestamps);
            AssertTimestamps(generatedTimestamps);
        }

        void AssertPackageMatchesLayout()
        {
            using ZipArchive archive = ZipFile.OpenRead(package);
            Dictionary<string, ZipArchiveEntry> entries = archive.Entries
                .Where(entry => entry.FullName.StartsWith("data/", StringComparison.Ordinal))
                .ToDictionary(entry => entry.FullName["data/".Length..]);
            string[][] mappings = File.ReadAllLines(Path.Combine(project.Root, "builtin-mappings.txt"))
                .Select(line => line.Split('|')).ToArray();
            string layoutDirectory = Path.GetDirectoryName(project.Output(true, "WorkloadManifest.json"))!;
            entries.Keys.Should().BeEquivalentTo(mappings.Select(mapping =>
                Path.GetRelativePath(layoutDirectory, mapping[1]).Replace('\\', '/')));
            entries.Keys.Should().BeEquivalentTo(Directory.GetFiles(layoutDirectory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(layoutDirectory, path).Replace('\\', '/')));
            foreach (string[] mapping in mappings)
            {
                string relativePath = Path.GetRelativePath(layoutDirectory, mapping[1]).Replace('\\', '/');
                using var reader = new StreamReader(entries[relativePath].Open());
                reader.ReadToEnd().Should().Be(File.ReadAllText(mapping[0]));
                File.ReadAllText(mapping[1]).Should().Be(File.ReadAllText(mapping[0]));
            }
        }
    }

    /// <summary>
    /// Verifies content edits, reverts, additions, renames, and removals reconcile one producer's
    /// payload without rewriting the other producer's manifest.
    /// </summary>
    /// <param name="builtin">Whether to change built-in rather than downloaded inputs.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LayoutManifestsTracksEditsRevertsAndPayloadMembership(bool builtin)
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        string source = project.Source(builtin, "WorkloadManifest.json");
        string original = File.ReadAllText(source);
        DateTime otherFamilyTimestamp = File.GetLastWriteTimeUtc(project.Output(!builtin, "WorkloadManifest.json"));

        WriteNewer(source, "changed manifest", project.CompletionFile);
        project.Build().RanLayout.Should().BeTrue();
        File.ReadAllText(project.Output(builtin, "WorkloadManifest.json")).Should().Be("changed manifest");

        WriteNewer(source, original, project.CompletionFile);
        project.Build().RanLayout.Should().BeTrue();
        File.ReadAllText(project.Output(builtin, "WorkloadManifest.json")).Should().Be(original);

        foreach (string relativePath in new[] { "added.dat", Path.Combine("localize", "added.fr.json") })
        {
            string addedSource = WriteFile(project.Source(builtin, relativePath), "added");
            project.Build().RanLayout.Should().BeTrue();
            File.ReadAllText(project.Output(builtin, relativePath)).Should().Be("added");

            string renamedRelativePath = Path.Combine(Path.GetDirectoryName(relativePath)!, "renamed.dat");
            File.Move(addedSource, project.Source(builtin, renamedRelativePath));
            project.Build().RanLayout.Should().BeTrue();
            File.Exists(project.Output(builtin, relativePath)).Should().BeFalse();
            File.ReadAllText(project.Output(builtin, renamedRelativePath)).Should().Be("added");

            File.Delete(project.Source(builtin, renamedRelativePath));
            project.Build().RanLayout.Should().BeTrue();
            File.Exists(project.Output(builtin, renamedRelativePath)).Should().BeFalse();
        }

        File.GetLastWriteTimeUtc(project.Output(!builtin, "WorkloadManifest.json")).Should().Be(otherFamilyTimestamp);
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies missing manifest and localization outputs force repair despite an existing
    /// completion stamp, after which an unchanged rerun skips mutation.
    /// </summary>
    /// <param name="builtin">Whether to remove built-in rather than downloaded outputs.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LayoutManifestsRepairsMissingOutputs(bool builtin)
    {
        ManifestProject project = CreateManifestProject();
        project.Build();

        foreach (string relativePath in new[] { "WorkloadManifest.json", Path.Combine("localize", "strings.fr.json") })
        {
            File.Delete(project.Output(builtin, relativePath));
            project.Build().RanLayout.Should().BeTrue();
            File.ReadAllText(project.Output(builtin, relativePath))
                .Should().Be(File.ReadAllText(project.Source(builtin, relativePath)));
            project.Build().RanLayout.Should().BeFalse();
        }
    }

    /// <summary>
    /// Verifies version and feature-band remapping, including prerelease paths, removes the
    /// former payload directory while preserving the other producer's manifest.
    /// </summary>
    /// <param name="builtin">Whether to remap the built-in rather than downloaded manifest.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LayoutManifestsTracksVersionFeatureBandAndPrereleaseRemaps(bool builtin)
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        ManifestDefinition definition = builtin ? project.Builtin : project.Downloaded;

        foreach ((string featureBand, string version) in new[]
        {
            ("11.0.100", "11.0.2"),
            ("11.0.200", "11.0.2"),
            ("11.0.200-preview.2", "11.0.3-preview.1")
        })
        {
            string oldDirectory = Path.GetDirectoryName(project.Output(builtin, "WorkloadManifest.json"))!;
            definition.FeatureBand = featureBand;
            definition.Version = version;
            project.WriteInputs();

            project.Build().RanLayout.Should().BeTrue();

            Directory.Exists(oldDirectory).Should().BeFalse();
            foreach (string relativePath in PayloadFiles)
            {
                File.ReadAllText(project.Output(builtin, relativePath))
                    .Should().Be(File.ReadAllText(project.Source(builtin, relativePath)));
            }
            File.Exists(project.Output(!builtin, "WorkloadManifest.json")).Should().BeTrue();
            project.Build().RanLayout.Should().BeFalse();
        }
    }

    /// <summary>
    /// Verifies valid version spellings that NuGet normalizes still produce owned layout outputs.
    /// </summary>
    [TestMethod]
    [DataRow("11.0.100.0")]
    [DataRow("11.0.100+build.7")]
    [DataRow("11.0.100.0+build.7")]
    public void LayoutManifestsAcceptsNonNormalizedVersions(string version)
    {
        ManifestProject project = CreateManifestProject();
        project.Downloaded.Version = version;
        project.WriteInputs();

        project.Build().RanLayout.Should().BeTrue();
        File.Exists(project.Output(false, "WorkloadManifest.json")).Should().BeTrue();

        string stale = WriteFile(project.Output(false, "stale.json"), "stale");
        project.Build().RanLayout.Should().BeTrue();
        File.Exists(stale).Should().BeFalse();
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies a first invocation without layout state removes retired owned payloads and
    /// baselines while preserving malformed paths, unrelated workload sets, and outside files.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsCleansRetiredPayloadsWithoutCachesAndPreservesUnownedFiles()
    {
        ManifestProject project = CreateManifestProject();
        string[] retiredDirectories =
        [
            Path.Combine("9.0.100", "retired.manifest", "9.0.1"),
            Path.Combine("11.0.100", "retired.manifest", "11.0.1"),
            Path.Combine("11.0.100", project.Downloaded.Id.ToLowerInvariant(), "10.0.1"),
            Path.Combine("11.0.100-preview.1", "retired.manifest", "11.0.1-preview.1"),
            Path.Combine("11.0.100", "any manifest id", "11.0.1")
        ];
        foreach (string directory in retiredDirectories)
        {
            WriteFile(Path.Combine(project.LayoutRoot, directory, "arbitrary.bin"), "owned");
            WriteFile(Path.Combine(project.LayoutRoot, directory, "nested", "auxiliary.txt"), "owned");
            Directory.CreateDirectory(Path.Combine(project.LayoutRoot, directory, "empty", "nested"));
        }

        string retiredBaseline = WriteFile(
            Path.Combine(project.LayoutRoot, "10.0.100", "workloadsets", "10.0.100-baseline", "baseline.workloadset.json"),
            "owned");
        string[] unownedPaths =
        [
            Path.Combine("notes.txt"),
            Path.Combine("11.0.100", "band.txt"),
            Path.Combine("11.0.100", "retired.manifest", "id.txt"),
            Path.Combine("not-a-band", "retired.manifest", "11.0.1", "keep.txt"),
            Path.Combine("11.0.100", "retired.manifest", "not-a-version", "keep.txt"),
            Path.Combine("11.0.100", "workloadsets", "11.0.100", "other.workloadset.json"),
            Path.Combine("11.0.100", "workloadsets", "11.0.100", "arbitrary.bin"),
            Path.Combine("11.0.100", "workloadsets", "11.0.100", "nested", "baseline.workloadset.json"),
            Path.Combine("11.0.100", "workloadsets", "not-a-version", "baseline.workloadset.json"),
            Path.Combine("11.0.100", "workloadsets", "baseline.workloadset.json")
        ];
        string[] preserved = unownedPaths.Select(path => WriteFile(Path.Combine(project.LayoutRoot, path), "unowned")).ToArray();
        string outside = WriteFile(Path.Combine(project.Root, "sdk", "outside-manifests.txt"), "unowned");
        Directory.CreateDirectory(Path.Combine(project.LayoutRoot, "malformed-empty-band"));
        project.StateFiles.Should().OnlyContain(path => !File.Exists(path));

        project.Build().RanLayout.Should().BeTrue();

        foreach (string directory in retiredDirectories)
        {
            Directory.Exists(Path.Combine(project.LayoutRoot, directory)).Should().BeFalse();
        }
        Directory.Exists(Path.Combine(project.LayoutRoot, "9.0.100")).Should().BeFalse();
        File.Exists(retiredBaseline).Should().BeFalse();
        foreach (string path in preserved.Append(outside))
        {
            File.ReadAllText(path).Should().Be("unowned");
        }
        Directory.Exists(Path.Combine(project.LayoutRoot, "malformed-empty-band")).Should().BeTrue();
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an extra owned file invalidates a completed layout without requiring input
    /// changes or rewriting retained payloads and inventories.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsDetectsStaleOutputsOnWarmBuild()
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);
        timestamps.Remove(project.CompletionFile);
        string stale = WriteFile(project.Output(false, "retired.json"), "stale");

        project.Build().RanLayout.Should().BeTrue();

        File.Exists(stale).Should().BeFalse();
        AssertTimestamps(timestamps);
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an empty owned directory makes a completed layout rerun and is removed, while an
    /// empty directory the layout doesn't own is left alone.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsRemovesEmptyOwnedDirectoriesOnWarmBuild()
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        string ownedBand = Path.Combine(project.LayoutRoot, "10.0.100");
        string ownedNested = Path.Combine(project.LayoutRoot, "11.0.100", project.Downloaded.Id.ToLowerInvariant(), "10.0.1", "localize");
        string unowned = Path.Combine(project.LayoutRoot, "11.0.100", project.Downloaded.Id.ToLowerInvariant(), "not-a-version");
        Directory.CreateDirectory(ownedBand);
        Directory.CreateDirectory(ownedNested);
        Directory.CreateDirectory(unowned);

        project.Build().RanLayout.Should().BeTrue();

        Directory.Exists(ownedBand).Should().BeFalse();
        Directory.Exists(Path.GetDirectoryName(ownedNested)).Should().BeFalse();
        Directory.Exists(unowned).Should().BeTrue();
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies renames that change only letter case produce the same names as a clean build, for
    /// both a payload file and a manifest version directory, even on case-insensitive file systems.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsAppliesCaseOnlyRenames()
    {
        ManifestProject project = CreateManifestProject();
        project.Downloaded.Version = "11.0.1-preview.1";
        project.WriteInputs();
        project.Build();
        string oldSource = project.Source(false, Path.Combine("localize", "strings.fr.json"));
        File.Move(oldSource, oldSource + ".tmp");
        File.Move(oldSource + ".tmp", project.Source(false, Path.Combine("localize", "strings.FR.json")));
        project.Downloaded.Version = "11.0.1-Preview.1";
        project.WriteInputs();

        project.Build().RanLayout.Should().BeTrue();

        string manifestDirectory = Path.Combine(project.LayoutRoot, "11.0.100", project.Downloaded.Id.ToLowerInvariant());
        Directory.GetDirectories(manifestDirectory).Select(Path.GetFileName).Should().Equal("11.0.1-Preview.1");
        Directory.GetFiles(Path.Combine(manifestDirectory, "11.0.1-Preview.1", "localize")).Select(Path.GetFileName)
            .Should().Equal("strings.FR.json");
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that when a feature band folder's name changes only in case and the folder also
    /// holds a file the layout doesn't own, the layout keeps that file and skips the next build.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsIgnoresFeatureBandCaseChangeWithUnownedFile()
    {
        ManifestProject project = CreateManifestProject();
        project.Downloaded.FeatureBand = "11.0.100-preview.1";
        project.WriteInputs();
        project.Build();
        string unownedFile = Path.Combine(project.LayoutRoot, "11.0.100-preview.1", "band.txt");
        WriteFile(unownedFile, "unowned");
        project.Downloaded.FeatureBand = "11.0.100-Preview.1";
        project.WriteInputs();

        project.Build().RanLayout.Should().BeTrue();

        File.Exists(unownedFile).Should().BeTrue();
        File.Exists(project.Output(false, "WorkloadManifest.json")).Should().BeTrue();
        Directory.GetFiles(project.LayoutRoot, "WorkloadManifest.json", SearchOption.AllDirectories).Should().HaveCount(2);
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies disabling producers removes their former payloads and allows a fully empty
    /// expected layout to complete and skip on the next invocation.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsCleansRemovedManifestsAndEmptyProducerSets()
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        string downloadedDirectory = Path.GetDirectoryName(project.Output(false, "WorkloadManifest.json"))!;
        string builtinDirectory = Path.GetDirectoryName(project.Output(true, "WorkloadManifest.json"))!;

        project.Downloaded.Enabled = false;
        project.WriteInputs();
        project.Build().RanLayout.Should().BeTrue();
        Directory.Exists(downloadedDirectory).Should().BeFalse();
        File.Exists(project.Output(true, "WorkloadManifest.json")).Should().BeTrue();

        project.Builtin.Enabled = false;
        project.WriteInputs();
        project.Build().RanLayout.Should().BeTrue();
        Directory.Exists(builtinDirectory).Should().BeFalse();
        File.Exists(project.CompletionFile).Should().BeTrue();
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies missing or corrupt inventories and completion markers are rebuilt from current
    /// inputs without treating a path injected into cached state as deletion authority.
    /// </summary>
    /// <param name="extension">The input inventory or completion marker to damage.</param>
    /// <param name="corrupt">Whether to replace the state contents rather than delete the file.</param>
    [TestMethod]
    [DataRow("inputs", false)]
    [DataRow("inputs", true)]
    [DataRow("complete", false)]
    [DataRow("complete", true)]
    public void LayoutManifestsReconstructsDeletedOrCorruptState(string extension, bool corrupt)
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        Dictionary<string, string> expectedState = project.StateFiles.ToDictionary(path => path, File.ReadAllText);
        string unowned = WriteFile(Path.Combine(project.LayoutRoot, "keep.txt"), "unowned");
        string stateFile = Path.ChangeExtension(project.CompletionFile, extension);
        if (corrupt)
        {
            WriteNewer(stateFile, unowned, project.CompletionFile);
        }
        else
        {
            File.Delete(stateFile);
        }
        project.Build().RanLayout.Should().BeTrue();

        foreach ((string path, string contents) in expectedState)
        {
            File.ReadAllText(path).Should().Be(contents);
        }
        File.ReadAllText(unowned).Should().Be("unowned");
        Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);
        project.Build().RanLayout.Should().BeFalse();
        AssertTimestamps(timestamps);
    }

    /// <summary>
    /// Verifies baselines remain opt-in, track manifest and baseline-version changes, and are
    /// removed when disabled without deleting unrelated workload sets.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsTracksBaselineEnableUpdatesAndDisable()
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        File.Exists(project.BaselineFile).Should().BeFalse();
        var enabled = new Dictionary<string, string> { ["GenerateBaselineWorkloadSet"] = "true" };

        project.Build(enabled).RanLayout.Should().BeTrue();
        string baseline = File.ReadAllText(project.BaselineFile);
        baseline.Should().Contain(project.Downloaded.Id);
        baseline.Should().Contain($"{project.Downloaded.Version}/{project.Downloaded.FeatureBand}");
        Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);
        project.Build(enabled, target: "LayoutBaselineWorkloadSet").RanLayout.Should().BeFalse();
        AssertTimestamps(timestamps);

        project.Downloaded.Version = "11.0.2";
        project.WriteInputs();
        project.Build(enabled).RanLayout.Should().BeTrue();
        File.ReadAllText(project.BaselineFile).Should().NotBe(baseline);
        File.ReadAllText(project.BaselineFile).Should().Contain("11.0.2/11.0.100");

        enabled["VersionPrefix"] = "11.0.200";
        project.Build(enabled).RanLayout.Should().BeTrue();
        string updatedBaseline = Path.Combine(project.LayoutRoot, "11.0.200-baseline", "workloadsets", "11.0.200-baseline", "baseline.workloadset.json");
        File.Exists(project.BaselineFile).Should().BeFalse();
        File.ReadAllText(updatedBaseline).Should().Contain("11.0.2/11.0.100");

        string otherWorkloadSet = WriteFile(Path.Combine(Path.GetDirectoryName(updatedBaseline)!, "other.workloadset.json"), "keep");
        project.Build().RanLayout.Should().BeTrue();
        File.Exists(project.BaselineFile).Should().BeFalse();
        File.Exists(updatedBaseline).Should().BeFalse();
        File.ReadAllText(otherWorkloadSet).Should().Be("keep");
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies missing required inputs fail without removing stale or retained payloads.
    /// </summary>
    /// <param name="builtin">Whether the missing input belongs to the built-in producer.</param>
    /// <param name="staleOutput">Whether to include an unrelated stale owned output before failure.</param>
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void LayoutManifestsRejectsMissingRequiredSourcesBeforeCleanup(bool builtin, bool staleOutput)
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        string stale = Path.Combine(project.LayoutRoot, "8.0.100", "retired.manifest", "8.0.1", "stale.bin");
        if (staleOutput)
        {
            WriteFile(stale, "stale");
        }
        string output = project.Output(builtin, "WorkloadManifest.json");
        string contents = File.ReadAllText(output);
        File.Delete(project.Source(builtin, "WorkloadManifest.json"));
        Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);
        timestamps.Remove(project.CompletionFile);

        project.Build(expectedSuccess: false).RanLayout.Should().Be(builtin);

        if (staleOutput)
        {
            File.ReadAllText(stale).Should().Be("stale");
        }

        File.ReadAllText(output).Should().Be(contents);
        AssertTimestamps(timestamps);
    }

    /// <summary>
    /// Verifies colliding destinations across producers fail before payload cleanup and cannot
    /// appear complete on a repeated invocation after inventories have been rewritten.
    /// </summary>
    [TestMethod]
    public void LayoutManifestsRejectsCrossProducerDestinationCollisionsBeforeCleanup()
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        string stale = WriteFile(Path.Combine(project.LayoutRoot, "8.0.100", "retired.manifest", "8.0.1", "stale.bin"), "stale");
        string output = project.Output(false, "WorkloadManifest.json");
        string contents = File.ReadAllText(output);
        project.Builtin.Id = project.Downloaded.Id.ToLowerInvariant();
        project.Builtin.FeatureBand = project.Downloaded.FeatureBand;
        project.Builtin.Version = project.Downloaded.Version;
        project.WriteInputs();
        Dictionary<string, DateTime> timestamps = SnapshotTimestamps(project);
        foreach (string stateFile in project.StateFiles)
        {
            timestamps.Remove(stateFile);
        }

        project.Build(expectedSuccess: false).RanLayout.Should().BeTrue();
        project.Build(expectedSuccess: false).RanLayout.Should().BeTrue();

        File.ReadAllText(stale).Should().Be("stale");
        File.ReadAllText(output).Should().Be(contents);
        File.Exists(project.CompletionFile).Should().BeFalse();
        AssertTimestamps(timestamps);
    }

    /// <summary>
    /// Verifies a failure after cleanup or copying leaves no completion stamp and that the next
    /// invocation repairs the partial layout before returning to unchanged-build skipping.
    /// </summary>
    /// <param name="interruptionTask">The mutation task immediately before which to inject failure.</param>
    [TestMethod]
    [DataRow("Copy")]
    [DataRow("CompleteIncrementalLayout")]
    public void LayoutManifestsRecoversFromInterruptedMutation(string interruptionTask)
    {
        ManifestProject project = CreateManifestProject();
        project.Build();
        string source = project.Source(false, "WorkloadManifest.json");
        string original = File.ReadAllText(source);
        WriteNewer(source, "changed before interruption", project.CompletionFile);
        string stale = WriteFile(Path.Combine(project.LayoutRoot, "8.0.100", "retired.manifest", "8.0.1", "stale.bin"), "stale");

        BuildObservation interrupted = project.Build(expectedSuccess: false, interruptionTask: interruptionTask);

        interrupted.RanLayout.Should().BeTrue();
        interrupted.Log.Should().Contain("Injected manifest layout interruption");
        File.Exists(stale).Should().BeFalse();
        File.Exists(project.CompletionFile).Should().BeFalse();
        File.ReadAllText(project.Output(false, "WorkloadManifest.json"))
            .Should().Be(interruptionTask == "Copy" ? original : "changed before interruption");

        project.Build().RanLayout.Should().BeTrue();
        File.ReadAllText(project.Output(false, "WorkloadManifest.json")).Should().Be("changed before interruption");
        File.Exists(project.CompletionFile).Should().BeTrue();
        project.Build().RanLayout.Should().BeFalse();
    }

    /// <summary>
    /// Verifies mapping targets return Pack-produced files without staging them and preserve
    /// shipping filters and destination conventions.
    /// </summary>
    /// <param name="traversal">
    /// Whether to exercise aggregate traversal and its version override rather than the direct producer.
    /// </param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BuiltinManifestMappingTargetsReturnPackOutputs(bool traversal)
    {
        string root = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path;
        string manifestsDirectory = Path.Combine(root, "src", "Workloads", "Manifests");
        Directory.CreateDirectory(manifestsDirectory);
        WriteFile(Path.Combine(root, "Directory.Build.targets"), "<Project />");
        string targets = Path.Combine(manifestsDirectory, "Directory.Build.targets");
        File.Copy(GetWorkloadFilePath("Directory.Build.targets"), targets);
        string producer = CreateBuiltinProducer(manifestsDirectory, "Test.MixedCase.Manifest", shipping: true);
        CreateBuiltinProducer(manifestsDirectory, "Test.NonShipping.Manifest", shipping: false);
        string projectPath = producer;
        string outputRoot = Path.Combine(root, "sdk-manifests");
        string version = traversal ? "11.0.9" : "11.0.1";
        var properties = new Dictionary<string, string>
        {
            ["ManifestDirectory"] = outputRoot + Path.DirectorySeparatorChar,
            ["DotNet1xxWorkloadManifestVersion"] = "11.0.9"
        };
        if (traversal)
        {
            // The traversal SDK supplies build orchestration, not the mapping targets under test.
            // Remove only that SDK import from the copied project so this fixture needs no restore.
            using var collection = new ProjectCollection();
            ProjectRootElement traversalProject = ProjectRootElement.Open(GetWorkloadFilePath("manifest-packages.csproj"), collection);
            traversalProject.Sdk = string.Empty;
            string traversalPath = Path.Combine(manifestsDirectory, "manifest-packages.csproj");
            traversalProject.Save(traversalPath);
            projectPath = WriteFile(
                Path.Combine(manifestsDirectory, "traversal.proj"),
                $"""
                <Project>
                  {UsingTasks}
                  <Import Project="{Escape(traversalPath)}" />
                  <ItemGroup>
                    <SyntheticProjectReference Include="@(ProjectReference->'%(FullPath)')" />
                    <ProjectReference Remove="@(ProjectReference)" />
                    <ProjectReference Include="@(SyntheticProjectReference)" />
                  </ItemGroup>
                </Project>
                """);
        }

        BuildObservation mapping = BuildProject(
            projectPath,
            traversal ? "GetBuiltinManifestLayoutInputs" : "GetManifestLayoutInputs",
            properties);

        string destination = Path.Combine(outputRoot, "11.0.100-preview.2", "test.mixedcase", version);
        mapping.Outputs.Should().HaveCount(4);
        mapping.Outputs.Select(item => item.GetMetadata("DestinationPath"))
            .Should().BeEquivalentTo(PayloadFiles.Select(path => Path.Combine(destination, path)));
        mapping.Outputs.Should().OnlyContain(item => Path.IsPathFullyQualified(item.ItemSpec) && File.Exists(item.ItemSpec));
        Directory.Exists(outputRoot).Should().BeFalse("collecting mappings must not copy into the layout");
        File.Exists(Path.Combine(Path.GetDirectoryName(producer)!, "pack.marker")).Should().BeTrue();

        BuildObservation nonShipping = BuildProject(
            Path.Combine(manifestsDirectory, "Test.NonShipping.Manifest", "Test.NonShipping.Manifest.proj"),
            "GetManifestLayoutInputs",
            properties);
        nonShipping.Outputs.Should().BeEmpty();
    }

    private static string[] PayloadFiles =>
        ["WorkloadManifest.json", "WorkloadManifest.targets", "auxiliary.bin", Path.Combine("localize", "strings.fr.json")];

    private ManifestProject CreateManifestProject([CallerMemberName] string testName = "") =>
        new(TestAssetsManager.CreateTestDirectory(testName, identifier: Guid.NewGuid().ToString("N")).Path);

    private static Dictionary<string, DateTime> SnapshotTimestamps(ManifestProject project) =>
        Directory.GetFiles(project.LayoutRoot, "*", SearchOption.AllDirectories)
            .Concat(project.StateFiles)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);

    private static void AssertTimestamps(Dictionary<string, DateTime> timestamps)
    {
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp, "'{0}' should not be rewritten", path);
        }
    }

    /// <summary>
    /// Writes an input with a timestamp strictly newer than the completion stamp so timestamp
    /// granularity cannot hide the content change from MSBuild.
    /// </summary>
    private static void WriteNewer(string path, string contents, string completionFile)
    {
        DateTime completionTimestamp = File.GetLastWriteTimeUtc(completionFile);
        WriteFile(path, contents);
        while (File.GetLastWriteTimeUtc(path) <= completionTimestamp)
        {
            WaitForUtcNowToAdvance();
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    private static string WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string Escape(string value) => SecurityElement.Escape(value)!;

    private static string UsingTasks
    {
        get
        {
            string sdkTasks = typeof(PrepareIncrementalLayout).Assembly.Location;
            string buildTasks = Path.Combine(Path.GetDirectoryName(typeof(Project).Assembly.Location)!, "Microsoft.Build.Tasks.Core.dll");
            string[] sdkTaskNames =
                ["PrepareIncrementalLayout", "CompleteIncrementalLayout", "GetWorkloadManifestLayout", "PruneEmptyLayoutDirectories", "GetWorkloadSetFeatureBand", "ResolveBundledManifestPackages"];
            string[] buildTaskNames = ["Copy", "Delete", "Error", "MSBuild", "ReadLinesFromFile", "WriteLinesToFile"];
            return string.Join(
                Environment.NewLine,
                sdkTaskNames.Select(name => $"<UsingTask TaskName=\"Microsoft.DotNet.Build.Tasks.{name}\" AssemblyFile=\"{Escape(sdkTasks)}\" />")
                    .Concat(buildTaskNames.Select(name => $"<UsingTask TaskName=\"Microsoft.Build.Tasks.{name}\" AssemblyFile=\"{Escape(buildTasks)}\" />")));
        }
    }

    private static string GetWorkloadFilePath(string fileName) =>
        LayoutFiles.GetPath(
            Path.Combine("Workloads", "Manifests", fileName),
            Path.Combine("src", "Workloads", "Manifests", fileName));

    /// <summary>
    /// Evaluates and builds a target with a fresh project collection and build manager, retaining
    /// task execution, returned items, and diagnostics for behavioral assertions.
    /// </summary>
    private static BuildObservation BuildProject(
        string projectPath,
        string target,
        Dictionary<string, string>? globalProperties = null,
        bool expectedSuccess = true,
        Action<ProjectCollection>? configure = null)
    {
        using var collection = new ProjectCollection(globalProperties ?? []);
        configure?.Invoke(collection);
        Project project = collection.LoadProject(projectPath);
        var logger = new LayoutLogger();
        using var manager = new BuildManager();
        BuildResult result = manager.Build(
            new BuildParameters(collection) { Loggers = [logger] },
            new BuildRequestData(project.CreateProjectInstance(), [target]));
        (result.OverallResult == BuildResultCode.Success).Should().Be(expectedSuccess, "MSBuild log:{0}{1}", Environment.NewLine, logger.Text);
        ITaskItem[] outputs = result.ResultsByTarget.TryGetValue(target, out TargetResult? targetResult)
            ? targetResult.Items
            : [];
        return new BuildObservation(logger.RanLayout, logger.Text, outputs);
    }

    /// <summary>
    /// Creates a synthetic producer whose Pack target materializes one mapped file, allowing
    /// mapping tests to distinguish Pack-dependent discovery from direct item enumeration.
    /// </summary>
    private static string CreateBuiltinProducer(string manifestsDirectory, string name, bool shipping)
    {
        string root = Path.Combine(manifestsDirectory, name);
        foreach (string relativePath in PayloadFiles.Where(path => path != "auxiliary.bin"))
        {
            WriteFile(Path.Combine(root, relativePath), name + ":" + relativePath);
        }
        WriteFile(Path.Combine(root, "ignored.txt"), "not package data");
        return WriteFile(
            Path.Combine(root, name + ".proj"),
            $$"""
            <Project>
              {{UsingTasks}}
              <PropertyGroup>
                <IsShipping>{{shipping.ToString().ToLowerInvariant()}}</IsShipping>
                <BuiltinWorkloadFeatureBand>11.0.100</BuiltinWorkloadFeatureBand>
                <_workloadVersionSuffix>-preview.2</_workloadVersionSuffix>
                <Version>11.0.1</Version>
              </PropertyGroup>
              <ItemGroup>
                <None Include="WorkloadManifest.json;WorkloadManifest.targets;auxiliary.bin" PackagePath="data" />
                <None Include="localize\strings.fr.json" PackagePath="data\localize" />
                <None Include="ignored.txt" PackagePath="other" />
              </ItemGroup>
              <Import Project="{{Escape(Path.Combine(manifestsDirectory, "Directory.Build.targets"))}}" />
              <Target Name="Pack">
                <WriteLinesToFile File="{{Escape(Path.Combine(root, "auxiliary.bin"))}}" Lines="generated by Pack" Overwrite="true" WriteOnlyWhenDifferent="true" />
                <WriteLinesToFile File="{{Escape(Path.Combine(root, "pack.marker"))}}" Lines="packed" Overwrite="true" />
              </Target>
            </Project>
            """);
    }

    private sealed record BuildObservation(bool RanLayout, string Log, ITaskItem[] Outputs);

    /// <summary>
    /// Records which targets started or were skipped as up to date, which tasks ran, and all messages.
    /// </summary>
    private sealed class LayoutLogger : ILogger
    {
        private readonly List<string> _messages = [];

        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
        public string? Parameters { get; set; }
        public HashSet<string> StartedTargets { get; } = [];
        public HashSet<string> SkippedTargets { get; } = [];
        public HashSet<string> ExecutedTasks { get; } = [];
        public bool RanLayout => StartedTargets.Contains("LayoutManifests") && !SkippedTargets.Contains("LayoutManifests");
        public string Text => string.Join(Environment.NewLine, _messages);

        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += (_, args) =>
            {
                switch (args)
                {
                    case TargetStartedEventArgs started:
                        StartedTargets.Add(started.TargetName);
                        break;
                    case TargetSkippedEventArgs skipped when skipped.SkipReason == TargetSkipReason.OutputsUpToDate:
                        SkippedTargets.Add(skipped.TargetName);
                        break;
                    case TaskStartedEventArgs task:
                        ExecutedTasks.Add(task.TaskName);
                        break;
                }

                if (args.Message is not null)
                {
                    _messages.Add(args.Message);
                }
            };
        }

        public void Shutdown() { }
    }

    private sealed class ManifestDefinition(string id)
    {
        public string Id { get; set; } = id;
        public string FeatureBand { get; set; } = "11.0.100";
        public string Version { get; set; } = "11.0.1";
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Isolates the production layout targets behind controllable producer inputs, with layout
    /// state stored outside staging and broad installer cleanup forbidden.
    /// </summary>
    private sealed class ManifestProject
    {
        private readonly string _projectPath;
        private readonly string _targetsPath;

        public ManifestProject(string root)
        {
            Root = root;
            _projectPath = Path.Combine(root, "manifest-layout.proj");
            _targetsPath = Path.Combine(root, "BundledManifests.targets");
            File.Copy(
                LayoutFiles.GetPath(
                    "BundledManifests.targets",
                    Path.Combine("src", "Layout", "redist", "targets", "BundledManifests.targets")),
                _targetsPath);
            foreach (bool builtin in new[] { false, true })
            {
                foreach (string relativePath in PayloadFiles)
                {
                    WriteFile(Source(builtin, relativePath), $"{(builtin ? "builtin" : "downloaded")}:{relativePath}");
                }
            }
            WriteInputs();
        }

        public string Root { get; }
        public ManifestDefinition Downloaded { get; } = new("Test.Downloaded");
        public ManifestDefinition Builtin { get; } = new("test.builtin");
        public string LayoutRoot => Path.Combine(Root, "sdk", "sdk-manifests");
        public string CompletionFile => Path.Combine(Root, "obj", "incremental-layout", "manifests-test-rid.complete");
        public string[] StateFiles => [Path.ChangeExtension(CompletionFile, "inputs"), CompletionFile];
        public string BaselineFile => Path.Combine(LayoutRoot, "11.0.100-baseline", "workloadsets", "11.0.100-baseline", "baseline.workloadset.json");

        public string Source(bool builtin, string relativePath) =>
            Path.Combine(Root, builtin ? "builtin" : "downloaded", "data", relativePath);

        public string Output(bool builtin, string relativePath)
        {
            ManifestDefinition definition = builtin ? Builtin : Downloaded;
            return Path.Combine(LayoutRoot, definition.FeatureBand, definition.Id.ToLowerInvariant(), definition.Version, relativePath);
        }

        /// <summary>
        /// Replaces the synthetic built-in mapping provider with a copied production producer,
        /// its templates, and the production mapping targets.
        /// </summary>
        /// <param name="name">The producer directory and project name.</param>
        /// <returns>The directory configured for generated manifest files, not the final SDK intermediate path.</returns>
        /// <remarks>
        /// The producer keeps its NoTargets SDK and NuGet Pack graph, restored from an isolated local feed.
        /// Only the aggregate traversal SDK is removed.
        /// </remarks>
        public string UseBuiltinProducer(string name)
        {
            Builtin.Id = name.Replace(".Manifest", "").ToLowerInvariant();
            WriteInputs();
            string repo = Path.Combine(Root, "repo");
            string manifests = Path.Combine(repo, "src", "Workloads", "Manifests");
            string producerDirectory = Path.Combine(manifests, name);
            string sourceDirectory = Path.Combine(AppContext.BaseDirectory, "ManifestProducers", name);
            foreach (string source in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(producerDirectory, Path.GetRelativePath(sourceDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }

            File.Copy(GetWorkloadFilePath("Directory.Build.targets"), Path.Combine(manifests, "Directory.Build.targets"), overwrite: true);
            WriteFile(Path.Combine(repo, "Directory.Build.targets"), "<Project />");
            string generatedDirectory = Path.Combine(Root, "generated");
            string templatingAssembly = Path.Combine(AppContext.BaseDirectory, "ManifestGeneration", "Microsoft.DotNet.Build.Tasks.Templating.dll");
            string support = WriteFile(
                Path.Combine(manifests, "generation-support.targets"),
                $$"""
                <Project>
                  {{UsingTasks}}
                  <UsingTask TaskName="Microsoft.DotNet.Build.Tasks.Templating.GenerateFileFromTemplate" AssemblyFile="{{Escape(templatingAssembly)}}" />
                  <PropertyGroup>
                    <IntermediateOutputPath>{{Escape(generatedDirectory + Path.DirectorySeparatorChar)}}</IntermediateOutputPath>
                    <IsShipping>true</IsShipping>
                    <BuiltinWorkloadFeatureBand>{{Escape(Builtin.FeatureBand)}}</BuiltinWorkloadFeatureBand>
                    <Version>{{Escape(Builtin.Version)}}</Version>
                    <PackageVersion>{{Escape(Builtin.Version)}}</PackageVersion>
                    <TargetFramework>{{ToolsetInfo.CurrentTargetFramework}}</TargetFramework>
                    <IsPackable>true</IsPackable>
                    <PackageId>{{Escape(name)}}</PackageId>
                    <PackageOutputPath>{{Escape(Path.Combine(Root, "nupkgs"))}}</PackageOutputPath>
                    <NoWarn>NU5128</NoWarn>
                    <MicrosoftNETCoreAppRuntimePackageVersion>11.0.1</MicrosoftNETCoreAppRuntimePackageVersion>
                    <VersionFeature60>1</VersionFeature60>
                    <VersionFeature70>1</VersionFeature70>
                    <VersionFeature80ForWorkloads>1</VersionFeature80ForWorkloads>
                    <VersionFeature90ForWorkloads>1</VersionFeature90ForWorkloads>
                    <VersionFeature100ForWorkloads>1</VersionFeature100ForWorkloads>
                  </PropertyGroup>
                </Project>
                """);

            using var collection = new ProjectCollection();
            string feed = Path.Combine(AppContext.BaseDirectory, "ManifestPackages");
            string sdkPackage = Directory.GetFiles(feed, "microsoft.build.notargets.*.nupkg").Single();
            string sdkVersion = Path.GetFileName(sdkPackage)["microsoft.build.notargets.".Length..^".nupkg".Length];

            WriteFile(Path.Combine(Root, "global.json"),
                $$$"""{"msbuild-sdks":{"Microsoft.Build.NoTargets":"{{{sdkVersion}}}"}}""");
            WriteFile(Path.Combine(Root, "NuGet.config"),
                $$"""<configuration><packageSources><clear /><add key="manifest-test-packages" value="{{Escape(feed)}}" /></packageSources></configuration>""");
            WriteFile(Path.Combine(manifests, "Directory.Build.props"),
                $$"""<Project><Import Project="{{Escape(support)}}" /></Project>""");
            WriteFile(Path.Combine(Root, "Directory.Packages.props"), "<Project />");

            // Record the returned built-in mappings so the test can compare them with the package.
            ProjectRootElement layout = ProjectRootElement.Open(_projectPath, collection);
            ProjectTargetElement capture = layout.AddTarget("CaptureBuiltinMappings");
            capture.AfterTargets = "_PrepareLayoutManifests";
            ProjectTaskElement write = capture.AddTask("WriteLinesToFile");
            write.SetParameter("File", Path.Combine(Root, "builtin-mappings.txt"));
            write.SetParameter("Lines", "@(_BuiltinManifestLayoutInput->'%(Identity)|%(DestinationPath)')");
            write.SetParameter("Overwrite", "true");
            write.SetParameter("WriteOnlyWhenDifferent", "true");
            layout.Save();

            ProjectRootElement traversal = ProjectRootElement.Open(GetWorkloadFilePath("manifest-packages.csproj"), collection);
            traversal.Sdk = string.Empty;
            traversal.AddImport(support);
            ProjectItemGroupElement references = traversal.AddItemGroup();
            references.AddItem("AbsoluteProjectReference", "@(ProjectReference->'%(FullPath)')");
            ProjectItemElement remove = traversal.CreateItemElement("ProjectReference");
            remove.Remove = "@(ProjectReference)";
            references.AppendChild(remove);
            references.AddItem("ProjectReference", "@(AbsoluteProjectReference)");
            traversal.Save(Path.Combine(manifests, "manifest-packages.csproj"));
            return generatedDirectory;
        }

        /// <summary>
        /// Rewrites the fixture projects from the current manifest definitions, restoring the
        /// synthetic built-in mapping provider and preserving existing source payloads.
        /// </summary>
        public void WriteInputs()
        {
            string repoRoot = Path.Combine(Root, "repo");
            WriteFile(
                _projectPath,
                $$"""
                <Project>
                  {{UsingTasks}}
                  <PropertyGroup>
                    <RepoRoot>{{Escape(repoRoot + Path.DirectorySeparatorChar)}}</RepoRoot>
                    <IntermediateOutputPath>{{Escape(Path.Combine(Root, "obj") + Path.DirectorySeparatorChar)}}</IntermediateOutputPath>
                    <RedistInstallerLayoutPath>{{Escape(Path.Combine(Root, "sdk") + Path.DirectorySeparatorChar)}}</RedistInstallerLayoutPath>
                    <NuGetPackageRoot>{{Escape(Path.Combine(Root, "packages") + Path.DirectorySeparatorChar)}}</NuGetPackageRoot>
                    <ProductMonikerRid>test-rid</ProductMonikerRid>
                    <TargetArchitecture>x64</TargetArchitecture>
                    <Version>11.0.100</Version>
                    <VersionPrefix>11.0.100</VersionPrefix>
                    <DotNetFinalVersionKind>release</DotNetFinalVersionKind>
                    <BuiltinWorkloadFeatureBand>{{Escape(Builtin.FeatureBand)}}</BuiltinWorkloadFeatureBand>
                    <DotNet1xxWorkloadManifestVersion>{{Escape(Builtin.Version)}}</DotNet1xxWorkloadManifestVersion>
                  </PropertyGroup>
                  <Import Project="{{Escape(_targetsPath)}}" />
                  <ItemGroup>
                    <BundledManifests Remove="@(BundledManifests)" />
                    <BuiltinManifests Remove="@(BuiltinManifests)" />
                    <BundledManifests Include="{{Escape(Downloaded.Id)}}" Condition="'{{Downloaded.Enabled}}' == 'True'"
                                      FeatureBand="{{Escape(Downloaded.FeatureBand)}}"
                                      Version="{{Escape(Downloaded.Version)}}"
                                      NupkgId="{{Escape($"{Downloaded.Id}.Manifest-{Downloaded.FeatureBand}")}}"
                                      MsiNupkgId="{{Escape($"{Downloaded.Id}.Manifest-{Downloaded.FeatureBand}.Msi.x64")}}" />
                    <BuiltinManifests Include="{{Escape(Builtin.Id)}}" Condition="'{{Builtin.Enabled}}' == 'True'" />
                  </ItemGroup>
                  <Target Name="GenerateInstallerLayout">
                    <Error Text="Standalone manifest layout must not invoke broad installer cleanup." />
                  </Target>
                </Project>
                """);
            WriteFile(
                Path.Combine(repoRoot, "src", "Workloads", "Manifests", "manifest-packages.csproj"),
                $$"""
                <Project>
                  <Target Name="GetBuiltinManifestLayoutInputs" Returns="@(SyntheticInput)">
                    <ItemGroup Condition="'{{Builtin.Enabled}}' == 'True'">
                      <SyntheticRootInput Include="{{Escape(Source(true, "WorkloadManifest.json"))}}" />
                      <SyntheticRootInput Include="{{Escape(Source(true, "*"))}}" Exclude="{{Escape(Source(true, "WorkloadManifest.json"))}}" />
                      <SyntheticLocalizeInput Include="{{Escape(Source(true, Path.Combine("localize", "*")))}}" />
                      <SyntheticInput Include="@(SyntheticRootInput)">
                        <DestinationPath>{{Escape(Output(true, "%(SyntheticRootInput.Filename)%(SyntheticRootInput.Extension)"))}}</DestinationPath>
                      </SyntheticInput>
                      <SyntheticInput Include="@(SyntheticLocalizeInput)">
                        <DestinationPath>{{Escape(Output(true, Path.Combine("localize", "%(SyntheticLocalizeInput.Filename)%(SyntheticLocalizeInput.Extension)")))}}</DestinationPath>
                      </SyntheticInput>
                    </ItemGroup>
                  </Target>
                </Project>
                """);
        }

        /// <summary>
        /// Runs a layout target. When <paramref name="interruptionTask"/> is set, an in-memory Error task is
        /// inserted before that task, so the copied targets file keeps its timestamp.
        /// </summary>
        public BuildObservation Build(
            Dictionary<string, string>? globalProperties = null,
            bool expectedSuccess = true,
            string? interruptionTask = null,
            string target = "LayoutManifests")
        {
            SyncDownloadedPackage();
            return BuildProject(
                _projectPath,
                target,
                globalProperties,
                expectedSuccess,
                interruptionTask is null ? null : collection => InjectInterruption(collection, interruptionTask));
        }

        public void SyncDownloadedPackage()
        {
            string packageData = Path.Combine(
                Root, "packages",
                $"{Downloaded.Id}.Manifest-{Downloaded.FeatureBand}".ToLowerInvariant(),
                NuGetVersion.Parse(Downloaded.Version).ToNormalizedString().ToLowerInvariant(),
                "data");
            if (Directory.Exists(packageData))
            {
                Directory.Delete(packageData, recursive: true);
            }

            if (!Downloaded.Enabled)
            {
                return;
            }

            string sourceData = Path.Combine(Root, "downloaded", "data");
            foreach (string source in Directory.GetFiles(sourceData, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(packageData, Path.GetRelativePath(sourceData, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }
        }

        private void InjectInterruption(ProjectCollection collection, string interruptionTask)
        {
            ProjectRootElement targets = ProjectRootElement.Open(_targetsPath, collection);
            ProjectTargetElement layout = targets.Targets.Single(candidate => candidate.Name == "LayoutManifests");
            ProjectTaskElement interruptedTask = layout.Tasks.First(task => task.Name == interruptionTask);

            ProjectTaskElement error = layout.AddTask("Error");
            error.SetParameter("Text", "Injected manifest layout interruption");
            layout.RemoveChild(error);
            layout.InsertBeforeChild(error, interruptedTask);
        }
    }
}
