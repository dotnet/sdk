// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
/// Tests shared file-based application build and launch planning.
/// </summary>
[TestClass]
public class FileBasedAppRunPlanTests
{
    /// <summary>Verifies that a fresh simple application selects direct compilation.</summary>
    [TestMethod]
    public void AnalyzeFreshSimpleAppSelectsCsc()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            string artifactsPath = Path.Join(testDirectory, "artifacts");
            File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
            (RunPlan plan, IReadOnlyList<string> messages) = CaptureVerboseMessages(
                () => FileBasedAppRunPlan.Analyze(CreateInputs(entryPointPath, artifactsPath)));

            Assert.AreEqual(RunTier.DirectCompile, plan.Tier);
            Assert.AreEqual(RunDecisionReason.DirectCompilationRequired, plan.Reason);
            Assert.IsNotNull(plan.Cache);
            Assert.Contains("cache file does not exist", messages.Single(static message => message.Contains("cache file", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies that a current synthetic cache selects cached launch.</summary>
    [TestMethod]
    public void AnalyzeCurrentSyntheticCacheSelectsNone()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            string artifactsPath = Path.Join(testDirectory, "artifacts");
            Directory.CreateDirectory(artifactsPath);
            File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
            var previousEntry = new RunFileBuildCacheEntry
            {
                BuildLevel = BuildLevel.Csc,
                SdkVersion = "11.0.100-test",
                RuntimeVersion = "11.0.0-test",
            };
            FileBasedAppRunPlan.CollectImplicitBuildFiles(
                new DirectoryInfo(testDirectory),
                previousEntry.ImplicitBuildFiles,
                out _);

            string startCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildStartCacheFileName);
            string successCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName);
            File.WriteAllText(startCachePath, entryPointPath);
            using (var stream = File.Create(successCachePath))
            {
                JsonSerializer.Serialize(stream, previousEntry, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }
            DateTime buildTimeUtc = DateTime.UtcNow.AddSeconds(-2);
            File.SetLastWriteTimeUtc(entryPointPath, buildTimeUtc.AddSeconds(-2));
            File.SetLastWriteTimeUtc(startCachePath, buildTimeUtc.AddSeconds(-1));
            File.SetLastWriteTimeUtc(successCachePath, buildTimeUtc);
            (RunPlan plan, IReadOnlyList<string> messages) = CaptureVerboseMessages(
                () => FileBasedAppRunPlan.Analyze(CreateInputs(entryPointPath, artifactsPath)));

            Assert.AreEqual(RunTier.CachedLaunch, plan.Tier);
            Assert.AreEqual(RunDecisionReason.CacheValid, plan.Reason);
            Assert.IsNotNull(plan.Cache?.PreviousEntry);
            Assert.Contains("output is up to date", messages.Single(static message => message.Contains("up to date", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies that an SDK mismatch disables auxiliary reuse.</summary>
    [TestMethod]
    public void AnalyzeVersionMismatchDisablesAuxiliaryReuse()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            string artifactsPath = Path.Join(testDirectory, "artifacts");
            Directory.CreateDirectory(artifactsPath);
            File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
            var previousEntry = new RunFileBuildCacheEntry
            {
                BuildLevel = BuildLevel.Csc,
                SdkVersion = "older-sdk",
                RuntimeVersion = "11.0.0-test",
            };
            WriteCacheFiles(entryPointPath, artifactsPath, previousEntry);
            (RunPlan plan, IReadOnlyList<string> messages) = CaptureVerboseMessages(
                () => FileBasedAppRunPlan.Analyze(CreateInputs(entryPointPath, artifactsPath)));

            Assert.AreEqual(RunTier.DirectCompile, plan.Tier);
            Assert.IsNotNull(plan.Cache);
            Assert.IsFalse(plan.Cache.DetermineFinalCanReuseAuxiliaryFiles());
            Assert.Contains("previous SDK version", messages.Single(message => message.Contains("SDK version", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies that disabling the cache does not resolve direct-compilation inputs.</summary>
    [TestMethod]
    public void AnalyzeNoCacheDoesNotResolveCscInputs()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
            FileBasedAppRunPlanInputs inputs = CreateInputs(
                entryPointPath,
                Path.Join(testDirectory, "artifacts")) with
            {
                NoCache = true,
                GetCscInputPaths = static () => throw new InvalidOperationException("CSC inputs should not be resolved."),
            };

            RunPlan plan = FileBasedAppRunPlan.Analyze(inputs);

            Assert.AreEqual(RunTier.MSBuildBuild, plan.Tier);
            Assert.AreEqual(RunDecisionReason.FullBuildRequired, plan.Reason);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies no-build synthetic launch selection does not prevalidate output, even after a source edit.</summary>
    [TestMethod]
    public void AnalyzeAotNoBuildSyntheticSelectsLaunchWithoutOutputAfterSourceChange()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            string artifactsPath = Path.Join(testDirectory, "artifacts");
            Directory.CreateDirectory(artifactsPath);
            File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
            var previousEntry = new RunFileBuildCacheEntry
            {
                BuildLevel = BuildLevel.Csc,
                SdkVersion = "11.0.100-test",
                RuntimeVersion = "11.0.0-test",
            };
            string successCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName);
            using (var stream = File.Create(successCachePath))
            {
                JsonSerializer.Serialize(stream, previousEntry, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }
            var launchArtifacts = FileBasedAppRunPlan.GetCscBuiltProgramLaunchArtifacts(entryPointPath, artifactsPath);
            Assert.IsFalse(File.Exists(launchArtifacts.AppHost));
            Assert.IsFalse(File.Exists(launchArtifacts.Assembly));
            Assert.IsFalse(File.Exists(launchArtifacts.RuntimeConfig));
            DateTime buildTimeUtc = DateTime.UtcNow.AddSeconds(-2);
            File.SetLastWriteTimeUtc(entryPointPath, buildTimeUtc.AddSeconds(-1));
            File.SetLastWriteTimeUtc(successCachePath, buildTimeUtc);

            RunPlan launchPlan = FileBasedAppRunPlan.AnalyzeAotNoBuildSynthetic(
                entryPointPath,
                artifactsPath);

            Assert.AreEqual(RunTier.LaunchOnly, launchPlan.Tier);
            Assert.AreEqual(RunDecisionReason.NoBuildSyntheticCache, launchPlan.Reason);
            Assert.AreEqual(launchArtifacts.AppHost, launchPlan.Launch?.Command);

            File.WriteAllText(entryPointPath, "#:package Example@1.0.0\nConsole.WriteLine(42);");
            File.SetLastWriteTimeUtc(entryPointPath, buildTimeUtc.AddSeconds(1));
            RunPlan changedSourcePlan = FileBasedAppRunPlan.AnalyzeAotNoBuildSynthetic(
                entryPointPath,
                artifactsPath);

            Assert.AreEqual(RunTier.LaunchOnly, changedSourcePlan.Tier);
            Assert.AreEqual(RunDecisionReason.NoBuildSyntheticCache, changedSourcePlan.Reason);
            Assert.AreEqual(launchArtifacts.AppHost, changedSourcePlan.Launch?.Command);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies that a valid authoritative cache returns its serialized run properties.</summary>
    [TestMethod]
    public void AnalyzeCachedLaunchReturnsValidatedRunProperties()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            string artifactsPath = Path.Join(testDirectory, "artifacts");
            Directory.CreateDirectory(artifactsPath);
            File.WriteAllText(entryPointPath, "#:package Example@1.0.0\nConsole.WriteLine(42);");
            string appHostPath = Path.Join(testDirectory, "custom", "Program.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(appHostPath)!);
            File.WriteAllText(appHostPath, string.Empty);
            var runProperties = new RunProperties(
                appHostPath,
                "cached-argument",
                testDirectory,
                "test-x64",
                "test-x64",
                "v11.0");
            var previousEntry = new RunFileBuildCacheEntry(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["NuGetInteractive"] = "true",
                })
            {
                Directives = ["#:package Example@1.0.0"],
                BuildLevel = BuildLevel.All,
                SdkVersion = "11.0.100-test",
                RuntimeVersion = "11.0.0-test",
                Run = runProperties,
            };
            WriteCacheFiles(entryPointPath, artifactsPath, previousEntry);

            RunPlan plan = FileBasedAppRunPlan.AnalyzeCachedLaunch(
                entryPointPath,
                artifactsPath,
                new Dictionary<string, string>(previousEntry.GlobalProperties, StringComparer.OrdinalIgnoreCase),
                previousEntry.SdkVersion!,
                previousEntry.RuntimeVersion!);

            Assert.AreEqual(RunTier.CachedLaunch, plan.Tier);
            Assert.AreEqual(RunDecisionReason.CacheValid, plan.Reason);
            Assert.AreEqual(runProperties, plan.Launch?.RunProperties);
            Assert.AreEqual(appHostPath, plan.Launch?.Command);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies that a changed source invalidates an authoritative cached launch.</summary>
    [TestMethod]
    public void AnalyzeCachedLaunchRejectsChangedSource()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            string entryPointPath = Path.Join(testDirectory, "Program.cs");
            string artifactsPath = Path.Join(testDirectory, "artifacts");
            Directory.CreateDirectory(artifactsPath);
            File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
            string appHostPath = Path.Join(testDirectory, "Program.exe");
            File.WriteAllText(appHostPath, string.Empty);
            var previousEntry = new RunFileBuildCacheEntry
            {
                BuildLevel = BuildLevel.All,
                SdkVersion = "11.0.100-test",
                RuntimeVersion = "11.0.0-test",
                Run = new RunProperties(appHostPath, null, testDirectory),
            };
            (_, string successCachePath) = WriteCacheFiles(entryPointPath, artifactsPath, previousEntry);
            File.SetLastWriteTimeUtc(entryPointPath, File.GetLastWriteTimeUtc(successCachePath).AddSeconds(1));

            RunPlan plan = FileBasedAppRunPlan.AnalyzeCachedLaunch(
                entryPointPath,
                artifactsPath,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                previousEntry.SdkVersion!,
                previousEntry.RuntimeVersion!);

            Assert.AreEqual(RunTier.ManagedFallback, plan.Tier);
            Assert.AreEqual(RunDecisionReason.CachedLaunchNotEligible, plan.Reason);
            Assert.IsNull(plan.Launch);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    /// <summary>Verifies auxiliary-file reuse decisions and diagnostics.</summary>
    [TestMethod]
    public void CacheInfoReportsAuxiliaryFileReuseDecision()
    {
        var cache = new FileBasedAppCacheInfo
        {
            EntryPointFile = new FileInfo("Program.cs"),
            CurrentEntry = new RunFileBuildCacheEntry(),
        };
        (bool canReuse, IReadOnlyList<string> messages) = CaptureVerboseMessages(cache.DetermineFinalCanReuseAuxiliaryFiles);
        Assert.IsFalse(canReuse);
        Assert.Contains("previous build level was not CSC", messages.Single());

        cache.PreviousEntry = new RunFileBuildCacheEntry { BuildLevel = BuildLevel.Csc };
        (canReuse, messages) = CaptureVerboseMessages(cache.DetermineFinalCanReuseAuxiliaryFiles);
        Assert.IsTrue(canReuse);
        Assert.Contains("can be reused", messages.Single());

        cache.InitialCanReuseAuxiliaryFiles = false;
        (canReuse, messages) = CaptureVerboseMessages(cache.DetermineFinalCanReuseAuxiliaryFiles);
        Assert.IsFalse(canReuse);
        Assert.Contains("same reason build is needed", messages.Single());
    }

    /// <summary>Verifies cache serialization and the required dictionary and path comparers.</summary>
    [TestMethod]
    public void CacheEntryRoundTripsWithExpectedComparers()
    {
        var entry = new RunFileBuildCacheEntry(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = "Release",
            })
        {
            Directives = ["#:property PublishAot=false"],
            BuildLevel = BuildLevel.Csc,
            SdkVersion = "11.0.100-test",
            RuntimeVersion = "11.0.0-test",
            Run = new RunProperties(
                "apphost",
                "arg1",
                "working-directory",
                "test-x64",
                "test-x64",
                "v11.0"),
            CscArguments = ["/nologo", "/target:exe"],
            BuildResultFile = "bin/Program.dll",
        };
        entry.ImplicitBuildFiles.Add("Directory.Build.props");
        entry.AdditionalSources.Add("Additional.cs");

        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, entry, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
        stream.Position = 0;
        RunFileBuildCacheEntry? roundTripped = JsonSerializer.Deserialize(
            stream,
            RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual("Release", roundTripped.GlobalProperties["configuration"]);
        Assert.Contains("Directory.Build.props", roundTripped.ImplicitBuildFiles);
        Assert.DoesNotContain("directory.build.props", roundTripped.ImplicitBuildFiles);
        Assert.Contains("Additional.cs", roundTripped.AdditionalSources);
        Assert.IsTrue(entry.Directives.SequenceEqual(roundTripped.Directives));
        Assert.AreEqual(entry.BuildLevel, roundTripped.BuildLevel);
        Assert.AreEqual(entry.SdkVersion, roundTripped.SdkVersion);
        Assert.AreEqual(entry.RuntimeVersion, roundTripped.RuntimeVersion);
        Assert.AreEqual(entry.Run, roundTripped.Run);
        Assert.IsTrue(entry.CscArguments.SequenceEqual(roundTripped.CscArguments));
        Assert.AreEqual(entry.BuildResultFile, roundTripped.BuildResultFile);
    }

    private static FileBasedAppRunPlanInputs CreateInputs(string entryPointPath, string artifactsPath)
        => new(
            EntryPointFileFullPath: entryPointPath,
            ArtifactsPath: artifactsPath,
            GlobalProperties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            CanCache: true,
            Directives: [],
            SdkVersion: "11.0.100-test",
            RuntimeVersion: "11.0.0-test",
            NoCache: false,
            GetCscInputPaths: static () => []);

    private static (string StartCachePath, string SuccessCachePath) WriteCacheFiles(
        string entryPointPath,
        string artifactsPath,
        RunFileBuildCacheEntry entry)
    {
        string startCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildStartCacheFileName);
        string successCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName);
        File.WriteAllText(startCachePath, entryPointPath);
        using (var stream = File.Create(successCachePath))
        {
            JsonSerializer.Serialize(stream, entry, RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
        }

        DateTime buildTimeUtc = DateTime.UtcNow.AddSeconds(-2);
        File.SetLastWriteTimeUtc(entryPointPath, buildTimeUtc.AddSeconds(-2));
        File.SetLastWriteTimeUtc(startCachePath, buildTimeUtc.AddSeconds(-1));
        File.SetLastWriteTimeUtc(successCachePath, buildTimeUtc);
        return (startCachePath, successCachePath);
    }

    private static (T Result, IReadOnlyList<string> Messages) CaptureVerboseMessages<T>(Func<T> action)
    {
        bool originalVerbose = CommandLoggingContext.IsVerbose;
        var reporter = new BufferedReporter();
        try
        {
            CommandLoggingContext.SetVerbose(true);
            Reporter.SetVerbose(reporter);
            return (action(), reporter.Lines.ToArray());
        }
        finally
        {
            Reporter.SetVerbose(Reporter.ConsoleOutReporter);
            CommandLoggingContext.SetVerbose(originalVerbose);
            Reporter.Reset();
        }
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Join(Path.GetTempPath(), $"dotnet-aot-run-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
