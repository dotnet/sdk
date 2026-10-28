// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class WindowsNativeTarArchiveExtractorTests
{
    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void NativeTarProcessRunner_ExposesExitCodeAndStandardError()
    {
        var runner = new NativeTarProcessRunner();

        NativeTarProcessResult result = runner.Run(
            "cmd.exe",
            ["/d", "/c", "echo process failure 1>&2 & exit /b 7"]);

        result.ExitCode.Should().Be(7);
        result.StandardError.Should().Contain("process failure");
        result.StartFailure.Should().BeNull();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetTarExecutable_UsesSystemCopyBeforeWorkingDirectory(bool useSysnative)
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string windowsDirectory = Path.Combine(testEnv.TempRoot, "Windows");
        string systemDirectory = Path.Combine(windowsDirectory, "System32");
        string trustedDirectory = useSysnative
            ? Path.Combine(windowsDirectory, "Sysnative")
            : systemDirectory;
        Directory.CreateDirectory(trustedDirectory);
        string trustedTar = Path.Combine(trustedDirectory, "tar.exe");
        File.WriteAllText(trustedTar, "system copy");
        string workingDirectory = Path.Combine(testEnv.TempRoot, "working");
        Directory.CreateDirectory(workingDirectory);
        File.WriteAllText(Path.Combine(workingDirectory, "tar.exe"), "working directory copy");

        WindowsNativeTarArchiveExtractor.GetTarExecutable(systemDirectory, windowsDirectory, useSysnative)
            .Should().Be(trustedTar);

        File.Delete(trustedTar);
        WindowsNativeTarArchiveExtractor.GetTarExecutable(systemDirectory, windowsDirectory, useSysnative)
            .Should().Be("tar.exe", "machines without the system copy still use PATH and managed fallback");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Extract_UsesWindowsTarForGzipArchive()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string archivePath = Path.Combine(testEnv.TempRoot, "sdk archive.tar.gz");
        using (FileStream archive = File.Create(archivePath))
        using (var gzip = new GZipStream(archive, CompressionLevel.Fastest))
        using (var writer = new TarWriter(gzip))
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "./sdk/11.0.100/sdk.dll")
            {
                DataStream = new MemoryStream("sdk content"u8.ToArray()),
            });
        }

        var fallback = new RecordingTarExtractor();
        var extractor = new WindowsNativeTarArchiveExtractor(fallback);

        extractor.Extract(new TarExtractionContext(archivePath, testEnv.InstallPath));

        fallback.WasCalled.Should().BeFalse();
        File.ReadAllText(Path.Combine(testEnv.InstallPath, "sdk", "11.0.100", "sdk.dll"))
            .Should().Be("sdk content");
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Extract_UsesWindowsTarForUncompressedArchive()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string archivePath = Path.Combine(testEnv.TempRoot, "sdk archive.tar");
        using (FileStream archive = File.Create(archivePath))
        using (var writer = new TarWriter(archive))
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "./sdk/11.0.100/sdk.dll")
            {
                DataStream = new MemoryStream("sdk content"u8.ToArray()),
            });
        }

        var fallback = new RecordingTarExtractor();
        var extractor = new WindowsNativeTarArchiveExtractor(fallback);

        extractor.Extract(new TarExtractionContext(archivePath, testEnv.InstallPath));

        fallback.WasCalled.Should().BeFalse();
        File.ReadAllText(Path.Combine(testEnv.InstallPath, "sdk", "11.0.100", "sdk.dll"))
            .Should().Be("sdk content");
    }

    [TestMethod]
    public void Extract_CommitsVersionDirectoriesAndPreservesHardlinks()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string targetDirectory = Path.Combine(testEnv.TempRoot, "target with spaces \u2603");
        string? stagingDirectory = null;

        var runner = new CallbackTarProcessRunner((executable, arguments) =>
        {
            executable.Should().Be(WindowsNativeTarArchiveExtractor.GetTarExecutable(
                Environment.SystemDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess));
            arguments.Should().ContainInOrder("-xzf", "archive.tar.gz", "-C");
            stagingDirectory = GetStagingDirectory(arguments);
            Path.GetDirectoryName(stagingDirectory).Should().Be(Path.GetFullPath(targetDirectory));

            string sdkDirectory = Path.Combine(stagingDirectory, "sdk", "11.0.100");
            Directory.CreateDirectory(sdkDirectory);
            string original = Path.Combine(sdkDirectory, "original.dll");
            File.WriteAllText(original, "original");
            File.CreateHardLink(Path.Combine(sdkDirectory, "linked.dll"), original);

            Directory.CreateDirectory(Path.Combine(stagingDirectory, "metadata"));
            File.WriteAllText(Path.Combine(stagingDirectory, "metadata", "workloads.json"), "metadata");
            File.WriteAllText(Path.Combine(stagingDirectory, "LICENSE.txt"), "license");
            return new NativeTarProcessResult(0, string.Empty);
        });
        var fallback = new RecordingTarExtractor();
        var extractedEntries = new List<string>();
        var extractor = new WindowsNativeTarArchiveExtractor(fallback, runner);

        extractor.Extract(new TarExtractionContext(
            "archive.tar.gz",
            targetDirectory,
            OnEntryExtracted: extractedEntries.Add));

        fallback.WasCalled.Should().BeFalse();
        extractedEntries.Should().Contain("sdk/11.0.100/");
        File.ReadAllText(Path.Combine(targetDirectory, "metadata", "workloads.json")).Should().Be("metadata");
        File.ReadAllText(Path.Combine(targetDirectory, "LICENSE.txt")).Should().Be("license");

        string installedOriginal = Path.Combine(targetDirectory, "sdk", "11.0.100", "original.dll");
        string installedLink = Path.Combine(targetDirectory, "sdk", "11.0.100", "linked.dll");
        File.WriteAllText(installedOriginal, "changed");
        File.ReadAllText(installedLink).Should().Be("changed", "same-volume directory moves must preserve hardlinks");
        Directory.Exists(stagingDirectory).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_NativeFailureFallsBackBeforeTargetIsModified()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string targetDirectory = Path.Combine(testEnv.TempRoot, "target");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "existing.txt"), "existing");
        string? stagingDirectory = null;

        var runner = new CallbackTarProcessRunner((_, arguments) =>
        {
            stagingDirectory = GetStagingDirectory(arguments);
            Path.GetDirectoryName(stagingDirectory).Should().Be(Path.GetFullPath(targetDirectory));
            Directory.CreateDirectory(Path.Combine(stagingDirectory, "sdk", "11.0.100"));
            File.WriteAllText(Path.Combine(stagingDirectory, "sdk", "11.0.100", "partial.dll"), "partial");
            return new NativeTarProcessResult(2, "archive read failed");
        });
        var fallback = new RecordingTarExtractor(context =>
        {
            File.Exists(Path.Combine(context.TargetDirectory, "sdk", "11.0.100", "partial.dll"))
                .Should().BeFalse("native extraction must not mutate the live root before fallback");
            File.WriteAllText(Path.Combine(context.TargetDirectory, "fallback.txt"), "fallback");
        });
        var extractor = new WindowsNativeTarArchiveExtractor(fallback, runner);

        extractor.Extract(new TarExtractionContext("archive.tar.gz", targetDirectory));

        fallback.WasCalled.Should().BeTrue();
        File.ReadAllText(Path.Combine(targetDirectory, "existing.txt")).Should().Be("existing");
        File.ReadAllText(Path.Combine(targetDirectory, "fallback.txt")).Should().Be("fallback");
        File.Exists(Path.Combine(targetDirectory, "sdk", "11.0.100", "partial.dll")).Should().BeFalse();
        Directory.Exists(stagingDirectory).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_WhenNativeTarIsUnavailableUsesFallback()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var runner = new CallbackTarProcessRunner((_, _) =>
            new NativeTarProcessResult(
                ExitCode: null,
                StandardError: string.Empty,
                StartFailure: new FileNotFoundException("tar.exe was not found")));
        var fallback = new RecordingTarExtractor(context =>
            File.WriteAllText(Path.Combine(context.TargetDirectory, "fallback.txt"), "fallback"));
        var extractor = new WindowsNativeTarArchiveExtractor(fallback, runner);

        extractor.Extract(new TarExtractionContext("archive.tar.gz", testEnv.InstallPath));

        fallback.WasCalled.Should().BeTrue();
        File.Exists(Path.Combine(testEnv.InstallPath, "fallback.txt")).Should().BeTrue();
    }

    [TestMethod]
    public void Extract_WhenBothBackendsFailPreservesBothFailures()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var runner = new CallbackTarProcessRunner((_, _) =>
            new NativeTarProcessResult(7, "native failure"));
        var fallback = new RecordingTarExtractor(_ => throw new InvalidDataException("managed failure"));
        var extractor = new WindowsNativeTarArchiveExtractor(fallback, runner);

        AggregateException exception = Assert.ThrowsExactly<AggregateException>(() =>
            extractor.Extract(new TarExtractionContext("archive.tar.gz", testEnv.InstallPath)));

        exception.InnerExceptions.Should().HaveCount(2);
        exception.InnerExceptions[0].Message.Should().Contain("exit").And.Contain("7").And.Contain("native failure");
        exception.InnerExceptions[1].Message.Should().Contain("managed failure");
    }

    [TestMethod]
    public void Extract_PreservesExistingSubcomponent()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string targetDirectory = Path.Combine(testEnv.TempRoot, "target");
        string existingSdk = Path.Combine(targetDirectory, "sdk", "11.0.100");
        Directory.CreateDirectory(existingSdk);
        File.WriteAllText(Path.Combine(existingSdk, "existing.dll"), "existing");

        var runner = new CallbackTarProcessRunner((_, arguments) =>
        {
            string stagedSdk = Path.Combine(GetStagingDirectory(arguments), "sdk", "11.0.100");
            Directory.CreateDirectory(stagedSdk);
            File.WriteAllText(Path.Combine(stagedSdk, "replacement.dll"), "replacement");
            return new NativeTarProcessResult(0, string.Empty);
        });
        var trackedEntries = new List<string>();
        var extractor = new WindowsNativeTarArchiveExtractor(new RecordingTarExtractor(), runner);

        extractor.Extract(new TarExtractionContext(
            "archive.tar.gz",
            targetDirectory,
            OnEntryExtracted: trackedEntries.Add,
            ShouldSkipEntry: entry =>
            {
                string? subcomponent = SubcomponentResolver.Resolve(entry);
                return subcomponent is not null &&
                    Directory.Exists(Path.Combine(targetDirectory, subcomponent.Replace('/', Path.DirectorySeparatorChar)));
            }));

        trackedEntries.Should().Contain("sdk/11.0.100/");
        File.ReadAllText(Path.Combine(existingSdk, "existing.dll")).Should().Be("existing");
        File.Exists(Path.Combine(existingSdk, "replacement.dll")).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_UsesExistingMuxerReplacementPolicy()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string targetDirectory = Path.Combine(testEnv.TempRoot, "target");
        Directory.CreateDirectory(Path.Combine(targetDirectory, "shared", "Microsoft.NETCore.App", "10.0.0"));
        string muxerName = MuxerHandler.MuxerEntryName;
        File.WriteAllText(Path.Combine(targetDirectory, muxerName), "old muxer");
        var muxerHandler = new MuxerHandler(targetDirectory);

        var runner = new CallbackTarProcessRunner((_, arguments) =>
        {
            string stagingDirectory = GetStagingDirectory(arguments);
            Directory.CreateDirectory(Path.Combine(stagingDirectory, "shared", "Microsoft.NETCore.App", "11.0.0"));
            File.WriteAllText(
                Path.Combine(stagingDirectory, "shared", "Microsoft.NETCore.App", "11.0.0", "hostpolicy.dll"),
                "runtime");
            File.WriteAllText(Path.Combine(stagingDirectory, muxerName), "new muxer");
            return new NativeTarProcessResult(0, string.Empty);
        });
        var extractor = new WindowsNativeTarArchiveExtractor(new RecordingTarExtractor(), runner);

        extractor.Extract(new TarExtractionContext(
            "archive.tar.gz",
            targetDirectory,
            MuxerHandler: muxerHandler));
        muxerHandler.FinalizeAfterExtraction();

        File.ReadAllText(Path.Combine(targetDirectory, muxerName)).Should().Be("new muxer");
    }

    private static string GetStagingDirectory(IReadOnlyList<string> arguments)
    {
        int destinationArgument = -1;
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] == "-C")
            {
                destinationArgument = i;
                break;
            }
        }
        destinationArgument.Should().BeGreaterThanOrEqualTo(0);
        return arguments[destinationArgument + 1];
    }

    private sealed class CallbackTarProcessRunner(
        Func<string, IReadOnlyList<string>, NativeTarProcessResult> callback) : INativeTarProcessRunner
    {
        public NativeTarProcessResult Run(string executable, IReadOnlyList<string> arguments)
            => callback(executable, arguments);
    }

    private sealed class RecordingTarExtractor(Action<TarExtractionContext>? onExtract = null) : ITarArchiveExtractor
    {
        public bool WasCalled { get; private set; }

        public void Extract(TarExtractionContext context)
        {
            WasCalled = true;
            onExtract?.Invoke(context);
        }
    }
}
