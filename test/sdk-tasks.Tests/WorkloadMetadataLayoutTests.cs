// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Verifies incremental ownership, cleanup, and recovery for workload userlocal markers
/// and root launcher shims in redist and intermediate installer layouts.
/// </summary>
[TestClass]
// MSBuild's default operating-environment save/restore affects process-wide state.
// Other in-process layout tests do not participate in a shared resource lock.
[DoNotParallelize]
public class WorkloadMetadataLayoutTests : SdkTest
{
    /// <summary>
    /// Verifies feature-band changes and disabling source-only mode remove obsolete markers
    /// without modifying unowned metadata, and unchanged builds preserve output timestamps.
    /// </summary>
    [TestMethod]
    public void MarkersTrackBandAndSourceOnlyTransitionsWithoutDeletingOtherMetadata()
    {
        LayoutProject project = CreateProject();
        string[] unowned =
        [
            project.Write("metadata/workloads/11.0.100/InstalledWorkloads/record", "record"),
            project.Write("metadata/workloads/11.0.100/nested/userlocal", "nested"),
            project.Write("metadata/workloads/11.0.101/userlocal", "not a band"),
            project.Write("metadata/workloads/backup/userlocal", "backup"),
            project.Write("metadata/workloads/userlocal", "parent"),
            project.Write("sdk-manifests/11.0.100/manifest/11.0.1/payload", "manifest"),
            project.Write("dnx.cmd", "sibling shim")
        ];
        Dictionary<string, DateTime> timestamps = Snapshot(unowned);
        project.Write("metadata/workloads/10.0.100/userlocal", "");
        project.Write("metadata/workloads/9.0.200-preview.2/userlocal", "");

        project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true).Ran.Should().BeTrue();
        string first = Path.Combine(project.Layout, "metadata", "workloads", "11.0.100", "userlocal");
        File.ReadAllBytes(first).Should().BeEmpty();
        Directory.Exists(Path.Combine(project.Layout, "metadata", "workloads", "10.0.100")).Should().BeFalse();
        Directory.Exists(Path.Combine(project.Layout, "metadata", "workloads", "9.0.200-preview.2")).Should().BeFalse();
        Dictionary<string, DateTime> warm = Snapshot([first, .. project.StateFiles("userlocal")]);
        project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true).Ran.Should().BeFalse();
        AssertTimestamps(warm);

        project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, band: "11.0.2").Ran.Should().BeTrue();
        File.Exists(first).Should().BeFalse();
        string second = Path.Combine(project.Layout, "metadata", "workloads", "11.0.200", "userlocal");
        File.Exists(second).Should().BeTrue();
        project.Build("LayoutWorkloadUserLocalMarker").Ran.Should().BeTrue();
        File.Exists(second).Should().BeFalse();
        project.Build("LayoutWorkloadUserLocalMarker").Ran.Should().BeFalse();
        foreach (string path in unowned)
        {
            File.Exists(path).Should().BeTrue();
        }
        AssertTimestamps(timestamps);
    }

    /// <summary>
    /// Verifies marker builds repair missing or nonempty outputs and corrupt or missing state,
    /// recover from interrupted builds, and prune empty recognized feature-band directories.
    /// </summary>
    [TestMethod]
    public void MarkerRepairsMissingOutputsStateAndInterruptedBuilds()
    {
        LayoutProject project = CreateProject();
        const string Target = "LayoutWorkloadUserLocalMarker";
        project.Build(Target, sourceOnly: true);
        string marker = Path.Combine(project.Layout, "metadata", "workloads", "11.0.100", "userlocal");
        File.Delete(marker);
        project.Build(Target, sourceOnly: true).Ran.Should().BeTrue();
        File.ReadAllBytes(marker).Should().BeEmpty();
        File.WriteAllText(marker, "not empty");
        project.Build(Target, sourceOnly: true).Ran.Should().BeTrue();
        File.ReadAllBytes(marker).Should().BeEmpty();
        foreach (string state in project.StateFiles("userlocal"))
        {
            File.WriteAllText(state, "corrupt");
            project.Build(Target, sourceOnly: true).Ran.Should().BeTrue();
            File.Delete(state);
            project.Build(Target, sourceOnly: true).Ran.Should().BeTrue();
        }

        project.Write("metadata/workloads/10.0.100/userlocal", "");
        project.Build(Target, sourceOnly: true, interrupt: true, success: false);
        File.Exists(project.Completion("userlocal")).Should().BeFalse();
        project.Build(Target, sourceOnly: true).Ran.Should().BeTrue();
        project.Build(Target, sourceOnly: true).Ran.Should().BeFalse();
        Directory.CreateDirectory(Path.Combine(project.Layout, "metadata", "workloads", "8.0.100"));
        project.Build(Target, sourceOnly: true).Ran.Should().BeTrue();
        Directory.Exists(Path.Combine(project.Layout, "metadata", "workloads", "8.0.100")).Should().BeFalse();
    }

    /// <summary>
    /// Verifies an invalid feature band fails before deleting stale owned markers
    /// or writing a completion stamp.
    /// </summary>
    [TestMethod]
    public void InvalidBandFailsBeforeRemovingOwnedMarkers()
    {
        LayoutProject project = CreateProject();
        string stale = project.Write("metadata/workloads/10.0.100/userlocal", "");
        project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, band: "..", success: false);
        File.Exists(stale).Should().BeTrue();
        File.Exists(project.Completion("userlocal")).Should().BeFalse();
    }

    /// <summary>
    /// Verifies root shims skip unchanged builds, track source edits, repair outputs and state,
    /// recover from interruptions, and restore Unix permissions without modifying unowned files.
    /// </summary>
    [TestMethod]
    public void ShimsSkipWarmBuildsTrackEditsAndRepairOutputsAndState()
    {
        LayoutProject project = CreateProject();
        string name = OperatingSystem.IsWindows() ? "dnx.cmd" : "dnx";
        string alternate = name == "dnx" ? "dnx.cmd" : "dnx";
        string source = Path.Combine(project.Root, name);
        string output = Path.Combine(project.Layout, name);
        string sentinel = project.Write("nested/" + alternate, "unowned");
        Dictionary<string, DateTime> sentinelTimestamp = Snapshot([sentinel]);
        project.Write(alternate, "stale");
        project.Build("LayoutDnxShim").Ran.Should().BeTrue();
        File.Exists(Path.Combine(project.Layout, alternate)).Should().BeFalse();
        File.ReadAllText(output).Should().Be(File.ReadAllText(source));
        Dictionary<string, DateTime> warm = Snapshot([output, .. project.StateFiles("dnx")]);
        Observation skipped = project.Build("LayoutDnxShim");
        skipped.Ran.Should().BeFalse();
        skipped.SourceCount.Should().Be(1);
        AssertTimestamps(warm);

        string original = File.ReadAllText(source);
        foreach (string content in new[] { "edited shim", original })
        {
            WriteNewer(source, content, project.Completion("dnx"));
            project.Build("LayoutDnxShim").Ran.Should().BeTrue();
            File.ReadAllText(output).Should().Be(content);
        }
        File.Delete(output);
        project.Build("LayoutDnxShim").Ran.Should().BeTrue();
        project.Write(alternate, "injected stale");
        project.Build("LayoutDnxShim").Ran.Should().BeTrue();
        File.Exists(Path.Combine(project.Layout, alternate)).Should().BeFalse();
        foreach (string state in project.StateFiles("dnx"))
        {
            File.WriteAllText(state, "corrupt");
            project.Build("LayoutDnxShim").Ran.Should().BeTrue();
            File.Delete(state);
            project.Build("LayoutDnxShim").Ran.Should().BeTrue();
        }
        WriteNewer(source, "changed before failure", project.Completion("dnx"));
        project.Build("LayoutDnxShim", interrupt: true, success: false);
        File.Exists(project.Completion("dnx")).Should().BeFalse();
        project.Build("LayoutDnxShim").Ran.Should().BeTrue();
        project.Build("LayoutDnxShim").Ran.Should().BeFalse();
        AssertTimestamps(sentinelTimestamp);

        if (!OperatingSystem.IsWindows())
        {
            UnixFileMode expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.GetUnixFileMode(output).Should().Be(expectedMode);
            File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            project.Build("LayoutDnxShim").Ran.Should().BeTrue();
            File.GetUnixFileMode(output).Should().Be(expectedMode);
            File.ReadAllText(output).Should().Be("changed before failure");
            project.Build("LayoutDnxShim").Ran.Should().BeFalse();
        }
    }

    /// <summary>
    /// Verifies base and intermediate shims repair replaced contents even when length,
    /// timestamp, and permissions are unchanged, then skip subsequent unchanged builds.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShimsRepairReplacedContentsWithUnchangedMetadata(bool intermediate)
    {
        LayoutProject project = CreateProject(simulateHost: intermediate);
        string target = intermediate ? "LayoutIntermediateDnxShim" : "LayoutDnxShim";
        string name = intermediate || !OperatingSystem.IsWindows() ? "dnx" : "dnx.cmd";
        string? host = intermediate ? "Linux" : null;
        string output = Path.Combine(intermediate ? project.IntermediateLayout : project.Layout, name);
        byte[] expected = File.ReadAllBytes(Path.Combine(project.Root, name));
        project.Build(target, host: host).Ran.Should().BeTrue();
        project.Build(target, host: host).Ran.Should().BeFalse();

        DateTime timestamp = File.GetLastWriteTimeUtc(output);
        UnixFileMode mode = default;
        if (!OperatingSystem.IsWindows())
        {
            mode = File.GetUnixFileMode(output);
        }
        byte[] replacement = [.. expected];
        replacement[0] ^= 1;
        File.WriteAllBytes(output, replacement);
        File.SetLastWriteTimeUtc(output, timestamp);
        new FileInfo(output).Length.Should().Be(expected.Length);
        File.GetLastWriteTimeUtc(output).Should().Be(timestamp);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(output).Should().Be(mode);
        }

        project.Build(target, host: host).Ran.Should().BeTrue();
        File.ReadAllBytes(output).Should().Equal(expected);
        string family = intermediate ? "dnx-installer" : "dnx";
        Dictionary<string, DateTime> warm = Snapshot([output, .. project.StateFiles(family)]);
        project.Build(target, host: host).Ran.Should().BeFalse();
        AssertTimestamps(warm);
    }

    /// <summary>
    /// Verifies Windows runtime archive repopulation cannot retain foreign shim contents
    /// when completion survives and the SDK shim source is unchanged.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AggregateRepopulationRepairsArchiveShimWithSurvivingCompletion()
    {
        LayoutProject project = CreateProject(aggregate: true);
        string source = Path.Combine(project.Root, "dnx.cmd");
        byte[] expected = File.ReadAllBytes(source);
        byte[] replacement = [.. expected];
        replacement[0] ^= 1;
        DateTime sourceTimestamp = File.GetLastWriteTimeUtc(source);
        foreach (byte[] contents in new[] { expected, replacement })
        {
            using (var archive = new ZipArchive(File.Create(Path.Combine(project.Root, "runtime.zip")), ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("dnx.cmd");
                entry.LastWriteTime = new DateTimeOffset(sourceTimestamp);
                using Stream stream = entry.Open();
                stream.Write(contents);
            }

            project.Build("GenerateInstallerLayout", observedTarget: "LayoutDnxShim").Ran.Should().BeTrue();
            File.ReadAllBytes(Path.Combine(project.Layout, "dnx.cmd")).Should().Equal(expected);
            File.GetLastWriteTimeUtc(source).Should().Be(sourceTimestamp);
        }
    }

    /// <summary>
    /// Verifies a missing selected shim source fails before deleting the alternate root shim
    /// or writing a completion stamp.
    /// </summary>
    [TestMethod]
    public void MissingShimInputDoesNotDeleteAlternateOutput()
    {
        LayoutProject project = CreateProject();
        string name = OperatingSystem.IsWindows() ? "dnx.cmd" : "dnx";
        string alternate = name == "dnx" ? "dnx.cmd" : "dnx";
        string stale = project.Write(alternate, "preserve until inputs are valid");
        File.Delete(Path.Combine(project.Root, name));
        project.Build("LayoutDnxShim", success: false);
        File.Exists(stale).Should().BeTrue();
        File.Exists(project.Completion("dnx")).Should().BeFalse();
    }

    /// <summary>
    /// Verifies directories occupying owned marker or shim file paths cause failure
    /// before stale owned files are removed.
    /// </summary>
    [TestMethod]
    public void DirectoryAtOwnedFilePathFailsBeforeCleanup()
    {
        LayoutProject project = CreateProject();
        string marker = project.Write("metadata/workloads/10.0.100/userlocal", "");
        Directory.CreateDirectory(Path.Combine(project.Layout, "metadata", "workloads", "11.0.100", "userlocal"));
        project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, success: false);
        File.Exists(marker).Should().BeTrue();

        string name = OperatingSystem.IsWindows() ? "dnx.cmd" : "dnx";
        string alternate = name == "dnx" ? "dnx.cmd" : "dnx";
        string stale = project.Write(alternate, "stale");
        Directory.CreateDirectory(Path.Combine(project.Layout, name));
        project.Build("LayoutDnxShim", success: false);
        File.Exists(stale).Should().BeTrue();
    }

    /// <summary>
    /// Verifies symbolic links at owned paths are rejected without deleting stale outputs
    /// or changing linked contents, timestamps, or Unix permissions.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void LinkedOwnedPathsFailWithoutModifyingTheirTargets()
    {
        LayoutProject project = CreateProject();
        string outside = Path.Combine(project.Root, "outside");
        string externalMarker = WriteFile(Path.Combine(outside, "userlocal"), "external marker");
        string workloads = Path.Combine(project.Layout, "metadata", "workloads");
        Directory.CreateDirectory(workloads);
        Directory.CreateSymbolicLink(Path.Combine(workloads, "11.0.100"), outside);
        string stale = project.Write("metadata/workloads/10.0.100/userlocal", "");
        project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, success: false);
        File.ReadAllText(externalMarker).Should().Be("external marker");
        File.Exists(stale).Should().BeTrue();

        string externalShim = WriteFile(Path.Combine(outside, "dnx"), "external shim");
        DateTime timestamp = File.GetLastWriteTimeUtc(externalShim);
        UnixFileMode mode = File.GetUnixFileMode(externalShim);
        File.CreateSymbolicLink(Path.Combine(project.Layout, "dnx"), externalShim);
        string alternate = project.Write("dnx.cmd", "stale");
        project.Build("LayoutDnxShim", success: false);
        File.ReadAllText(externalShim).Should().Be("external shim");
        File.GetLastWriteTimeUtc(externalShim).Should().Be(timestamp);
        File.GetUnixFileMode(externalShim).Should().Be(mode);
        File.Exists(alternate).Should().BeTrue();
    }

    /// <summary>
    /// Verifies a Windows junction at an owned feature-band path causes failure before cleanup,
    /// preserving external and stale marker contents and timestamps without recording completion.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void JunctionOwnedPathsFailBeforeDeletingStaleMarkers()
    {
        LayoutProject project = CreateProject();
        string outside = Path.Combine(project.Root, "outside");
        string externalMarker = WriteFile(Path.Combine(outside, "userlocal"), "external marker");
        string stale = project.Write("metadata/workloads/10.0.100/userlocal", "stale marker");
        Dictionary<string, DateTime> timestamps = Snapshot([externalMarker, stale]);
        string junction = Path.Combine(project.Layout, "metadata", "workloads", "11.0.100");
        string command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        new RunExeCommand(Log, command)
            .Execute("/d", "/c", "mklink", "/J", junction, outside)
            .Should().Pass();

        try
        {
            (File.GetAttributes(junction) & FileAttributes.ReparsePoint).Should().Be(FileAttributes.ReparsePoint);
            project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, success: false);
            File.ReadAllText(externalMarker).Should().Be("external marker");
            File.ReadAllText(stale).Should().Be("stale marker");
            AssertTimestamps(timestamps);
            File.Exists(project.Completion("userlocal")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    /// <summary>
    /// Verifies simulated host changes select the correct base shim and exclude intermediate
    /// shims on Windows and macOS while preserving unowned installer payloads.
    /// </summary>
    [TestMethod]
    public void SimulatedHostTransitionsCleanBaseAndIntermediateRootShims()
    {
        LayoutProject project = CreateProject(simulateHost: true);
        string sentinel = WriteFile(Path.Combine(project.IntermediateLayout, "sdk", "keep"), "sdk");
        Dictionary<string, DateTime> sentinelTimestamp = Snapshot([sentinel]);
        foreach (string host in new[] { "Linux", "Windows", "OSX", "Linux" })
        {
            project.Build("LayoutDnxShim", host: host);
            project.Build("LayoutIntermediateDnxShim", host: host);
            string baseName = host == "Windows" ? "dnx.cmd" : "dnx";
            File.ReadAllText(Path.Combine(project.Layout, baseName))
                .Should().Be(File.ReadAllText(Path.Combine(project.Root, baseName)));
            File.Exists(Path.Combine(project.Layout, baseName == "dnx" ? "dnx.cmd" : "dnx")).Should().BeFalse();
            File.Exists(Path.Combine(project.IntermediateLayout, "dnx")).Should().Be(host == "Linux");
            File.Exists(Path.Combine(project.IntermediateLayout, "dnx.cmd")).Should().BeFalse();
            project.Build("LayoutDnxShim", host: host).Ran.Should().BeFalse();
            project.Build("LayoutIntermediateDnxShim", host: host).Ran.Should().BeFalse();
        }
        project.Build("PrepareIntermediateSdkInstallerOutput", host: "Linux");
        File.Exists(Path.Combine(project.IntermediateLayout, "dnx")).Should().BeTrue();
        AssertTimestamps(sentinelTimestamp);
    }

    /// <summary>
    /// Verifies a skipped base-shim target retains source items needed to copy nonempty
    /// intermediate payloads while preserving base outputs and unowned installer files.
    /// </summary>
    [TestMethod]
    public void IntermediatePayloadIsCopiedAfterBaseShimSkips()
    {
        LayoutProject project = CreateProject();
        string payload = Path.Combine(project.Root, "payload");
        WriteFile(Path.Combine(payload, "tool.dll"), "tool payload");
        WriteFile(Path.Combine(payload, "nested", "data.json"), "nested payload");
        string sentinel = WriteFile(Path.Combine(project.IntermediateLayout, "sdk", "keep"), "unowned payload");
        Dictionary<string, DateTime> sentinelTimestamp = Snapshot([sentinel]);
        project.Build("LayoutDnxShim");
        string baseShim = Path.Combine(project.Layout, OperatingSystem.IsWindows() ? "dnx.cmd" : "dnx");
        Dictionary<string, DateTime> warm = Snapshot([baseShim, .. project.StateFiles("dnx")]);

        Observation result = project.Build("PrepareIntermediateSdkInstallerOutput", observedTarget: "LayoutDnxShim");
        result.Ran.Should().BeFalse();
        result.SourceCount.Should().Be(1);
        File.ReadAllText(Path.Combine(project.IntermediateLayout, "sdk", "11.0.1", "tool.dll"))
            .Should().Be("tool payload");
        File.ReadAllText(Path.Combine(project.IntermediateLayout, "sdk", "11.0.1", "nested", "data.json"))
            .Should().Be("nested payload");
        File.Exists(Path.Combine(project.IntermediateLayout, "dnx"))
            .Should().Be(!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS());
        File.Exists(Path.Combine(project.IntermediateLayout, "dnx.cmd")).Should().BeFalse();
        AssertTimestamps(warm);
        AssertTimestamps(sentinelTimestamp);
    }

    /// <summary>
    /// Verifies Linux intermediate shims repair permissions to exactly 0755 without changing
    /// script bytes, and subsequent unchanged builds skip.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [SupportedOSPlatform("linux")]
    public void IntermediateShimRepairsExactUnixPermissionsWithoutChangingBytes()
    {
        LayoutProject project = CreateProject();
        const string Target = "LayoutIntermediateDnxShim";
        string output = Path.Combine(project.IntermediateLayout, "dnx");
        UnixFileMode expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        project.Build(Target).Ran.Should().BeTrue();
        byte[] bytes = File.ReadAllBytes(output);
        File.GetUnixFileMode(output).Should().Be(expectedMode);
        project.Build(Target).Ran.Should().BeFalse();

        File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        project.Build(Target).Ran.Should().BeTrue();
        File.GetUnixFileMode(output).Should().Be(expectedMode);
        File.ReadAllBytes(output).Should().Equal(bytes);
        project.Build(Target).Ran.Should().BeFalse();
    }

    /// <summary>
    /// Verifies intermediate shim builds repair missing outputs and damaged state, recover
    /// from interrupted copies, and require no source input when host policy excludes the shim.
    /// </summary>
    [TestMethod]
    public void IntermediateShimRepairsStateMissingOutputsAndInterruptedCopies()
    {
        LayoutProject project = CreateProject(simulateHost: true);
        const string Target = "LayoutIntermediateDnxShim";
        project.Build(Target, host: "Linux");
        string output = Path.Combine(project.IntermediateLayout, "dnx");
        File.Delete(output);
        project.Build(Target, host: "Linux").Ran.Should().BeTrue();
        foreach (string state in project.StateFiles("dnx-installer"))
        {
            File.WriteAllText(state, "corrupt");
            project.Build(Target, host: "Linux").Ran.Should().BeTrue();
            File.Delete(state);
            project.Build(Target, host: "Linux").Ran.Should().BeTrue();
        }
        string stale = WriteFile(Path.Combine(project.IntermediateLayout, "dnx.cmd"), "stale");
        project.Build(Target, host: "Linux", interrupt: true, success: false);
        File.Exists(stale).Should().BeFalse();
        File.Exists(project.Completion("dnx-installer")).Should().BeFalse();
        project.Build(Target, host: "Linux").Ran.Should().BeTrue();
        project.Build(Target, host: "Linux").Ran.Should().BeFalse();
        project.Build(Target, host: "OSX");
        File.Exists(output).Should().BeFalse();
        File.Delete(Path.Combine(project.Root, "dnx"));
        project.Build(Target, host: "OSX").Ran.Should().BeFalse("excluded scripts need no source input");
    }

    /// <summary>
    /// Verifies incremental owned outputs match a clean layout and destination remapping
    /// creates the new outputs without cleaning the abandoned layout.
    /// </summary>
    [TestMethod]
    public void IncrementalOwnedOutputsMatchCleanLayoutAndDestinationRemaps()
    {
        LayoutProject incremental = CreateProject();
        LayoutProject clean = CreateProject();
        incremental.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, band: "10.0.1");
        incremental.Build("LayoutDnxShim");
        incremental.Write("metadata/workloads/8.0.100/userlocal", "");
        incremental.Write(OperatingSystem.IsWindows() ? "dnx" : "dnx.cmd", "stale");
        foreach (LayoutProject project in new[] { incremental, clean })
        {
            project.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, band: "11.0.2");
            project.Build("LayoutDnxShim");
        }
        LayoutContents(incremental.Layout).Should().BeEquivalentTo(LayoutContents(clean.Layout));

        string remapped = Path.Combine(incremental.Root, "remapped");
        incremental.Build("LayoutWorkloadUserLocalMarker", sourceOnly: true, layout: remapped).Ran.Should().BeTrue();
        incremental.Build("LayoutDnxShim", layout: remapped).Ran.Should().BeTrue();
        Directory.GetFiles(remapped, "*", SearchOption.AllDirectories).Should().HaveCount(2);
        // Changing the destination never grants ownership of the abandoned layout.
        LayoutContents(incremental.Layout).Should().BeEquivalentTo(LayoutContents(clean.Layout));
    }

    private LayoutProject CreateProject(bool simulateHost = false, bool aggregate = false) =>
        new(TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path, simulateHost, aggregate);

    private static Dictionary<string, string> LayoutContents(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllText);

    private static Dictionary<string, DateTime> Snapshot(IEnumerable<string> files) =>
        files.ToDictionary(path => path, File.GetLastWriteTimeUtc);

    private static void AssertTimestamps(Dictionary<string, DateTime> timestamps)
    {
        foreach ((string path, DateTime timestamp) in timestamps)
        {
            File.GetLastWriteTimeUtc(path).Should().Be(timestamp, "'{0}' is unchanged", path);
        }
    }

    private static string WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private static void WriteNewer(string path, string contents, string completion)
    {
        DateTime timestamp = File.GetLastWriteTimeUtc(completion);
        WriteFile(path, contents);
        while (File.GetLastWriteTimeUtc(path) <= timestamp)
        {
            Thread.Sleep(20);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    private static string Escape(string value) => SecurityElement.Escape(value)!;

    private sealed record Observation(bool Ran, int SourceCount);

    private sealed class LayoutProject
    {
        private readonly string _project;

        public LayoutProject(string root, bool simulateHost, bool aggregate)
        {
            Root = root;
            Layout = Path.Combine(root, "layout");
            IntermediateLayout = Path.Combine(root, "obj", "sdk-installer", "Debug");
            string targets = LayoutFiles.GetPath("GenerateInstallerLayout.targets",
                Path.Combine("src", "Layout", "redist", "targets", "GenerateInstallerLayout.targets"));
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_EXECUTION_DIRECTORY")))
            {
                targets.Should().Be(Path.Combine(SdkTestContext.Current.TestExecutionDirectory,
                    "Layout", "GenerateInstallerLayout.targets"));
            }
            using var collection = new ProjectCollection();
            ProjectRootElement production = ProjectRootElement.Open(targets, collection);
            foreach (ProjectTargetElement target in production.Targets)
            {
                if (simulateHost)
                {
                    foreach (ProjectElement element in target.AllChildren)
                    {
                        element.Condition = element.Condition
                            .Replace("!$([MSBuild]::IsOSPlatform('Windows'))", "'$(SimulatedHost)' != 'Windows'")
                            .Replace("!$([MSBuild]::IsOSPlatform('WINDOWS'))", "'$(SimulatedHost)' != 'Windows'")
                            .Replace("$([MSBuild]::IsOSPlatform('Windows'))", "'$(SimulatedHost)' == 'Windows'")
                            .Replace("!$([MSBuild]::IsOSPlatform('OSX'))", "'$(SimulatedHost)' != 'OSX'");
                    }
                    if (OperatingSystem.IsWindows())
                    {
                        foreach (ProjectTaskElement exec in target.Tasks.Where(task => task.Name == "Exec"))
                        {
                            exec.Condition = "false";
                        }
                    }
                }
                ProjectTaskElement? complete = target.Tasks.FirstOrDefault(task => task.Name == "CompleteIncrementalLayout");
                if (complete is not null)
                {
                    ProjectTaskElement error = production.CreateTaskElement("Error");
                    error.Condition = "'$(InterruptLayout)' == 'true'";
                    error.SetParameter("Text", "Injected layout interruption.");
                    target.InsertBeforeChild(error, complete);
                }
            }
            string copiedTargets = Path.Combine(root, "GenerateInstallerLayout.targets");
            production.Save(copiedTargets);
            foreach (string name in new[] { "dnx", "dnx.cmd" })
            {
                string source = LayoutFiles.GetPath(name, Path.Combine("src", "Layout", "redist", name));
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_EXECUTION_DIRECTORY")))
                {
                    source.Should().Be(Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "Layout", name));
                }
                File.Copy(source, Path.Combine(root, name));
            }
            string assembly = typeof(GetDnxShimLayout).Assembly.Location;
            string builtIn = Path.Combine(Path.GetDirectoryName(typeof(Project).Assembly.Location)!, "Microsoft.Build.Tasks.Core.dll");
            string usingTasks = string.Join(Environment.NewLine,
                new[] { "GetWorkloadUserLocalLayout", "GetDnxShimLayout", "PruneEmptyLayoutDirectories", "CompleteIncrementalLayout", "CopyPreservingRelativeSymlinks", "ExtractArchiveToDirectory" }
                    .Select(name => $"<UsingTask TaskName=\"{name}\" AssemblyFile=\"{Escape(assembly)}\" />")
                    .Concat(new[] { "Copy", "Delete", "WriteLinesToFile", "ReadLinesFromFile", "Error", "Exec", "RemoveDir", "MakeDir" }
                        .Select(name => $"<UsingTask TaskName=\"Microsoft.Build.Tasks.{name}\" AssemblyFile=\"{Escape(builtIn)}\" />")));
            string aggregateSetup = aggregate ? $$"""
                <ItemGroup>
                  <BundledLayoutComponent Include="runtime">
                    <DownloadDestination>{{Escape(Path.Combine(root, "runtime.zip"))}}</DownloadDestination>
                  </BundledLayoutComponent>
                </ItemGroup>
                <Target Name="GenerateSdkLayout" />
                <Target Name="DownloadBundledComponents" />
                <Target Name="LayoutTemplates" />
                <Target Name="LayoutManifests" />
                <Target Name="LayoutBaselineWorkloadSet" />
                <Target Name="CrossgenLayout" />
                """ : """<Target Name="GenerateInstallerLayout" DependsOnTargets="LayoutDnxShim" />""";
            _project = WriteFile(Path.Combine(root, "layout.proj"), $$"""
                <Project>
                  {{usingTasks}}
                  <PropertyGroup>
                    <RedistInstallerLayoutPath>{{Escape(Layout)}}\</RedistInstallerLayoutPath>
                    <IntermediateOutputPath>{{Escape(Path.Combine(root, "state"))}}\</IntermediateOutputPath>
                    <ArtifactsObjDir>{{Escape(Path.Combine(root, "obj"))}}\</ArtifactsObjDir>
                    <Configuration>Debug</Configuration>
                    <ProductMonikerRid>test</ProductMonikerRid>
                    <CliProductBandVersion>11.0.1</CliProductBandVersion>
                    <InstallerOutputDirectory>{{Escape(Path.Combine(root, "payload"))}}\</InstallerOutputDirectory>
                    <Version>11.0.1</Version>
                    <OutputPath>{{Escape(Path.Combine(root, "sdk-output"))}}\</OutputPath>
                    <NuGetPackageRoot>{{Escape(Path.Combine(root, "packages"))}}\</NuGetPackageRoot>
                    <SkipBuildingInstallers>true</SkipBuildingInstallers>
                  </PropertyGroup>
                  <Import Project="{{Escape(copiedTargets)}}" />
                  {{aggregateSetup}}
                </Project>
                """);
        }

        public string Root { get; }
        public string Layout { get; }
        public string IntermediateLayout { get; }
        public string Completion(string family) => Path.Combine(Root, "state", "incremental-layout", family + "-test.complete");
        public string[] StateFiles(string family) =>
            [Completion(family), Path.ChangeExtension(Completion(family), ".inputs")];
        public string Write(string relative, string contents) =>
            WriteFile(Path.Combine(Layout, relative.Replace('/', Path.DirectorySeparatorChar)), contents);

        public Observation Build(string target, bool sourceOnly = false, string band = "11.0.1",
            bool interrupt = false, bool success = true, string? host = null, string? layout = null,
            string? observedTarget = null)
        {
            var properties = new Dictionary<string, string>
            {
                ["DotNetBuildSourceOnly"] = sourceOnly.ToString(),
                ["CliProductBandVersion"] = band,
                ["InterruptLayout"] = interrupt.ToString().ToLowerInvariant(),
                ["SimulatedHost"] = host ?? ""
            };
            if (layout is not null)
            {
                properties["RedistInstallerLayoutPath"] = layout + Path.DirectorySeparatorChar;
            }
            using var collection = new ProjectCollection(properties);
            ProjectInstance instance = collection.LoadProject(_project).CreateProjectInstance();
            var logger = new LayoutLogger(observedTarget ?? target);
            using var manager = new BuildManager();
            BuildResult result = manager.Build(
                new BuildParameters(collection) { Loggers = [logger] },
                new BuildRequestData(instance, [target], null, BuildRequestDataFlags.ProvideProjectStateAfterBuild));
            (result.OverallResult == BuildResultCode.Success).Should().Be(success, "MSBuild log:{0}{1}", Environment.NewLine, logger.Text);
            return new Observation(logger.Ran, result.ProjectStateAfterBuild?.GetItems("DnxShimSource").Count ?? 0);
        }
    }

    private sealed class LayoutLogger(string target) : Logger
    {
        private readonly List<string> _messages = [];
        public bool Ran { get; private set; }
        public string Text => string.Join(Environment.NewLine, _messages);

        public override void Initialize(IEventSource source)
        {
            source.TargetStarted += (_, args) => Ran |= args.TargetName == target;
            source.AnyEventRaised += (_, args) =>
            {
                if (args is TargetSkippedEventArgs skipped && skipped.TargetName == target)
                {
                    Ran = false;
                }
                if (args.Message is not null)
                {
                    _messages.Add(args.Message);
                }
            };
        }
    }
}
