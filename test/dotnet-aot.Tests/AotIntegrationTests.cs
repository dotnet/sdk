// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
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

    private static Dictionary<string, string> CreateServerEnvironment(string hostPath, string testDirectory)
    {
        var environment = CreateRunEnvironment(hostPath);
        environment["DOTNET_CLI_HOME"] = Path.Join(testDirectory, "home");
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        environment["DOTNET_CLI_CONTEXT_VERBOSE"] = "1";
        environment["DOTNET_CLI_CONTEXT_VERBOSE_TO_STDERR"] = "1";
        environment["DOTNET_TEST_RUNNER"] = "VSTest";
        environment["MSBUILDNODEHANDSHAKESALT"] = $"aot-server-commands-{Guid.NewGuid():N}";
        environment["MSBUILDUSESERVER"] = "1";
        environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "1";
        environment["DOTNET_CLI_RUN_MSBUILD_OUTOFPROC"] = "0";
        environment["MSBUILD_EXE_PATH"] = "";
        environment["MSBUILD_LOGGING_ARGS"] = "";
        environment["MSBUILDDEBUGONSTART"] = "";
        environment["MSBUILDDUMPPROCESSCOUNTERS"] = "0";
        environment["MSBUILDENSURESTDOUTFORTASKPROCESSES"] = "0";
        environment["MSBUILDDISABLENODEREUSE"] = "0";
        environment["MSBUILDFORCEMULTITHREADED"] = "0";
        environment["MSBUILDENABLEMULTITHREADED"] = "0";
        environment["MSBUILDTERMINALLOGGER"] = "";
        environment["MSBUILDLIVELOGGER"] = "";
        return environment;
    }

    private void ShutdownServerAndDeleteTestDirectory(string testDirectory, Dictionary<string, string> environment)
    {
        try
        {
            var (exitCode, stdout, stderr) = RunDn(
                ["build-server", "shutdown", "--msbuild"], enableAot: false, timeoutMs: 60_000,
                extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, exitCode, $"Could not shut down the test MSBuild server: {stdout}{stderr}");
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void AotVersion_WithEnableAot_OutputsVersionAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--version"], enableAot: true);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "Expected version output");
    }

    // Emulates the deployed muxer layout: dotnet-aot lives in a directory other than dn's own, so
    // AppContext.BaseDirectory is no longer the SDK directory. Verifies that --info's Base Path still
    // reports the sdk_dir passed in by the host.
    [TestMethod]
    public void AotInfo_SeparatedLayout_BasePathIsResolvedSdkDirectory()
    {
        SkipIfDnUnavailable();

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
                // Telemetry's SDK-resolution probe can print installed SDKs to stdout on Windows.
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            };

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

    [TestMethod]
    [DataRow("dotnet bu", 9, new[] { "build", "build-server", "msbuild" })]
    [DataRow("dotnet --ver", 12, new[] { "--version" })]
    public void AotComplete_RootLabels_RunWithoutManagedFallbackOrFirstRunSideEffects(
        string input, int position, string[] expected)
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-aot-complete-{Guid.NewGuid():N}");
        string sdkDirectory = Path.Combine(testDirectory, "sdk");
        string cliHome = Path.Combine(testDirectory, "home");
        Directory.CreateDirectory(sdkDirectory);
        Directory.CreateDirectory(cliHome);

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_AOT_SDK_DIR"] = sdkDirectory,
            ["DOTNET_AOT_LIBRARY_DIR"] = sdkDirectory,
            ["DOTNET_CLI_HOME"] = cliHome,
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = bool.TrueString,
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = bool.FalseString,
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = bool.FalseString,
            ["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = bool.TrueString,
            ["DOTNET_NOLOGO"] = bool.FalseString,
        };

        try
        {
            CopyAotLibrary(sdkDirectory);

            var (exitCode, stdout, stderr) = RunDn(
                ["complete", "--position", position.ToString(), input],
                enableAot: true,
                extraEnv: environment);

            Assert.AreEqual(0, exitCode, stderr);
            Assert.IsTrue(string.IsNullOrEmpty(stderr), stderr);
            Assert.AreSequenceEqual(
                expected,
                stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            Assert.IsFalse(File.Exists(Path.Combine(
                cliHome,
                ".dotnet",
                FirstTimeUseNoticeSentinel.SENTINEL)));
            Assert.IsEmpty(Directory.EnumerateFiles(cliHome, "*.sentinel", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void AotComplete_CommandSpecificProviders_FallBackToManaged()
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-aot-complete-fallback-{Guid.NewGuid():N}");
        string sdkDirectory = Path.Combine(testDirectory, "sdk");
        string cliHome = Path.Combine(testDirectory, "home");
        string customHive = Path.Combine(testDirectory, "hive");
        Directory.CreateDirectory(sdkDirectory);
        Directory.CreateDirectory(cliHome);

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_AOT_SDK_DIR"] = sdkDirectory,
            ["DOTNET_AOT_LIBRARY_DIR"] = sdkDirectory,
            ["DOTNET_CLI_HOME"] = cliHome,
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = bool.TrueString,
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = bool.FalseString,
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = bool.FalseString,
            ["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = bool.TrueString,
            ["DOTNET_NOLOGO"] = bool.TrueString,
        };
        string completionInput = $"new --debug:custom-hive \"{customHive}\" ";

        try
        {
            // The SDK root must contain both the loaded native library and the managed fallback.
            string managedSdkDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_SDK_DIR")
                ?? Path.GetDirectoryName(FindDnPath()!)!;
            // Template providers discover installed packages relative to this SDK's Sdks directory.
            environment["MSBuildSDKsPath"] = Path.Combine(managedSdkDirectory, "Sdks");
            foreach (string file in Directory.EnumerateFiles(managedSdkDirectory))
            {
                File.Copy(file, Path.Combine(sdkDirectory, Path.GetFileName(file)));
            }
            CopyAotLibrary(sdkDirectory);

            var (warmupExitCode, _, warmupError) = RunDn(
                ["complete", completionInput],
                enableAot: true,
                extraEnv: environment);
            Assert.AreEqual(0, warmupExitCode, warmupError);

            var (aotExitCode, aotOutput, aotError) = RunDn(
                ["complete", completionInput],
                enableAot: true,
                extraEnv: environment);
            var (managedExitCode, managedOutput, managedError) = RunDn(
                ["complete", completionInput],
                enableAot: false,
                extraEnv: environment);

            Assert.AreEqual(0, aotExitCode, aotError);
            Assert.AreEqual(0, managedExitCode, managedError);
            Assert.IsTrue(string.IsNullOrEmpty(aotError), aotError);
            Assert.IsTrue(string.IsNullOrEmpty(managedError), managedError);
            aotOutput.Should().Contain("console");
            Assert.AreEqual(managedOutput, aotOutput);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static void CopyAotLibrary(string sdkDirectory)
    {
        string aotLibraryDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_LIBRARY_DIR")
            ?? Path.GetDirectoryName(FindDnPath()!)!;
        string aotLibraryFileName = OperatingSystem.IsWindows() ? "dotnet-aot.dll"
            : OperatingSystem.IsMacOS() ? "libdotnet-aot.dylib"
            : "libdotnet-aot.so";
        File.Copy(
            Path.Combine(aotLibraryDirectory, aotLibraryFileName),
            Path.Combine(sdkDirectory, aotLibraryFileName),
            overwrite: true);
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
        string? nugetConfigPath = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_NUGET_CONFIG");
        if (string.IsNullOrEmpty(nugetConfigPath) || !File.Exists(nugetConfigPath))
        {
            Assert.Inconclusive("DOTNET_AOT_TEST_NUGET_CONFIG must identify the NuGet configuration for cached run-file integration setup.");
        }
        File.Copy(nugetConfigPath, Path.Join(testDirectory, "NuGet.config"));

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
    public void BuildHelp_ShowsPropertyOptionInNativeAot()
    {
        SkipIfDnUnavailable();

        var (aotExitCode, aotOutput, aotError) = RunDn(["build", "--help"], enableAot: true);

        Assert.AreEqual(0, aotExitCode, aotError);
        aotOutput.Should().Contain($"-p, -property <{CommandDefinitionStrings.PropertyArgumentName}>");
        Regex.Replace(aotOutput, @"\s+", " ").Should().Contain(CommandDefinitionStrings.PropertyOptionDescription);
    }

    [TestMethod]
    [DataRow("--property")]
    [DataRow("-property")]
    [DataRow("/property")]
    [DataRow("/p")]
    [DataRow("-p")]
    [DataRow("--p")]
    public void BuildPropertyAliases_PreserveManagedInvocationFromNativeAot(string alias)
    {
        SkipIfDnUnavailable();

        string directory = Path.Combine(Path.GetTempPath(), $"aot-build-properties-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string projectPath = Path.Combine(directory, "Properties.proj");
            File.WriteAllText(projectPath, "<Project />");
            string[] args =
            [
                "build", projectPath, "--no-restore", "--nologo",
                "--getProperty:First,Second",
                alias, "First=original;Second=two", $"{alias}:First=updated"
            ];

            var (managedExitCode, managedOutput, managedError) = RunDn(args, enableAot: false, workingDirectory: directory);
            var (aotExitCode, aotOutput, aotError) = RunDn(args, enableAot: true, workingDirectory: directory);

            Assert.AreEqual(0, managedExitCode, managedError);
            Assert.AreEqual(0, aotExitCode, aotError);
            Assert.AreEqual(managedOutput, aotOutput);
            using var output = JsonDocument.Parse(aotOutput);
            var properties = output.RootElement.GetProperty("Properties");
            Assert.AreEqual("updated", properties.GetProperty("First").GetString());
            Assert.AreEqual("two", properties.GetProperty("Second").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void AotBuild_WithEnableAot_FallsBackToManaged()
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Join(Path.GetTempPath(), $"dotnet-aot-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        try
        {
            const string projectFileName = "ManagedFallback.proj";
            string projectPath = Path.Join(testDirectory, projectFileName);
            File.Copy(
                Path.Join(AppContext.BaseDirectory, "TestAssets", "AotManagedFallback", projectFileName),
                projectPath);

            var (exitCode, stdout, stderr) = RunDn(
                ["build", projectPath, "--no-restore", "--disable-build-servers", "--verbosity", "minimal", "-nologo"],
                enableAot: true,
                workingDirectory: testDirectory);

            Assert.AreEqual(0, exitCode, stdout + stderr);
            Assert.Contains("AOT_MANAGED_FALLBACK:Core", stdout);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize] // This test uses and shuts down the test SDK's persistent MSBuild server.
    public void AotBuild_ServerClientMatchesManagedWithoutReplayingBuilds()
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Join(Path.GetTempPath(), $"dotnet-aot-server-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        string binlogRoot = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_BINLOG_DIRECTORY") ?? testDirectory;
        string binlogDirectory = Path.Join(binlogRoot, $"aot-server-{Guid.NewGuid():N}");
        Directory.CreateDirectory(binlogDirectory);
        string projectPath = Path.Join(testDirectory, "Build.proj");
        File.WriteAllText(projectPath, """
            <Project DefaultTargets="Build">
              <Target Name="Restore">
                <WriteLinesToFile File="$(EvidencePath)" Lines="restore" Overwrite="false" />
              </Target>
              <Target Name="Build">
                <Message Text="AOT_SERVER_BUILD:$(MSBuildRuntimeType):$(MSBUILDFAILONDRIVEENUMERATINGWILDCARD)" Importance="high" />
                <WriteLinesToFile File="$(EvidencePath)" Lines="build" Overwrite="false" />
                <Error Condition="'$(FailBuild)' == 'true'" Text="AOT_SERVER_EXPECTED_FAILURE" />
              </Target>
            </Project>
            """);

        string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? throw new InvalidOperationException("The test SDK host path is required.");
        var environment = CreateRunEnvironment(hostPath);
        environment["DOTNET_CLI_HOME"] = Path.Join(testDirectory, "home");
        string telemetryLog = Path.Join(testDirectory, "telemetry.json");
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = OperatingSystem.IsWindows() ? "0" : "1";
        environment["DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT"] = "1";
        environment["DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER"] = "0";
        environment["DOTNET_CLI_TELEMETRY_LOG_PATH"] = telemetryLog;
        foreach (string variable in EnvironmentVariableNames.OtlpExporterEnvVars)
        {
            environment[variable] = "";
        }
        environment["DOTNET_CLI_CONTEXT_VERBOSE"] = "1";
        environment["DOTNET_CLI_CONTEXT_VERBOSE_TO_STDERR"] = "1";
        environment["MSBUILDNODEHANDSHAKESALT"] = $"aot-server-test-{Guid.NewGuid():N}";
        environment["MSBUILDUSESERVER"] = "1";
        environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "1";
        environment["DOTNET_CLI_RUN_MSBUILD_OUTOFPROC"] = "0";
        environment["MSBUILD_EXE_PATH"] = "";
        environment["MSBUILD_LOGGING_ARGS"] = "";
        environment["MSBUILDDEBUGONSTART"] = "";
        environment["MSBUILDDUMPPROCESSCOUNTERS"] = "0";
        environment["MSBUILDENSURESTDOUTFORTASKPROCESSES"] = "0";
        environment["MSBUILDDISABLENODEREUSE"] = "0";
        environment["MSBUILDFORCEMULTITHREADED"] = "0";
        environment["MSBUILDENABLEMULTITHREADED"] = "0";
        environment["MSBUILDFAILONDRIVEENUMERATINGWILDCARD"] = "";

        try
        {
            var (warmupExit, warmupOutput, warmupError) = RunDn(
                ["--version"], enableAot: false, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, warmupExit, warmupOutput + warmupError);

            var native = RunBuild("native", 1, enableAot: true, fail: false);
            var managed = RunBuild("managed", 2, enableAot: false, fail: false);
            Assert.AreEqual(managed.exitCode, native.exitCode, native.stdout + native.stderr);
            Assert.AreEqual(0, native.exitCode, native.stdout + native.stderr);
            Assert.Contains("AOT_SERVER_BUILD:Core:1", native.stdout);
            Assert.Contains("AOT_SERVER_BUILD:Core:1", managed.stdout);
            Assert.Contains("AOT build server client: request completed.", native.stderr);
            Assert.AreSequenceEqual(new[] { "build" }, File.ReadAllLines(Path.Join(testDirectory, "native.txt")));
            Assert.AreSequenceEqual(new[] { "build" }, File.ReadAllLines(Path.Join(testDirectory, "managed.txt")));

            if (OperatingSystem.IsWindows())
            {
                using JsonDocument telemetry = JsonDocument.Parse(File.ReadAllText(telemetryLog));
                JsonElement[] buildEvents = telemetry.RootElement.GetProperty("activities").EnumerateArray()
                    .SelectMany(activity => activity.GetProperty("events").EnumerateArray())
                    .Where(e => e.GetProperty("name").GetString() == "dotnet/cli/msbuild/build")
                    .ToArray();
                Assert.HasCount(2, buildEvents);
                JsonElement nativeTags = buildEvents[0].GetProperty("tags");
                Assert.AreEqual("True", nativeTags.GetProperty("BuildSuccess").GetString());
                Assert.AreEqual("EnvVar", nativeTags.GetProperty("ServerEnableReason").GetString());
                Assert.AreEqual("False", nativeTags.GetProperty("ResponseFileUsed").GetString());
                Assert.IsTrue(double.TryParse(nativeTags.GetProperty("BuildDurationInMilliseconds").GetString(),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double duration)
                    && duration > 0);
                Assert.IsFalse(nativeTags.TryGetProperty("InitialMSBuildServerState", out _));
            }

            var nativeFailure = RunBuild("native-failure", 3, enableAot: true, fail: true);
            var managedFailure = RunBuild("managed-failure", 4, enableAot: false, fail: true);
            Assert.AreEqual(managedFailure.exitCode, nativeFailure.exitCode, nativeFailure.stdout + nativeFailure.stderr);
            Assert.AreNotEqual(0, nativeFailure.exitCode);
            Assert.Contains("AOT_SERVER_EXPECTED_FAILURE", nativeFailure.stdout + nativeFailure.stderr);
            Assert.Contains("AOT build server client: request completed.", nativeFailure.stderr);
            Assert.AreSequenceEqual(new[] { "build" }, File.ReadAllLines(Path.Join(testDirectory, "native-failure.txt")));

            string separateEvidence = Path.Join(testDirectory, "separate.txt");
            var separateRestore = RunDn(
                ["build", projectPath, "--framework", "net8.0", $"-p:EvidencePath={separateEvidence}",
                    $"-bl:{Path.Join(binlogDirectory, "5-{}.binlog")}"],
                enableAot: true, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, separateRestore.exitCode, separateRestore.stdout + separateRestore.stderr);
            Assert.AreEqual(2, separateRestore.stderr.Split("AOT build server client: request completed.").Length - 1);
            Assert.AreSequenceEqual(new[] { "restore", "build" }, File.ReadAllLines(separateEvidence));

            File.WriteAllText(Path.Join(testDirectory, "Directory.Build.rsp"), "-nr:false");
            var fallback = RunBuild("fallback", 6, enableAot: true, fail: false);
            Assert.AreEqual(0, fallback.exitCode, fallback.stdout + fallback.stderr);
            Assert.DoesNotContain("AOT build tier: MSBuildServer.", fallback.stderr);
            Assert.AreSequenceEqual(new[] { "build" }, File.ReadAllLines(Path.Join(testDirectory, "fallback.txt")));
        }
        finally
        {
            ShutdownServerAndDeleteTestDirectory(testDirectory, environment);
        }

        (int exitCode, string stdout, string stderr) RunBuild(string name, int binlogNumber, bool enableAot, bool fail)
        {
            string evidence = Path.Join(testDirectory, $"{name}.txt");
            return RunDn(
                ["build", projectPath, "--no-restore", "--verbosity", "minimal",
                    $"-p:EvidencePath={evidence}", $"-p:FailBuild={fail.ToString().ToLowerInvariant()}",
                    $"-bl:{Path.Join(binlogDirectory, $"{binlogNumber}.binlog")}"],
                enableAot, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
        }
    }

    [TestMethod]
    public void AotServerCommands_MatchManagedAndDoNotReplay()
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Join(Path.GetTempPath(), $"dotnet-aot-server-commands-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        string binlogRoot = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_BINLOG_DIRECTORY") ?? testDirectory;
        string binlogDirectory = Path.Join(binlogRoot, $"aot-server-commands-{Guid.NewGuid():N}");
        Directory.CreateDirectory(binlogDirectory);
        string projectPath = Path.Join(testDirectory, "Commands.proj");
        File.WriteAllText(projectPath, """
            <Project>
              <Target Name="Restore">
                <WriteLinesToFile File="$(AOT_EVIDENCE)" Lines="restore" Overwrite="false" />
                <Error Condition="'$(AOT_FAIL)' == '1'" Text="AOT_SERVER_RESTORE_FAILURE" />
              </Target>
              <Target Name="Clean">
                <WriteLinesToFile File="$(AOT_EVIDENCE)" Lines="clean" Overwrite="false" />
                <Message Condition="'$(CleanMarker)' != ''" Text="AOT_CLEAN_PROPERTY:$(CleanMarker)" Importance="high" />
                <Error Condition="'$(AOT_FAIL)' == '1'" Text="AOT_SERVER_CLEAN_FAILURE" />
              </Target>
              <Target Name="VSTest">
                <WriteLinesToFile File="$(AOT_EVIDENCE)" Lines="test" Overwrite="false" />
                <Error Condition="'$(AOT_FAIL)' == '1'" Text="AOT_SERVER_TEST_FAILURE" />
              </Target>
            </Project>
            """);

        string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? throw new InvalidOperationException("The test SDK host path is required.");
        var environment = CreateServerEnvironment(hostPath, testDirectory);

        try
        {
            var warmup = RunDn(["--version"], enableAot: false, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, warmup.exitCode, warmup.stdout + warmup.stderr);

            foreach (string command in new[] { "restore", "clean", "test" })
            {
                var native = RunCommand(command, "native", enableAot: true);
                var managed = RunCommand(command, "managed", enableAot: false);
                Assert.AreEqual(0, managed.exitCode, managed.stdout + managed.stderr);
                Assert.AreEqual(managed.exitCode, native.exitCode, native.stdout + native.stderr);
                Assert.Contains($"AOT {command} tier: MSBuildServer.", native.stderr);
                Assert.Contains("AOT build server client: request completed.", native.stderr);
                Assert.AreSequenceEqual(new[] { command }, ReadEvidence(command, "native"));
                Assert.AreSequenceEqual(ReadEvidence(command, "managed"), ReadEvidence(command, "native"));

                var nativeFailure = RunCommand(command, "native-failure", enableAot: true, fail: true);
                var managedFailure = RunCommand(command, "managed-failure", enableAot: false, fail: true);
                Assert.AreEqual(managedFailure.exitCode, nativeFailure.exitCode, nativeFailure.stdout + nativeFailure.stderr);
                Assert.AreNotEqual(0, nativeFailure.exitCode);
                Assert.Contains("AOT build server client: request completed.", nativeFailure.stderr);
                Assert.AreSequenceEqual(new[] { command }, ReadEvidence(command, "native-failure"));
                Assert.AreSequenceEqual(ReadEvidence(command, "managed-failure"), ReadEvidence(command, "native-failure"));
            }

            var nativeProperty = RunCommand("clean", "native-property", enableAot: true,
                extraArgs: ["-p:CleanMarker=property value"]);
            var managedProperty = RunCommand("clean", "managed-property", enableAot: false,
                extraArgs: ["-p:CleanMarker=property value"]);
            Assert.AreEqual(0, nativeProperty.exitCode, nativeProperty.stdout + nativeProperty.stderr);
            Assert.AreEqual(managedProperty.exitCode, nativeProperty.exitCode);
            Assert.Contains("AOT clean tier: MSBuildServer.", nativeProperty.stderr);
            Assert.Contains("AOT_CLEAN_PROPERTY:property value", nativeProperty.stdout);
            Assert.Contains("AOT_CLEAN_PROPERTY:property value", managedProperty.stdout);
            Assert.AreSequenceEqual(new[] { "clean" }, ReadEvidence("clean", "native-property"));
            Assert.AreSequenceEqual(ReadEvidence("clean", "managed-property"), ReadEvidence("clean", "native-property"));

            var separateRestore = RunCommand("test", "separate", enableAot: true,
                extraArgs: ["--framework", $"net{Product.TargetFrameworkVersion}"], noRestore: false);
            Assert.AreEqual(0, separateRestore.exitCode, separateRestore.stdout + separateRestore.stderr);
            Assert.Contains("AOT restore tier: MSBuildServer.", separateRestore.stderr);
            Assert.Contains("AOT test tier: MSBuildServer.", separateRestore.stderr);
            Assert.AreSequenceEqual(new[] { "restore", "test" }, ReadEvidence("test", "separate"));

            File.WriteAllText(Path.Join(testDirectory, "Directory.Build.rsp"), "-nr:false");
            foreach (string command in new[] { "restore", "clean", "test" })
            {
                var fallback = RunCommand(command, "fallback", enableAot: true);
                Assert.AreEqual(0, fallback.exitCode, fallback.stdout + fallback.stderr);
                Assert.DoesNotContain($"AOT {command} tier: MSBuildServer.", fallback.stderr);
                Assert.AreSequenceEqual(new[] { command }, ReadEvidence(command, "fallback"));
            }
        }
        finally
        {
            ShutdownServerAndDeleteTestDirectory(testDirectory, environment);
        }

        string[] ReadEvidence(string command, string name)
            => File.ReadAllLines(Path.Join(testDirectory, $"{command}-{name}.txt"));

        (int exitCode, string stdout, string stderr) RunCommand(
            string command, string name, bool enableAot, bool fail = false, string[]? extraArgs = null, bool noRestore = true)
        {
            var commandEnvironment = new Dictionary<string, string>(environment)
            {
                ["AOT_EVIDENCE"] = Path.Join(testDirectory, $"{command}-{name}.txt"),
                ["AOT_FAIL"] = fail ? "1" : "0",
            };
            string[] testArgs = command == "test"
                ? (noRestore ? ["--no-restore", "--tl:on"] : ["--tl:on"])
                : [];
            return RunDn(
                [command, projectPath, .. testArgs, .. (extraArgs ?? []),
                    $"-bl:{Path.Join(binlogDirectory, $"{command}-{name}-{{}}.binlog")}"],
                enableAot, timeoutMs: 120_000, extraEnv: commandEnvironment, workingDirectory: testDirectory);
        }
    }

    [TestMethod]
    public void AotVSTest_ServerRunsRealTestsAndPreservesManagedFallback()
    {
        SkipIfDnUnavailable();

        string testDirectory = Path.Join(Path.GetTempPath(), $"dotnet-aot-vstest-server-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        string binlogRoot = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_BINLOG_DIRECTORY") ?? testDirectory;
        string binlogDirectory = Path.Join(binlogRoot, $"aot-vstest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(binlogDirectory);
        string projectPath = Path.Join(testDirectory, "ServerTests.csproj");
        File.WriteAllText(projectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net{Product.TargetFrameworkVersion}</TargetFramework>
                <IsTestProject>true</IsTestProject>
                <IsPackable>false</IsPackable>
                <UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
                <PackageReference Include="MSTest.TestAdapter" Version="3.4.3" />
                <PackageReference Include="MSTest.TestFramework" Version="3.4.3" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(testDirectory, "ServerTests.cs"), """
            using System;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class ServerTests
            {
                [TestMethod]
                public void Pass()
                    => File.AppendAllText(Environment.GetEnvironmentVariable("AOT_TEST_EVIDENCE")!, "pass\n");

                [TestMethod]
                public void Fail()
                {
                    File.AppendAllText(Environment.GetEnvironmentVariable("AOT_TEST_EVIDENCE")!, "fail\n");
                    Assert.Fail("AOT_SERVER_EXPECTED_TEST_FAILURE");
                }
            }
            """);

        string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? throw new InvalidOperationException("The test SDK host path is required.");
        string nugetConfigPath = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_NUGET_CONFIG")
            ?? throw new InvalidOperationException("The test NuGet configuration is required.");
        var environment = CreateServerEnvironment(hostPath, testDirectory);

        try
        {
            var warmup = RunDn(["--version"], enableAot: false, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, warmup.exitCode, warmup.stdout + warmup.stderr);

            var restore = RunDn(
                ["restore", projectPath, "--configfile", nugetConfigPath, Binlog("restore")],
                enableAot: true, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, restore.exitCode, restore.stdout + restore.stderr);
            Assert.Contains("AOT restore tier: MSBuildServer.", restore.stderr);
            Assert.IsTrue(File.Exists(Path.Join(testDirectory, "obj", "project.assets.json")));

            var build = RunDn(
                ["build", projectPath, "--no-restore", Binlog("build")],
                enableAot: true, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, build.exitCode, build.stdout + build.stderr);

            foreach (bool fail in new[] { false, true })
            {
                string caseName = fail ? "fail" : "pass";
                var native = RunTest($"native-{caseName}", enableAot: true, fail, noBuild: fail);
                var managed = RunTest($"managed-{caseName}", enableAot: false, fail, noBuild: fail);
                Assert.AreEqual(managed.exitCode, native.exitCode, native.stdout + native.stderr);
                Assert.AreEqual(fail ? 1 : 0, native.exitCode, native.stdout + native.stderr);
                Assert.Contains("AOT test tier: MSBuildServer.", native.stderr);
                Assert.Contains("AOT build server client: request completed.", native.stderr);
                Assert.AreSequenceEqual(new[] { caseName }, ReadEvidence($"native-{caseName}"));
                Assert.AreSequenceEqual(ReadEvidence($"managed-{caseName}"), ReadEvidence($"native-{caseName}"));
                AssertTestResult($"native-{caseName}", fail);
                AssertTestResult($"managed-{caseName}", fail);
            }

            var fallback = RunTest("fallback", enableAot: true, fail: false, terminalLogger: "off");
            Assert.AreEqual(0, fallback.exitCode, fallback.stdout + fallback.stderr);
            Assert.DoesNotContain("AOT test tier: MSBuildServer.", fallback.stderr);
            Assert.AreSequenceEqual(new[] { "pass" }, ReadEvidence("fallback"));
            AssertTestResult("fallback", fail: false);

            string testAssembly = Path.Join(testDirectory, "bin", "Debug", $"net{Product.TargetFrameworkVersion}", "ServerTests.dll");
            Assert.IsTrue(File.Exists(testAssembly));
            var nativeClean = RunDn(["clean", projectPath, Binlog("native-clean")],
                enableAot: true, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, nativeClean.exitCode, nativeClean.stdout + nativeClean.stderr);
            Assert.Contains("AOT clean tier: MSBuildServer.", nativeClean.stderr);
            Assert.Contains("AOT build server client: request completed.", nativeClean.stderr);
            Assert.IsFalse(File.Exists(testAssembly));

            var managedBuild = RunDn(["build", projectPath, "--no-restore", Binlog("managed-build")],
                enableAot: false, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, managedBuild.exitCode, managedBuild.stdout + managedBuild.stderr);
            Assert.IsTrue(File.Exists(testAssembly));
            var managedClean = RunDn(["clean", projectPath, Binlog("managed-clean")],
                enableAot: false, timeoutMs: 120_000, extraEnv: environment, workingDirectory: testDirectory);
            Assert.AreEqual(0, managedClean.exitCode, managedClean.stdout + managedClean.stderr);
            Assert.IsFalse(File.Exists(testAssembly));
        }
        finally
        {
            ShutdownServerAndDeleteTestDirectory(testDirectory, environment);
        }

        string Binlog(string name) => $"-bl:{Path.Join(binlogDirectory, $"{name}-{{}}.binlog")}";
        string[] ReadEvidence(string name) => File.ReadAllLines(Path.Join(testDirectory, $"{name}.txt"));

        void AssertTestResult(string name, bool fail)
        {
            string path = Path.Join(testDirectory, name, "results.trx");
            Assert.IsTrue(File.Exists(path), $"Missing test result: {path}");
            XElement counters = XDocument.Load(path).Descendants().Single(element => element.Name.LocalName == "Counters");
            Assert.AreEqual("1", counters.Attribute("total")?.Value);
            Assert.AreEqual(fail ? "1" : "0", counters.Attribute("failed")?.Value);
            Assert.AreEqual(fail ? "0" : "1", counters.Attribute("passed")?.Value);
        }

        (int exitCode, string stdout, string stderr) RunTest(
            string name, bool enableAot, bool fail, bool noBuild = true, string terminalLogger = "on")
        {
            var testEnvironment = new Dictionary<string, string>(environment)
            {
                ["AOT_TEST_EVIDENCE"] = Path.Join(testDirectory, $"{name}.txt"),
            };
            return RunDn(
                ["test", projectPath, noBuild ? "--no-build" : "--no-restore", $"--tl:{terminalLogger}", "--filter",
                    $"FullyQualifiedName~{(fail ? "Fail" : "Pass")}", "--logger", "trx;LogFileName=results.trx",
                    "--results-directory", Path.Join(testDirectory, name), Binlog(name)],
                enableAot, timeoutMs: 120_000, extraEnv: testEnvironment, workingDirectory: testDirectory);
        }
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
