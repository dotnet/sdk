// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;
using BootstrapperStrings = Microsoft.DotNet.Tools.Bootstrapper.Strings;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateEndToEndTests : SdkTest
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public void NativeCopyUpdatesToDailyAndChangesVersion()
    {
        using var environment = new TestEnvironment();
        string source = DotnetupTestUtilities.GetDotnetupExecutablePath();
        string executable = CreateExecutablePath(environment);
        File.Copy(source, executable);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(executable, File.GetUnixFileMode(source));
        }

        string originalVersion = ReadVersion(environment, executable);
        var daily = CreateDownloader(environment).ResolveDotnetupDownload(CurrentRid());
        Assert.AreNotEqual(daily.Version.ToString(), originalVersion,
            "This replacement test requires a distinct native build. Set DOTNETUP_TEST_EXECUTABLE to a self-update-capable development build with a different full version.");

        string output = Run(environment, executable, ["self", "update", "--no-progress"]);
        string updatedVersion = ReadVersion(environment, executable);

        Assert.AreNotEqual(originalVersion, updatedVersion, output);
        Assert.Contains(Microsoft.Dotnet.Installation.Strings.UnsignedBlobFeedWarning, output);
        Assert.Contains(originalVersion, Directory.EnumerateFiles(Path.GetDirectoryName(executable)!, Path.GetFileName(executable) + ".old.*")
            .Select(SelfUpdateVerifier.ReadVersion), "The update must retain the original executable as a backup.");
        AssertDailyBecomesNoOp(environment, executable);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public void DownloadedDailySelfUpdateBecomesNoOp()
    {
        using var environment = new TestEnvironment();
        var downloader = CreateDownloader(environment);
        var daily = downloader.ResolveDotnetupDownload(CurrentRid());
        string executable = CreateExecutablePath(environment);
        downloader.DownloadWithVerification(daily, executable);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.AreEqual(daily.Version.ToString(), ReadVersion(environment, executable));
        AssertDailyBecomesNoOp(environment, executable);
    }

    private static void AssertDailyBecomesNoOp(TestEnvironment environment, string executable)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string before = ReadVersion(environment, executable);
            byte[] originalBytes = File.ReadAllBytes(executable);
            string output = Run(environment, executable, ["self", "update", "--no-progress"]);
            string after = ReadVersion(environment, executable);
            if (before == after)
            {
                string version = before;
                Assert.Contains(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    BootstrapperStrings.SelfUpdateAlreadyUpToDate,
                    version,
                    version), output);
                Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(executable), "A no-op must not change the executable bytes.");
                Assert.DoesNotContain(Microsoft.Dotnet.Installation.Strings.UnsignedBlobFeedWarning, output);
                return;
            }

            Console.WriteLine($"Daily changed during attempt {attempt}: {before} -> {after}. Retrying the no-op check.");
        }

        Assert.Fail($"Daily changed on all {maxAttempts} successful updates; no stable no-op was observed.");
    }

    private static string ReadVersion(TestEnvironment environment, string executable)
    {
        string version = Run(environment, executable, ["--version"]).Trim();
        Assert.IsTrue(Microsoft.Deployment.DotNet.Releases.ReleaseVersion.TryParse(version, out _));
        return version;
    }

    private static string Run(TestEnvironment environment, string executable, string[] args)
    {
        var result = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true,
            workingDirectory: environment.TempRoot, executablePath: executable, timeout: TimeSpan.FromMinutes(3),
            environmentVariables: new()
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
                ["DOTNET_DOTNETUP_DATA_DIR"] = Path.Combine(environment.TempRoot, "data"),
                ["DOTNET_CLI_HOME"] = Path.Combine(environment.TempRoot, "home"),
                ["DOTNET_TESTHOOK_DEFAULT_DOTNET_PATH"] = environment.InstallPath,
                ["DOTNET_TESTHOOK_MANIFEST_PATH"] = environment.ManifestPath,
            });
        Assert.AreEqual(0, result.exitCode, $"{executable} {string.Join(' ', args)}\n{result.output}");
        return result.output;
    }

    private static DotnetDownloader CreateDownloader(TestEnvironment environment)
        => new(new ReleaseManifest(), cacheDirectory: Path.Combine(environment.TempRoot, "cache"));

    private static string CreateExecutablePath(TestEnvironment environment)
    {
        string directory = Path.Combine(environment.TempRoot, "self-update-under-test");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "dotnetup" + DotnetupUtilities.ExeSuffix);
    }

    private static string CurrentRid()
        => DotnetupUtilities.GetRuntimeIdentifier(InstallerUtilities.GetDefaultInstallArchitecture());
}