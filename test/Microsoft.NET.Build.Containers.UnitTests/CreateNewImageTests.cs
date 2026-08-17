// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class CreateNewImageTests
{
    private const string BaseManifestDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public void ArchiveIncrementalFingerprintTracksInputs()
    {
        string publishDirectory = CreateTempDirectory();
        try
        {
            string publishedFile = Path.Combine(publishDirectory, "app.dll");
            File.WriteAllText(publishedFile, "first");

            CreateNewImage task = CreateIncrementalTask(publishDirectory);
            string initial = ComputeResolvedFingerprint(task);

            File.WriteAllText(publishedFile, "second");
            string contentChanged = ComputeResolvedFingerprint(task);
            Assert.AreNotEqual(initial, contentChanged);

            File.WriteAllText(publishedFile, "first");
            task.WorkingDirectory = "/changed";
            string configurationChanged = ComputeResolvedFingerprint(task);
            Assert.AreNotEqual(initial, configurationChanged);

            task.WorkingDirectory = "/app";
            const string changedBaseManifestDigest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
            string baseImageChanged = ContainerArchiveCache.ComputeFingerprint(
                task,
                changedBaseManifestDigest,
                baseImageIsResolved: true,
                TestContext.CancellationToken);
            Assert.AreNotEqual(initial, baseImageChanged);
        }
        finally
        {
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ArchiveIncrementalFingerprintTracksLabels()
    {
        string publishDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(publishDirectory, "app.dll"), "content");
            CreateNewImage task = CreateIncrementalTask(publishDirectory);
            task.GenerateLabels = true;
            TaskItem createdLabel = new("org.opencontainers.image.created");
            createdLabel.SetMetadata("Value", "first");
            task.Labels = [createdLabel];

            string initial = ComputeResolvedFingerprint(task);
            createdLabel.SetMetadata("Value", "second");

            Assert.AreNotEqual(
                initial,
                ComputeResolvedFingerprint(task));
        }
        finally
        {
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ArchiveIncrementalFingerprintIgnoresDisabledLabels()
    {
        string publishDirectory = CreateTempDirectory();
        try
        {
            CreateNewImage task = CreateIncrementalTask(publishDirectory);
            task.GenerateLabels = false;
            task.GenerateCreatedLabels = true;
            task.GenerateDigestLabel = true;
            TaskItem label = new("label");
            label.SetMetadata("Value", "first");
            task.Labels = [label];
            string initial = ComputeResolvedFingerprint(task);

            task.GenerateCreatedLabels = false;
            task.GenerateDigestLabel = false;
            label.SetMetadata("Value", "second");

            Assert.AreEqual(initial, ComputeResolvedFingerprint(task));
        }
        finally
        {
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ResolvedBaseFingerprintIgnoresReferenceSelectors()
    {
        string rootDirectory = CreateTempDirectory();
        try
        {
            string publishDirectory = Path.Combine(rootDirectory, "publish");
            string runtimeIdentifierGraph = Path.Combine(rootDirectory, "runtime.json");
            Directory.CreateDirectory(publishDirectory);
            File.WriteAllText(runtimeIdentifierGraph, "{}");
            CreateNewImage task = CreateIncrementalTask(publishDirectory, runtimeIdentifierGraph);
            string initial = ComputeResolvedFingerprint(task);

            task.BaseRegistry = "example.invalid";
            task.BaseImageName = "different/image";
            task.BaseImageTag = "different";
            task.BaseImageDigest = "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
            task.ContainerRuntimeIdentifier = "linux-arm64";
            File.WriteAllText(task.RuntimeIdentifierGraphPath, """{"different":true}""");

            Assert.AreEqual(initial, ComputeResolvedFingerprint(task));
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ArchiveIncrementalFingerprintIsStableAcrossDirectories()
    {
        string rootDirectory = CreateTempDirectory();
        try
        {
            string firstPublishDirectory = Path.Combine(rootDirectory, "first", "publish");
            string secondPublishDirectory = Path.Combine(rootDirectory, "second", "publish");
            string firstGraph = Path.Combine(rootDirectory, "first", "runtime.json");
            string secondGraph = Path.Combine(rootDirectory, "second", "runtime.json");
            Directory.CreateDirectory(firstPublishDirectory);
            Directory.CreateDirectory(secondPublishDirectory);
            File.WriteAllText(Path.Combine(firstPublishDirectory, "app.dll"), "content");
            File.WriteAllText(Path.Combine(secondPublishDirectory, "app.dll"), "content");
            File.WriteAllText(firstGraph, "{}");
            File.WriteAllText(secondGraph, "{}");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(firstGraph, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.SetUnixFileMode(secondGraph, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
            CreateNewImage firstTask = CreateIncrementalTask(firstPublishDirectory, firstGraph);
            CreateNewImage secondTask = CreateIncrementalTask(secondPublishDirectory, secondGraph);

            string first = ContainerArchiveCache.ComputeFingerprint(
                firstTask,
                BaseManifestDigest,
                baseImageIsResolved: false,
                TestContext.CancellationToken);
            string second = ContainerArchiveCache.ComputeFingerprint(
                secondTask,
                BaseManifestDigest,
                baseImageIsResolved: false,
                TestContext.CancellationToken);

            Assert.AreEqual(first, second);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ArchiveIncrementalFingerprintHonorsCancellation()
    {
        string publishDirectory = CreateTempDirectory();
        try
        {
            CreateNewImage task = CreateIncrementalTask(publishDirectory);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(
                () => ContainerArchiveCache.ComputeFingerprint(
                    task,
                    BaseManifestDigest,
                    baseImageIsResolved: true,
                    cancellation.Token));
        }
        finally
        {
            Directory.Delete(publishDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async System.Threading.Tasks.Task ArchiveIncrementalCacheRestoresOutputsBeforeResolvingBaseImage()
    {
        string tempDirectory = CreateTempDirectory();
        try
        {
            string publishDirectory = Path.Combine(tempDirectory, "publish");
            Directory.CreateDirectory(publishDirectory);
            File.WriteAllText(Path.Combine(publishDirectory, "app.dll"), "content");
            string archivePath = Path.Combine(tempDirectory, "archives");
            string resolvedArchivePath = Path.Combine(archivePath, "test.tar.gz");
            string cachePath = Path.Combine(tempDirectory, "obj", "publish.cache.json");
            Directory.CreateDirectory(archivePath);
            File.WriteAllText(resolvedArchivePath, "archive");

            CreateNewImage original = CreateIncrementalTask(publishDirectory);
            original.BaseRegistry = "invalid.example";
            original.BaseImageDigest = BaseManifestDigest;
            original.ArchiveOutputPath = archivePath;
            original.ArchiveIncrementalCachePath = cachePath;
            original.GeneratedContainerManifest = "manifest";
            original.GeneratedContainerConfiguration = "configuration";
            original.GeneratedContainerDigest = "sha256:digest";
            original.GeneratedContainerMediaType = "application/vnd.oci.image.manifest.v1+json";
            original.GeneratedContainerNames = [new TaskItem("test:latest")];
            string fingerprint = ContainerArchiveCache.ComputeFingerprint(
                original,
                BaseManifestDigest,
                baseImageIsResolved: false,
                TestContext.CancellationToken);
            ContainerArchiveCache.Save(original, fingerprint);
            DateTime archiveWriteTime = File.GetLastWriteTimeUtc(resolvedArchivePath);

            CreateNewImage cached = CreateIncrementalTask(publishDirectory);
            cached.BaseRegistry = "invalid.example";
            cached.BaseImageDigest = BaseManifestDigest;
            cached.ArchiveOutputPath = archivePath;
            cached.ArchiveIncrementalCachePath = cachePath;
            cached.EnableArchiveIncrementalCache = true;
            cached.BuildEngine = new Mock<IBuildEngine>().Object;

            Assert.IsTrue(await cached.ExecuteAsync(CancellationToken.None));
            Assert.AreEqual("manifest", cached.GeneratedContainerManifest);
            Assert.AreEqual("configuration", cached.GeneratedContainerConfiguration);
            Assert.AreEqual("sha256:digest", cached.GeneratedContainerDigest);
            Assert.AreEqual("test:latest", cached.GeneratedContainerNames.Single().ItemSpec);
            Assert.AreEqual(archiveWriteTime, File.GetLastWriteTimeUtc(resolvedArchivePath));

            File.WriteAllText(resolvedArchivePath, "different archive");
            Assert.IsFalse(ContainerArchiveCache.TryRestore(cached, fingerprint));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("1636374896", 1636374896L)]
    [DataRow("99999999999", 99999999999L)]
    [DataRow("100000000000", 100000000000L)]
    public void ParseSourceDateEpochReturnsUtcTimestamp(string value, long expectedSeconds)
    {
        DateTime? actual = CreateNewImage.ParseSourceDateEpoch(value);

        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(expectedSeconds).UtcDateTime, actual);
        Assert.AreEqual(DateTimeKind.Utc, actual!.Value.Kind);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "unset")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("   ", DisplayName = "whitespace")]
    [DataRow("  1636374896\n", DisplayName = "surrounding whitespace")]
    [DataRow("not-a-number", DisplayName = "not numeric")]
    [DataRow("1636374896.5", DisplayName = "fractional")]
    [DataRow("-1", DisplayName = "negative")]
    [DataRow("0x10", DisplayName = "hexadecimal")]
    [DataRow("1,636,374,896", DisplayName = "group separators")]
    [DataRow("99999999999999999999", DisplayName = "larger than Int64")]
    [DataRow("253402300800", DisplayName = "outside DateTimeOffset's range")]
    public void ParseSourceDateEpochReturnsNullForInvalidValues(string? value)
        => Assert.IsNull(CreateNewImage.ParseSourceDateEpoch(value));

    [TestMethod]
    // Entrypoint, backwards compatibility.
    [DataRow("", "entrypointArg", "appCommand", "", "", null, new[] { "appCommand" }, new[] { "entrypointArg" })]
    // When no entrypoint is specified, emit the AppCommand as the Entrypoint.
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "appCommand", "appCommandArgs" }, new[] { "defaultArgs" })]
    // Set all properties. When an entrypoint is specified, emit the AppCommand as Cmd.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs",
                "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    public void EntrypointAndCmd_NoInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs",
                                                                       "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, base entrypoint is preserved.
    [DataRow("", "", "appCommand", "", "", "", null, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "", null, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    [DataRow("", "", "appCommand", "", "", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, 'dotnet' base entrypoint is ignored.
    [DataRow("", "", "appCommand", "", "", "dotnet", null, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "dotnet", null, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "dotnet", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, '/usr/bin/dotnet' base entrypoint is ignored.
    [DataRow("", "", "appCommand", "", "", "/usr/bin/dotnet", null, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "/usr/bin/dotnet", null, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "/usr/bin/dotnet", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    public void EntrypointAndCmd_DefaultArgsInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("DefaultArgs", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties except entrypoint and entrypointArgs.
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "appCommand", "appCommandArgs" }, new[] { "defaultArgs" })]
    // Can't set entrypoint or entrypointArgs with instruction 'Entrypoint'.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("entrypoint", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_EntrypointInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("Entrypoint", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties except appCommand and appCommandArgs.
    [DataRow("entrypoint", "entrypointArgs", "", "", "defaultArgs", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "defaultArgs" })]
    // Can't set appCommand or appCommandArgs with instruction 'None'.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("entrypoint", "entrypointArgs", "", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_NoneInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("None", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties accepted.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // Set all properties except entrypoint fails: can't set entrypointArgs without setting entrypoint.
    [DataRow("", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    // Set all properties except appCommand fails: can't set appCommandArgs without setting appCommand.
    [DataRow("entrypoint", "entrypointArgs", "", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_RequiredProperties(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("DefaultArgs", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    private static void ValidateArgsAndCmd(string appCommandInstruction, string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
    {
        var newImage = new CreateNewImage()
        {
            Entrypoint = CreateTaskItems(entrypoint),
            EntrypointArgs = CreateTaskItems(entrypointArgs),
            DefaultArgs = CreateTaskItems(defaultArgs),
            AppCommand = CreateTaskItems(appCommand),
            AppCommandArgs = CreateTaskItems(appCommandArgs),
            AppCommandInstruction = appCommandInstruction,
            BuildEngine = new Mock<IBuildEngine>().Object
        };

        (string[] imageEntrypoint, string[] imageCmd) = newImage.DetermineEntrypointAndCmd(baseImageEntrypoint?.Split(';', StringSplitOptions.RemoveEmptyEntries));

        Assert.AreEqual(newImage.Log.HasLoggedErrors, imageEntrypoint.Length == 0 && imageCmd.Length == 0);
        Assert.AreSequenceEqual(expectedEntrypoint ?? Array.Empty<string>(), imageEntrypoint);
        Assert.AreSequenceEqual(expectedCmd ?? Array.Empty<string>(), imageCmd);

        static ITaskItem[] CreateTaskItems(string value)
            => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => new TaskItem(s)).ToArray();
    }

    private string ComputeResolvedFingerprint(CreateNewImage task)
        => ContainerArchiveCache.ComputeFingerprint(
            task,
            BaseManifestDigest,
            baseImageIsResolved: true,
            TestContext.CancellationToken);

    private static CreateNewImage CreateIncrementalTask(string publishDirectory, string? runtimeIdentifierGraph = null)
    {
        runtimeIdentifierGraph ??= Path.Combine(publishDirectory, "runtime.json");
        if (!File.Exists(runtimeIdentifierGraph))
        {
            File.WriteAllText(runtimeIdentifierGraph, "{}");
        }
        return new CreateNewImage
        {
            BaseRegistry = "mcr.microsoft.com",
            BaseImageName = "dotnet/runtime",
            BaseImageTag = "latest",
            Repository = "test",
            ImageTags = ["latest"],
            PublishDirectory = publishDirectory,
            WorkingDirectory = "/app",
            ContainerRuntimeIdentifier = "linux-x64",
            RuntimeIdentifierGraphPath = runtimeIdentifierGraph,
        };
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }
}
