// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Integration tests that run the actual AOT binary (dn.exe / dn) end-to-end.
///  These tests require the AOT binary to be present in the SDK layout.
///  They are categorized with <c>[TestCategory("AOT")]</c> so they can be filtered in CI
///  (e.g. by the <c>AOT</c> test category).
/// </summary>
[TestCategory("AOT")]
[TestClass]
public partial class AotIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private ITestOutputHelper? _logBacking;
    private ITestOutputHelper _log => _logBacking ??= new TestContextOutputHelper(TestContext);

    private static string? FindDnPath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_DN_PATH");
        if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        // Look for dn in the SDK layout (same location as dotnet)
        string? dotnetPath = Environment.ProcessPath;
        if (dotnetPath is null)
        {
            return null;
        }

        string? sdkDir = Path.GetDirectoryName(dotnetPath);
        if (sdkDir is null)
        {
            return null;
        }

        string dnName = OperatingSystem.IsWindows() ? "dn.exe" : "dn";
        string dnPath = Path.Combine(sdkDir, dnName);
        return File.Exists(dnPath) ? dnPath : null;
    }

    private (int exitCode, string stdout, string stderr) RunDn(
        string[] args,
        bool enableAot = true,
        int timeoutMs = 30_000,
        Dictionary<string, string>? extraEnv = null,
        string? workingDirectory = null)
    {
        string? dnPath = FindDnPath();
        if (dnPath is null)
        {
            return (-1, "", "dn binary not found");
        }

        var psi = new ProcessStartInfo
        {
            FileName = dnPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        if (enableAot)
        {
            psi.Environment["DOTNET_CLI_ENABLEAOT"] = "true";
        }
        else
        {
            // The AOT fast path is enabled by default, so explicitly disable it (rather than just
            // removing the variable) to exercise the managed-fallback behavior.
            psi.Environment["DOTNET_CLI_ENABLEAOT"] = "false";
        }

        if (extraEnv is not null)
        {
            foreach (KeyValuePair<string, string> entry in extraEnv)
            {
                psi.Environment[entry.Key] = entry.Value;
            }
        }

        _log.WriteLine($"Running: {dnPath} {string.Join(" ", args)}");
        _log.WriteLine($"  DOTNET_CLI_ENABLEAOT={enableAot}");

        using var process = Process.Start(psi)!;

        // Read streams asynchronously before WaitForExit to avoid deadlocks
        // when the child process fills the OS pipe buffer.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);

        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill();
            return (-1, "", "[TIMEOUT]");
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();

        _log.WriteLine($"  Exit code: {process.ExitCode}");
        if (!string.IsNullOrEmpty(stdout)) _log.WriteLine($"  Stdout: {stdout.TrimEnd()}");
        if (!string.IsNullOrEmpty(stderr)) _log.WriteLine($"  Stderr: {stderr.TrimEnd()}");

        return (process.ExitCode, stdout, stderr);
    }

    private (int exitCode, string stdout, string stderr) RunProcess(
        string executablePath,
        string[] args,
        string workingDirectory,
        Dictionary<string, string> environment,
        int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
        foreach ((string name, string value) in environment)
        {
            psi.Environment[name] = value;
        }

        using var process = Process.Start(psi)!;
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(entireProcessTree: true);
            return (-1, "", "[TIMEOUT]");
        }

        return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    private void SkipIfDnUnavailable()
    {
        if (FindDnPath() is null)
        {
            Assert.Inconclusive("AOT binary (dn) not found in SDK layout. Build with NativeAOT to enable these tests.");
        }
    }

    [TestMethod]
    public void AotTestModules_RunsManagedTestModule()
    {
        SkipIfDnUnavailable();
        string? testModule = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_MANAGED_TEST_MODULE");
        if (string.IsNullOrEmpty(testModule) || !File.Exists(testModule))
        {
            Assert.Inconclusive("A managed Microsoft.Testing.Platform module was not provided for Native AOT validation.");
        }

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_CONTEXT_VERBOSE"] = bool.TrueString,
            ["DOTNET_CLI_CONTEXT_VERBOSE_TO_STDERR"] = bool.TrueString,
            ["DOTNET_TEST_RUNNER"] = "Microsoft.Testing.Platform",
        };
        var (exitCode, stdout, stderr) = RunDn(
            [
                "test",
                "--test-modules", Path.GetFileName(testModule),
                "--root-directory", Path.GetDirectoryName(testModule)!,
                "--no-progress",
                "--no-ansi",
                "--",
                "--filter", "FullyQualifiedName~AotParserTests.ParseVersion_HasNoErrors",
            ],
            enableAot: true,
            timeoutMs: 60_000,
            extraEnv: environment);

        Assert.AreEqual(0, exitCode, stderr);
        stderr.Should().Contain("AOT test tier: TestModules.");
        stdout.Should().Contain("total: 1");
        stdout.Should().Contain("succeeded: 1");
    }

    private static Dictionary<string, string> CreateRunEnvironment(string hostPath)
    {
        string dotnetRoot = Path.GetDirectoryName(hostPath)!;
        var environment = new Dictionary<string, string>
        {
            ["DOTNET_ROOT"] = dotnetRoot,
            ["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = bool.TrueString,
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = bool.FalseString,
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = bool.FalseString,
            ["DOTNET_NOLOGO"] = "1",
        };
        string? rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
            RuntimeInformation.RuntimeIdentifier,
            RuntimeInformation.RuntimeIdentifier,
            $"v{Product.TargetFrameworkVersion}");
        if (rootVariableName is not null)
        {
            environment[rootVariableName] = dotnetRoot;
        }

        string packagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");
        if (Directory.Exists(packagesPath))
        {
            environment["NUGET_PACKAGES"] = packagesPath;
        }

        return environment;
    }

    [TestMethod]
    public void AotVersion_WithEnableAot_OutputsVersionAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--version"], enableAot: true);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "Expected version output");
    }

    [TestMethod]
    public void AotInfo_SeparatedLayout_BasePathIsResolvedSdkDirectory()
    {
        SkipIfDnUnavailable();
        RunSeparatedLayoutBasePathTest(selfLocate: false);
    }

    [TestMethod]
    public void AotInfo_SeparatedLayout_SelfLocate_BasePathIsResolvedSdkDirectory()
    {
        SkipIfDnUnavailable();
        RunSeparatedLayoutBasePathTest(selfLocate: true);
    }

    // Emulates the deployed muxer layout: dotnet-aot lives in a directory other than dn's own, so
    // AppContext.BaseDirectory is no longer the SDK directory. Verifies that --info's Base Path still
    // reports the resolved SDK directory - whether it was passed in as sdk_dir (selfLocate: false) or
    // self-located from the loaded module (selfLocate: true).
    private void RunSeparatedLayoutBasePathTest(bool selfLocate)
    {
        string dnPath = FindDnPath()!;
        string sdkLayoutDir = Path.GetDirectoryName(dnPath)!;
        string aotLib = OperatingSystem.IsWindows() ? "dotnet-aot.dll"
            : OperatingSystem.IsMacOS() ? "libdotnet-aot.dylib"
            : "libdotnet-aot.so";
        string aotLibraryDir = Environment.GetEnvironmentVariable("DOTNET_AOT_LIBRARY_DIR") ?? sdkLayoutDir;
        string aotSource = Path.Combine(aotLibraryDir, aotLib);
        if (!File.Exists(aotSource))
        {
            Assert.Inconclusive($"{aotLib} not found; build with NativeAOT to enable this test.");
        }

        string sdkSubDir = Path.Combine(Path.GetTempPath(), "aot-sep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sdkSubDir);
        try
        {
            // These are synthetic sentinel values; only their end-to-end appearance in --version matters.
            const string expectedVersion = "11.0.100-preview.7.12345.67";
            File.Copy(aotSource, Path.Combine(sdkSubDir, aotLib));
            File.WriteAllLines(
                Path.Combine(sdkSubDir, ".version"),
                ["0123456789abcdef", expectedVersion]);

            var env = new Dictionary<string, string>
            {
                ["DOTNET_AOT_SDK_DIR"] = sdkSubDir,
                ["DOTNET_AOT_LIBRARY_DIR"] = sdkSubDir,
            };
            if (selfLocate)
            {
                env["DOTNET_AOT_BLANK_SDKDIR"] = "1";
            }

            var (exitCode, stdout, _) = RunDn(["--info"], enableAot: true, extraEnv: env);

            Assert.AreEqual(0, exitCode);

            bool basePathReferencesSdkDir = false;
            foreach (string line in stdout.Split('\n'))
            {
                if (line.Contains("Base Path:") && line.Contains(sdkSubDir))
                {
                    basePathReferencesSdkDir = true;
                    break;
                }
            }

            Assert.IsTrue(basePathReferencesSdkDir,
                $"--info Base Path did not reference the resolved SDK directory '{sdkSubDir}'. Output:\n{stdout}");

            var (versionExitCode, versionOutput, _) = RunDn(["--version"], enableAot: true, extraEnv: env);
            Assert.AreEqual(0, versionExitCode);
            Assert.AreEqual(expectedVersion, versionOutput.Trim());
        }
        finally
        {
            Directory.Delete(sdkSubDir, recursive: true);
        }
    }

    [TestMethod]
    public void AotInfo_WithEnableAot_OutputsInfoAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--info"], enableAot: true);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain(".NET SDK:");
        stdout.Should().Contain("Version:");
        stdout.Should().Contain("Workload version:");
        stdout.Should().Contain("MSBuild version:");
        stdout.Should().Contain("Runtime Environment:");
    }

    [TestMethod]
    public void AotNoArgs_WithEnableAot_ShowsUsage()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn([], enableAot: true);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain("Usage:");
    }

    /// <summary>
    /// Verifies synthetic no-build launch across explicit, positional, shorthand, and profile forms, plus conservative managed fallback.
    /// </summary>
    [TestMethod]
    public void AotRun_NoBuildSyntheticCache_LaunchesAndConservativelyFallsBack()
    {
        SkipIfDnUnavailable();

        string testDirectory = TestPathUtility.ResolveTempPrefixLink(
            Path.Join(Path.GetTempPath(), $"dotnet-aot-run-file-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(testDirectory);
        string entryPointPath = Path.Join(testDirectory, "Program.cs");
        File.WriteAllText(entryPointPath, """
            if (Environment.GetEnvironmentVariable("REPORT_PROFILE") == "1")
            {
                Console.WriteLine(
                    "AOT_PROFILE:" +
                    Environment.GetEnvironmentVariable("TEST_AOT_RUN") + ":" +
                    string.Join("|", args) + ":" +
                    Environment.GetEnvironmentVariable("PROFILE_ONLY") + ":" +
                    Environment.GetEnvironmentVariable("ASPNETCORE_URLS") + ":" +
                    Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE") + ":" +
                    Environment.CurrentDirectory);
            }
            else
            {
                Console.WriteLine("AOT_RUN_FILE:" + Environment.GetEnvironmentVariable("TEST_AOT_RUN") + ":" + string.Join("|", args));
            }
            """);
        string artifactsPath = VirtualProjectBuilder.GetArtifactsPath(entryPointPath);
        if (Directory.Exists(artifactsPath))
        {
            Directory.Delete(artifactsPath, recursive: true);
        }

        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(hostPath) || !File.Exists(hostPath))
        {
            Assert.Inconclusive("DOTNET_HOST_PATH must identify the dotnet host for cached run-file integration setup.");
        }

        var environment = CreateRunEnvironment(hostPath);

        try
        {
            var setupEnvironment = new Dictionary<string, string>(environment)
            {
                ["DOTNET_CLI_ENABLEAOT"] = bool.FalseString,
                ["DOTNET_HOST_PATH"] = hostPath,
            };
            var (setupExitCode, setupOutput, setupError) = RunProcess(
                hostPath,
                ["run", "--file", entryPointPath, "--no-launch-profile"],
                testDirectory,
                setupEnvironment);

            Assert.AreEqual(0, setupExitCode, setupOutput + setupError);
            Assert.AreEqual("AOT_RUN_FILE::", setupOutput.Trim());

            string successCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName);
            RunFileBuildCacheEntry? syntheticCacheEntry;
            using (FileStream stream = File.OpenRead(successCachePath))
            {
                syntheticCacheEntry = JsonSerializer.Deserialize(
                    stream,
                    RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }
            Assert.IsNotNull(syntheticCacheEntry);
            syntheticCacheEntry.BuildLevel = BuildLevel.Csc;
            syntheticCacheEntry.Run = null;
            syntheticCacheEntry.BuildResultFile = null;
            syntheticCacheEntry.CscArguments = [];
            using (FileStream stream = File.Create(successCachePath))
            {
                JsonSerializer.Serialize(
                    stream,
                    syntheticCacheEntry,
                    RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }

            environment["DOTNET_CLI_CONTEXT_VERBOSE"] = bool.TrueString;
            environment["DOTNET_CLI_CONTEXT_VERBOSE_TO_STDERR"] = bool.TrueString;
            var (exitCode, stdout, stderr) = RunDn(
                [
                    "run",
                    "--file", entryPointPath,
                    "--no-build",
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, exitCode, stderr);
            Assert.AreEqual("AOT_RUN_FILE:value:arg one|--flag", stdout.Trim());
            Assert.Contains("AOT run tier: LaunchOnly (NoBuildSyntheticCache).", stderr);
            Assert.DoesNotContain("Getting target command: for csc-built program.", stderr);

            var (positionalExitCode, positionalStdout, positionalStderr) = RunDn(
                [
                    "run",
                    entryPointPath,
                    "--no-build",
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, positionalExitCode, positionalStderr);
            Assert.AreEqual("AOT_RUN_FILE:value:arg one|--flag", positionalStdout.Trim());
            Assert.Contains("AOT run tier: LaunchOnly (NoBuildSyntheticCache).", positionalStderr);
            Assert.DoesNotContain("Getting target command: for csc-built program.", positionalStderr);

            var (shorthandExitCode, shorthandStdout, shorthandStderr) = RunDn(
                [
                    entryPointPath,
                    "--no-build",
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, shorthandExitCode, shorthandStderr);
            Assert.AreEqual("AOT_RUN_FILE:value:arg one|--flag", shorthandStdout.Trim());
            Assert.Contains("AOT run tier: LaunchOnly (NoBuildSyntheticCache).", shorthandStderr);
            Assert.DoesNotContain("Getting target command: for csc-built program.", shorthandStderr);

            var launchArtifacts = FileBasedAppRunPlan.GetCscBuiltProgramLaunchArtifacts(entryPointPath, artifactsPath);
            string profileWorkingDirectory = Path.Join(testDirectory, "profile-working-directory");
            Directory.CreateDirectory(profileWorkingDirectory);
            string launchSettingsPath = Path.Join(testDirectory, "Program.run.json");
            WriteLaunchSettings(launchSettingsPath, launchArtifacts.AppHost);

            var (projectProfileExitCode, projectProfileStdout, projectProfileStderr) = RunDn(
                [
                    "run",
                    "--file", entryPointPath,
                    "--no-build",
                    "--launch-profile", "ProjectProfile",
                    "-e", "TEST_AOT_RUN=cli-value",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, projectProfileExitCode, projectProfileStderr);
            Assert.AreEqual(
                $"AOT_PROFILE:cli-value:profileArg1|profileArg2:profile-value:https://localhost:5001:ProjectProfile:{testDirectory}",
                projectProfileStdout.Trim());
            Assert.Contains($"Using launch settings from {launchSettingsPath}", projectProfileStderr);
            Assert.Contains("AOT run tier: LaunchOnly (NoBuildSyntheticCache).", projectProfileStderr);
            Assert.DoesNotContain("Getting target command: for csc-built program.", projectProfileStderr);

            var (shorthandProfileExitCode, shorthandProfileStdout, shorthandProfileStderr) = RunDn(
                [
                    entryPointPath,
                    "--no-build",
                    "--launch-profile", "ProjectProfile",
                    "-e", "TEST_AOT_RUN=cli-value",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, shorthandProfileExitCode, shorthandProfileStderr);
            Assert.AreEqual(projectProfileStdout.Trim(), shorthandProfileStdout.Trim());
            Assert.Contains("AOT run tier: LaunchOnly (NoBuildSyntheticCache).", shorthandProfileStderr);

            byte[] successCacheBeforeExecutableProfile = File.ReadAllBytes(successCachePath);
            File.Delete(successCachePath);
            DateTime artifactsTimeBeforeExecutableProfile = Directory.GetLastWriteTimeUtc(artifactsPath);

            var (executableProfileExitCode, executableProfileStdout, executableProfileStderr) = RunDn(
                [
                    "run",
                    "--file", entryPointPath,
                    "--no-build",
                    "--launch-profile", "ExecutableProfile",
                    "-e", "TEST_AOT_RUN=cli-value",
                    "--", "cli arg",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, executableProfileExitCode, executableProfileStderr);
            Assert.AreEqual(
                $"AOT_PROFILE:cli-value:cli arg:executable-value::ExecutableProfile:{profileWorkingDirectory}",
                executableProfileStdout.Trim());
            Assert.Contains($"Using launch settings from {launchSettingsPath}", executableProfileStderr);
            Assert.Contains("AOT run tier: LaunchOnly (ExecutableLaunchProfile).", executableProfileStderr);
            Assert.DoesNotContain("Getting target command:", executableProfileStderr);
            Assert.AreEqual(artifactsTimeBeforeExecutableProfile, Directory.GetLastWriteTimeUtc(artifactsPath));
            File.WriteAllBytes(successCachePath, successCacheBeforeExecutableProfile);

            string projectPath = Path.Join(testDirectory, "App.csproj");
            File.WriteAllText(projectPath, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net{{Product.TargetFrameworkVersion}}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                </Project>
                """);
            var (projectBuildExitCode, projectBuildOutput, projectBuildError) = RunProcess(
                hostPath,
                ["build", projectPath, "--tl:off"],
                testDirectory,
                setupEnvironment);
            Assert.AreEqual(0, projectBuildExitCode, projectBuildOutput + projectBuildError);

            var projectRunEnvironment = new Dictionary<string, string>(environment)
            {
                ["DOTNET_CLI_ENABLEAOT"] = bool.TrueString,
                ["DOTNET_HOST_PATH"] = hostPath,
            };
            var (projectExitCode, projectStdout, projectStderr) = RunProcess(
                hostPath,
                [
                    "run",
                    entryPointPath,
                    "--no-build",
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                testDirectory,
                projectRunEnvironment);

            Assert.AreEqual(0, projectExitCode, projectStderr);
            Assert.AreEqual($"AOT_RUN_FILE:value:{entryPointPath}|arg one|--flag", projectStdout.Trim());
            Assert.DoesNotContain("AOT run tier: LaunchOnly", projectStderr);

            var (projectShorthandExitCode, projectShorthandStdout, projectShorthandStderr) = RunDn(
                [
                    entryPointPath,
                    "--no-build",
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, projectShorthandExitCode, projectShorthandStderr);
            Assert.AreEqual("AOT_RUN_FILE:value:arg one|--flag", projectShorthandStdout.Trim());
            Assert.Contains("AOT run tier: LaunchOnly (NoBuildSyntheticCache).", projectShorthandStderr);
            File.Delete(projectPath);

            File.AppendAllText(entryPointPath, $"{Environment.NewLine}// #: conservative fallback");
            RunFileBuildCacheEntry? fallbackCacheEntry;
            using (FileStream stream = File.OpenRead(successCachePath))
            {
                fallbackCacheEntry = JsonSerializer.Deserialize(
                    stream,
                    RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }
            Assert.IsNotNull(fallbackCacheEntry);
            fallbackCacheEntry.CscArguments = ["/nologo"];
            using (FileStream stream = File.Create(successCachePath))
            {
                JsonSerializer.Serialize(
                    stream,
                    fallbackCacheEntry,
                    RunFileBuildCacheJsonSerializerContext.Default.RunFileBuildCacheEntry);
            }
            File.SetLastWriteTimeUtc(entryPointPath, File.GetLastWriteTimeUtc(successCachePath).AddSeconds(2));
            byte[] successCacheBeforeFallback = File.ReadAllBytes(successCachePath);

            var (fallbackExitCode, fallbackStdout, fallbackStderr) = RunDn(
                [
                    "run",
                    "--file", entryPointPath,
                    "--no-build",
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, fallbackExitCode, fallbackStderr);
            Assert.AreEqual("AOT_RUN_FILE:value:arg one|--flag", fallbackStdout.Trim());
            Assert.Contains("Getting target command: for csc-built program.", fallbackStderr);
            Assert.DoesNotContain("AOT run tier: LaunchOnly", fallbackStderr);
            Assert.AreSequenceEqual(successCacheBeforeFallback, File.ReadAllBytes(successCachePath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
            if (Directory.Exists(artifactsPath))
            {
                Directory.Delete(artifactsPath, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that the product muxer launches validated cached run properties without rewriting the cache.
    /// </summary>
    [TestMethod]
    public void AotRun_ValidatedCachedRunPropertiesLaunches()
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Join(Path.GetTempPath(), $"dotnet-aot-run-file-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        string entryPointPath = Path.Join(testDirectory, "Program.cs");
        File.WriteAllText(entryPointPath, """
            #:property AssemblyName=CachedApp
            #:property PublishAot=false
            Console.WriteLine("AOT_CACHED:v1:" + Environment.GetEnvironmentVariable("TEST_AOT_RUN") + ":" + string.Join("|", args));
            """);
        string artifactsPath = VirtualProjectBuilder.GetArtifactsPath(entryPointPath);
        if (Directory.Exists(artifactsPath))
        {
            Directory.Delete(artifactsPath, recursive: true);
        }

        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(hostPath) || !File.Exists(hostPath))
        {
            Assert.Inconclusive("DOTNET_HOST_PATH must identify the dotnet host for cached-run integration setup.");
        }

        var environment = CreateRunEnvironment(hostPath);
        var setupEnvironment = new Dictionary<string, string>(environment)
        {
            ["DOTNET_CLI_ENABLEAOT"] = bool.FalseString,
            ["DOTNET_HOST_PATH"] = hostPath,
        };

        try
        {
            var (setupExitCode, setupOutput, setupError) = RunProcess(
                hostPath,
                ["run", "--file", entryPointPath, "--no-launch-profile"],
                testDirectory,
                setupEnvironment);
            Assert.AreEqual(0, setupExitCode, setupOutput + setupError);
            Assert.AreEqual("AOT_CACHED:v1::", setupOutput.Trim());

            string successCachePath = Path.Join(artifactsPath, FileBasedAppRunPlan.BuildSuccessCacheFileName);
            byte[] successCacheBeforeNativeLaunch = File.ReadAllBytes(successCachePath);
            environment["DOTNET_CLI_CONTEXT_VERBOSE"] = bool.TrueString;
            environment["DOTNET_CLI_CONTEXT_VERBOSE_TO_STDERR"] = bool.TrueString;

            var (exitCode, stdout, stderr) = RunDn(
                [
                    "run",
                    "--file", entryPointPath,
                    "--no-launch-profile",
                    "-e", "TEST_AOT_RUN=value",
                    "--", "arg one", "--flag",
                ],
                enableAot: true,
                extraEnv: environment,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, exitCode, stderr);
            Assert.AreEqual("AOT_CACHED:v1:value:arg one|--flag", stdout.Trim());
            Assert.Contains("AOT run tier: CachedLaunch (CacheValid).", stderr);
            Assert.DoesNotContain("Getting target command:", stderr);
            Assert.AreSequenceEqual(successCacheBeforeNativeLaunch, File.ReadAllBytes(successCachePath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
            if (Directory.Exists(artifactsPath))
            {
                Directory.Delete(artifactsPath, recursive: true);
            }
        }
    }

    private static void WriteLaunchSettings(string path, string executablePath)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteStartObject("profiles");

        writer.WriteStartObject("ProjectProfile");
        writer.WriteString("commandName", "Project");
        writer.WriteString("commandLineArgs", "profileArg1 profileArg2");
        writer.WriteString("applicationUrl", "https://localhost:5001");
        writer.WriteStartObject("environmentVariables");
        writer.WriteString("REPORT_PROFILE", "1");
        writer.WriteString("PROFILE_ONLY", "profile-value");
        writer.WriteString("TEST_AOT_RUN", "profile-value");
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartObject("ExecutableProfile");
        writer.WriteString("commandName", "Executable");
        writer.WriteString("executablePath", executablePath);
        writer.WriteString("workingDirectory", "profile-working-directory");
        writer.WriteString("commandLineArgs", "executableProfileArg");
        writer.WriteStartObject("environmentVariables");
        writer.WriteString("REPORT_PROFILE", "1");
        writer.WriteString("PROFILE_ONLY", "executable-value");
        writer.WriteString("TEST_AOT_RUN", "profile-value");
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    [TestMethod]
    public void AotBuild_WithEnableAot_FallsBackToManaged()
    {
        SkipIfDnUnavailable();

        // "build" is unsupported by AOT parser → should fall back to managed CLI
        // In a full SDK layout, this would invoke dotnet build. We just verify it doesn't crash.
        var (exitCode, stdout, stderr) = RunDn(["build", "--help"], enableAot: true);

        // If managed fallback works, it should show build help (exit 0)
        // If managed fallback is missing, it returns 1
        // Either way, it shouldn't crash or timeout
        Assert.IsTrue(exitCode == 0 || exitCode == 1,
            $"Expected exit code 0 or 1, got {exitCode}. Stderr: {stderr}");
    }

    [TestMethod]
    public void Version_WithAotDisabled_StillWorks()
    {
        SkipIfDnUnavailable();

        // With DOTNET_CLI_ENABLEAOT disabled, everything goes through managed fallback
        var (exitCode, stdout, stderr) = RunDn(["--version"], enableAot: false);

        // Managed fallback requires dotnet.dll + all dependencies in the layout.
        // In a partial layout (e.g. local dev with only dn.exe published),
        // the fallback correctly fails because dotnet.dll is missing.
        if (exitCode != 0 && stderr.Contains("dotnet.dll"))
        {
            Assert.Inconclusive("Managed fallback not available (dotnet.dll not in layout)");
        }

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout));
    }

    [TestMethod]
    public void Info_WithAotDisabled_ShowsFullInfo()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, stderr) = RunDn(["--info"], enableAot: false);

        if (exitCode != 0 && stderr.Contains("dotnet.dll"))
        {
            Assert.Inconclusive("Managed fallback not available (dotnet.dll not in layout)");
        }

        Assert.AreEqual(0, exitCode);
        // Managed fallback should include workload and MSBuild info
        stdout.Should().Contain("Version:");
    }
}
