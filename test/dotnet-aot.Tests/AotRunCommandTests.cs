// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
/// Tests Native AOT file-based run planning and launch invocation construction.
/// </summary>
[TestClass]
public class AotRunCommandTests
{
    /// <summary>Verifies that an explicit no-build invocation launches complete synthetic output.</summary>
    [TestMethod]
    public void EligibleSyntheticNoBuildProducesLaunchInvocation()
    {
        var fixture = CreateFixture();
        File.WriteAllText(Path.Join(fixture.TestDirectory, "App.csproj"), "<Project />");
        string? originalDotnetRoot = NativeEntryPoint.DotnetRoot;
        string? rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
            RuntimeInformation.RuntimeIdentifier,
            RuntimeInformation.RuntimeIdentifier,
            $"v{Product.TargetFrameworkVersion}");
        string? originalRootVariable = rootVariableName is null ? null : Environment.GetEnvironmentVariable(rootVariableName);
        try
        {
            NativeEntryPoint.DotnetRoot = fixture.TestDirectory;
            if (rootVariableName is not null)
            {
                Environment.SetEnvironmentVariable(rootVariableName, null);
            }
            DateTime oldArtifactsTime = DateTime.UtcNow.AddDays(-1);
            Directory.SetLastWriteTimeUtc(fixture.ArtifactsPath, oldArtifactsTime);
            var parseResult = Parser.Parse([
                "run",
                "--file", fixture.EntryPointPath,
                "--no-build",
                "--no-launch-profile",
                "-e", "TEST_AOT_RUN=value",
                "--", "arg one", "--flag",
            ]);
            AotRunInvocation? invocation = null;

            int exitCode = AotRunCommand.Execute(
                parseResult,
                value =>
                {
                    invocation = value;
                    return 17;
                },
                fixture.TestDirectory);

            Assert.AreEqual(17, exitCode);
            Assert.IsNotNull(invocation);
            Assert.AreEqual(fixture.LaunchArtifacts.AppHost, invocation.Command);
            Assert.AreEqual("\"arg one\" --flag", invocation.CommandArguments);
            Assert.AreEqual("value", invocation.EnvironmentVariables["TEST_AOT_RUN"]);
            if (rootVariableName is not null)
            {
                Assert.AreEqual(fixture.TestDirectory, invocation.EnvironmentVariables[rootVariableName]);
            }
            Assert.AreEqual(fixture.TestDirectory, invocation.WorkingDirectory);
            Assert.IsGreaterThan(oldArtifactsTime, Directory.GetLastWriteTimeUtc(fixture.ArtifactsPath));
        }
        finally
        {
            NativeEntryPoint.DotnetRoot = originalDotnetRoot;
            if (rootVariableName is not null)
            {
                Environment.SetEnvironmentVariable(rootVariableName, originalRootVariable);
            }
            DeleteFixture(fixture);
        }
    }

    /// <summary>Verifies that a positional no-build invocation launches complete synthetic output.</summary>
    [TestMethod]
    public void EligiblePositionalNoBuildProducesLaunchInvocation()
    {
        var fixture = CreateFixture();
        string? originalDotnetRoot = NativeEntryPoint.DotnetRoot;
        try
        {
            NativeEntryPoint.DotnetRoot = fixture.TestDirectory;
            var parseResult = Parser.Parse([
                "run",
                fixture.EntryPointPath,
                "--no-build",
                "--no-launch-profile",
                "--", "arg one", "--flag",
            ]);
            AotRunInvocation? invocation = null;

            int exitCode = AotRunCommand.Execute(
                parseResult,
                value =>
                {
                    invocation = value;
                    return 17;
                },
                fixture.TestDirectory);

            Assert.AreEqual(17, exitCode);
            Assert.IsNotNull(invocation);
            Assert.AreEqual(fixture.LaunchArtifacts.AppHost, invocation.Command);
            Assert.AreEqual("\"arg one\" --flag", invocation.CommandArguments);
            Assert.AreEqual(fixture.TestDirectory, invocation.WorkingDirectory);
        }
        finally
        {
            NativeEntryPoint.DotnetRoot = originalDotnetRoot;
            DeleteFixture(fixture);
        }
    }

    /// <summary>Verifies that positional file discovery defers when the current directory contains a project.</summary>
    [TestMethod]
    public void PositionalFileWithProjectInCurrentDirectoryFallsBackBeforePlanning()
    {
        var fixture = CreateFixture();
        try
        {
            File.WriteAllText(Path.Join(fixture.TestDirectory, "App.csproj"), "<Project />");
            DateTime oldArtifactsTime = DateTime.UtcNow.AddDays(-1);
            Directory.SetLastWriteTimeUtc(fixture.ArtifactsPath, oldArtifactsTime);
            var parseResult = Parser.Parse([
                "run",
                fixture.EntryPointPath,
                "--no-build",
                "--no-launch-profile",
            ]);

            Assert.ThrowsExactly<CommandNotAvailableInAotException>(() =>
                AotRunCommand.Execute(
                    parseResult,
                    static _ => throw new InvalidOperationException("Launcher should not be called."),
                    fixture.TestDirectory));

            Assert.AreEqual(oldArtifactsTime, Directory.GetLastWriteTimeUtc(fixture.ArtifactsPath));
        }
        finally
        {
            DeleteFixture(fixture);
        }
    }

    /// <summary>Verifies that a Project launch profile decorates a synthetic launch.</summary>
    [TestMethod]
    public void ProjectLaunchProfileDecoratesSyntheticLaunch()
    {
        var fixture = CreateFixture();
        string? originalDotnetRoot = NativeEntryPoint.DotnetRoot;
        try
        {
            NativeEntryPoint.DotnetRoot = fixture.TestDirectory;
            WriteLaunchSettings(fixture, $$"""
                {
                    "profiles": {
                        "ProjectProfile": {
                            "commandName": "Project",
                            "commandLineArgs": "profileArg1 profileArg2",
                            "applicationUrl": "https://localhost:5001",
                            "environmentVariables": {
                                "PROFILE_ONLY": "profile-value",
                                "OVERRIDE": "profile-value"
                            }
                        }
                    }
                }
                """);
            var parseResult = Parser.Parse([
                "run",
                "--file", fixture.EntryPointPath,
                "--no-build",
                "-e", "OVERRIDE=cli-value",
            ]);
            AotRunInvocation? invocation = null;

            int exitCode = AotRunCommand.Execute(
                parseResult,
                value =>
                {
                    invocation = value;
                    return 17;
                },
                fixture.TestDirectory);

            Assert.AreEqual(17, exitCode);
            Assert.IsNotNull(invocation);
            Assert.AreEqual(fixture.LaunchArtifacts.AppHost, invocation.Command);
            Assert.AreEqual("profileArg1 profileArg2", invocation.CommandArguments);
            Assert.AreEqual(fixture.TestDirectory, invocation.WorkingDirectory);
            Assert.AreEqual(fixture.ArtifactsPath, invocation.ArtifactsPath);
            Assert.AreEqual("ProjectProfile", invocation.EnvironmentVariables["DOTNET_LAUNCH_PROFILE"]);
            Assert.AreEqual("https://localhost:5001", invocation.EnvironmentVariables["ASPNETCORE_URLS"]);
            Assert.AreEqual("profile-value", invocation.EnvironmentVariables["PROFILE_ONLY"]);
            Assert.AreEqual("cli-value", invocation.EnvironmentVariables["OVERRIDE"]);
        }
        finally
        {
            NativeEntryPoint.DotnetRoot = originalDotnetRoot;
            DeleteFixture(fixture);
        }
    }

    /// <summary>Verifies that a no-build Executable launch profile bypasses the synthetic build cache.</summary>
    [TestMethod]
    public void ExecutableLaunchProfileBypassesSyntheticCache()
    {
        var fixture = CreateFixture();
        try
        {
            string profileDirectory = Path.Join(fixture.TestDirectory, "profile-working-directory");
            Directory.CreateDirectory(profileDirectory);
            WriteLaunchSettings(fixture, """
                {
                    "profiles": {
                        "ExecutableProfile": {
                            "commandName": "Executable",
                            "executablePath": "profile-executable",
                            "workingDirectory": "profile-working-directory",
                            "commandLineArgs": "profileArg1 profileArg2",
                            "environmentVariables": {
                                "PROFILE_ONLY": "profile-value",
                                "OVERRIDE": "profile-value"
                            }
                        }
                    }
                }
                """);
            Directory.Delete(fixture.ArtifactsPath, recursive: true);
            var parseResult = Parser.Parse([
                "run",
                "--file", fixture.EntryPointPath,
                "--no-build",
                "--launch-profile", "ExecutableProfile",
                "-e", "OVERRIDE=cli-value",
                "--", "cli arg", "--flag",
            ]);
            AotRunInvocation? invocation = null;

            int exitCode = AotRunCommand.Execute(
                parseResult,
                value =>
                {
                    invocation = value;
                    return 17;
                },
                fixture.TestDirectory);

            Assert.AreEqual(17, exitCode);
            Assert.IsNotNull(invocation);
            Assert.AreEqual("profile-executable", invocation.Command);
            Assert.AreEqual("\"cli arg\" --flag", invocation.CommandArguments);
            Assert.AreEqual(profileDirectory, invocation.WorkingDirectory);
            Assert.IsNull(invocation.ArtifactsPath);
            Assert.AreEqual("ExecutableProfile", invocation.EnvironmentVariables["DOTNET_LAUNCH_PROFILE"]);
            Assert.AreEqual("profile-value", invocation.EnvironmentVariables["PROFILE_ONLY"]);
            Assert.AreEqual("cli-value", invocation.EnvironmentVariables["OVERRIDE"]);
            string? rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
                RuntimeInformation.RuntimeIdentifier,
                RuntimeInformation.RuntimeIdentifier,
                $"v{Product.TargetFrameworkVersion}");
            if (rootVariableName is not null)
            {
                Assert.DoesNotContain(rootVariableName, invocation.EnvironmentVariables.Keys);
            }
            Assert.IsFalse(Directory.Exists(fixture.ArtifactsPath));
        }
        finally
        {
            DeleteFixture(fixture);
        }
    }

    /// <summary>Verifies that a changed source containing directive bytes defers before launch side effects.</summary>
    [TestMethod]
    public void ChangedSourceWithDirectiveFallsBackBeforeCommit()
    {
        var fixture = CreateFixture();
        try
        {
            File.WriteAllText(fixture.EntryPointPath, "#:package Example@1.0.0\nConsole.WriteLine(42);");
            File.SetLastWriteTimeUtc(fixture.EntryPointPath, File.GetLastWriteTimeUtc(fixture.SuccessCachePath).AddSeconds(1));
            DateTime oldArtifactsTime = DateTime.UtcNow.AddDays(-1);
            Directory.SetLastWriteTimeUtc(fixture.ArtifactsPath, oldArtifactsTime);
            var parseResult = Parser.Parse([
                "run",
                "--file", fixture.EntryPointPath,
                "--no-build",
                "--no-launch-profile",
            ]);
            bool launched = false;

            Assert.Throws<CommandNotAvailableInAotException>(() =>
                AotRunCommand.Execute(parseResult, _ =>
                {
                    launched = true;
                    return 0;
                }));

            Assert.IsFalse(launched);
            Assert.AreEqual(oldArtifactsTime, Directory.GetLastWriteTimeUtc(fixture.ArtifactsPath));
        }
        finally
        {
            DeleteFixture(fixture);
        }
    }

    /// <summary>Verifies that cached arguments and application arguments are combined without introducing a leading separator.</summary>
    /// <param name="cachedArguments">The cached run arguments.</param>
    /// <param name="expectedArguments">The expected final command arguments.</param>
    [TestMethod]
    [DataRow("cached-argument", "cached-argument \"app arg\"")]
    [DataRow(null, "\"app arg\"")]
    public void CachedRunArgumentsDoNotStartWithSeparator(string? cachedArguments, string expectedArguments)
    {
        string actualArguments = AotRunArguments.Combine(
            cachedArguments,
            ["app arg"],
            launchProfileArguments: null,
            appendApplicationArgumentsToBase: true);

        Assert.AreEqual(expectedArguments, actualArguments);
    }

    /// <summary>Verifies that unsupported options defer before cache planning or launch side effects.</summary>
    [TestMethod]
    public void UnsupportedOptionFallsBackBeforePlanning()
    {
        var fixture = CreateFixture();
        try
        {
            DateTime oldArtifactsTime = DateTime.UtcNow.AddDays(-1);
            Directory.SetLastWriteTimeUtc(fixture.ArtifactsPath, oldArtifactsTime);
            var parseResult = Parser.Parse([
                "run",
                "--file", fixture.EntryPointPath,
                "--no-build",
                "--no-launch-profile",
                "--configuration", "Release",
            ]);

            Assert.Throws<CommandNotAvailableInAotException>(() =>
                AotRunCommand.Execute(parseResult, static _ => throw new InvalidOperationException("Launcher should not be called.")));

            Assert.AreEqual(oldArtifactsTime, Directory.GetLastWriteTimeUtc(fixture.ArtifactsPath));
        }
        finally
        {
            DeleteFixture(fixture);
        }
    }

    private static AotRunCommandTestFixture CreateFixture()
    {
        string testDirectory = Path.Join(Path.GetTempPath(), $"dotnet-aot-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        string entryPointPath = Path.Join(testDirectory, "Program.cs");
        File.WriteAllText(entryPointPath, "Console.WriteLine(42);");
        string artifactsPath = VirtualProjectBuilder.GetArtifactsPath(entryPointPath);
        Directory.CreateDirectory(artifactsPath);
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
        foreach (string path in new[] { launchArtifacts.AppHost, launchArtifacts.Assembly, launchArtifacts.RuntimeConfig })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }
        DateTime buildTimeUtc = DateTime.UtcNow.AddSeconds(-2);
        File.SetLastWriteTimeUtc(entryPointPath, buildTimeUtc.AddSeconds(-1));
        File.SetLastWriteTimeUtc(successCachePath, buildTimeUtc);

        return new AotRunCommandTestFixture(testDirectory, entryPointPath, artifactsPath, successCachePath, launchArtifacts);
    }

    private static void WriteLaunchSettings(AotRunCommandTestFixture fixture, string contents)
        => File.WriteAllText(Path.Join(fixture.TestDirectory, "Program.run.json"), contents);

    private static void DeleteFixture(AotRunCommandTestFixture fixture)
    {
        Directory.Delete(fixture.TestDirectory, recursive: true);
        if (Directory.Exists(fixture.ArtifactsPath))
        {
            Directory.Delete(fixture.ArtifactsPath, recursive: true);
        }
    }

    }
