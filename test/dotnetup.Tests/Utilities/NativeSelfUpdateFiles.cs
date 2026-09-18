// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection.PortableExecutable;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class NativeSelfUpdateFiles : IDisposable
{
    private readonly SelfUpdateTestFiles _files;

    public NativeSelfUpdateFiles()
    {
        var original = Environment.GetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE");
        var replacement = Environment.GetEnvironmentVariable("DOTNETUP_TEST_REPLACEMENT");
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement))
        {
            Assert.Inconclusive("Set DOTNETUP_TEST_EXECUTABLE and DOTNETUP_TEST_REPLACEMENT to two distinct native dotnetup releases.");
        }

        original = DotnetupTestUtilities.GetDotnetupExecutablePath();
        Assert.IsTrue(File.Exists(replacement), "DOTNETUP_TEST_REPLACEMENT must name an existing native executable.");
        AssertNative(original);
        AssertNative(replacement);

        _files = new SelfUpdateTestFiles();
        try
        {
            ReplacementPath = Path.Combine(Paths.DirectoryPath, "replacement" + DotnetupUtilities.ExeSuffix);
            File.Copy(original, Paths.InstalledPath, overwrite: true);
            File.Copy(replacement, ReplacementPath);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Paths.InstalledPath, File.GetUnixFileMode(original));
                File.SetUnixFileMode(ReplacementPath, File.GetUnixFileMode(replacement));
            }

            File.Delete(Paths.StagedPath);
            OriginalIdentity = SelfUpdatePaths.ReadVersionMetadata(Paths.InstalledPath);
            ReplacementIdentity = SelfUpdatePaths.ReadVersionMetadata(ReplacementPath);
            Assert.AreNotEqual(OriginalIdentity, ReplacementIdentity, "Publish the two binaries with different release versions.");
            var metadata = ReplacementIdentity.Split('|');
            Assert.StartsWith(metadata[0], GetExecutableVersion(ReplacementPath));
            var rid = metadata[1];
            Release = new ResolvedDownload(new Uri("https://example.invalid/native-dotnetup"), new string('0', 128),
                rid, ReleaseVersion.Parse(metadata[0]));
        }
        catch
        {
            _files.Dispose();
            throw;
        }
    }

    public SelfUpdatePaths Paths => _files.Paths;
    public string ReplacementPath { get; }
    public string OriginalIdentity { get; }
    public string ReplacementIdentity { get; }
    public ResolvedDownload Release { get; }
    public string StateDirectory => Path.Combine(Paths.DirectoryPath, "state");

    public SelfUpdateWorkflow CreateWorkflow(Action<ResolvedDownload, string>? download = null, SelfUpdateCoordinator? coordinator = null)
        => new(Paths, OriginalIdentity, () => Release, download ?? CopyReplacement, coordinator);

    public void CopyReplacement(ResolvedDownload release, string destination)
    {
        Assert.AreSame(Release, release);
        Assert.AreEqual(Paths.StagedPath, destination);
        File.Copy(ReplacementPath, destination);
    }

    public Process Start(string[] arguments, bool enableTelemetry = false, string? dotnetRoot = null)
    {
        var start = new ProcessStartInfo(Paths.InstalledPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Paths.DirectoryPath,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_DOTNETUP_DATA_DIR"] = Path.Combine(StateDirectory, "data");
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(StateDirectory, "home");
        start.Environment["DOTNET_TESTHOOK_DEFAULT_DOTNET_PATH"] = dotnetRoot ?? Paths.DirectoryPath;
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["NO_COLOR"] = "1";
        start.Environment[Constants.Telemetry.TelemetryOptOutEnvVar] = enableTelemetry ? "0" : "1";
        start.Environment[Constants.Telemetry.DisableTraceExportEnvVar] = "0";
        start.Environment[Constants.Telemetry.StoragePathEnvVar] = Path.Combine(StateDirectory, "storage");
        start.Environment[Constants.Telemetry.DiskLogPathEnvVar] = Path.Combine(StateDirectory, "telemetry.json");
        start.Environment[Constants.Telemetry.TestShutdownBudgetPathEnvVar] = Path.Combine(StateDirectory, "drainer-budget.txt");
        start.Environment[Constants.Telemetry.E2EConnectionStringEnvVar] =
            "InstrumentationKey=00000000-0000-0000-0000-000000000001;IngestionEndpoint=http://127.0.0.1:1/";
        start.Environment[Constants.Telemetry.ForceLocalDeliveryEnvVar] = "0";
        start.Environment["CI"] = "false";
        start.Environment.Remove("TF_BUILD");
        start.Environment.Remove("GITHUB_ACTIONS");
        var process = Process.Start(start);
        Assert.IsNotNull(process);
        return process;
    }

    public string Run(string[] arguments, bool succeeds = true, bool enableTelemetry = false)
    {
        using var process = Start(arguments, enableTelemetry);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            Assert.IsTrue(process.WaitForExit(20_000), "Native dotnetup timed out.");
            var output = stdout.GetAwaiter().GetResult();
            var error = stderr.GetAwaiter().GetResult();
            if (succeeds)
            {
                Assert.AreEqual(0, process.ExitCode, output + error);
                Assert.AreEqual(string.Empty, error);
            }
            else
            {
                Assert.AreNotEqual(0, process.ExitCode, output + error);
            }

            return output + error;
        }
        finally
        {
            Stop(process);
        }
    }

    public static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            Assert.IsTrue(process.WaitForExit(10_000));
        }
    }

    public void Dispose() => _files.Dispose();

    private static string GetExecutableVersion(string path)
    {
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("--version");
        start.Environment[Constants.Telemetry.TelemetryOptOutEnvVar] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["NO_COLOR"] = "1";
        using var process = Process.Start(start);
        Assert.IsNotNull(process);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(20_000), "Native dotnetup --version timed out.");
        Assert.AreEqual(0, process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        Assert.AreEqual(string.Empty, stderr.GetAwaiter().GetResult());
        var version = stdout.GetAwaiter().GetResult().Trim();
        Assert.IsFalse(string.IsNullOrEmpty(version), "Native dotnetup --version returned no version.");
        return version;
    }

    private static void AssertNative(string path)
    {
        using var stream = File.OpenRead(path);
        if (OperatingSystem.IsWindows())
        {
            using var reader = new PEReader(stream);
            Assert.IsNull(reader.PEHeaders.CorHeader, "These tests require NativeAOT executables, not managed assemblies.");
            Assert.AreEqual(0, reader.PEHeaders.PEHeader!.CorHeaderTableDirectory.Size);
            return;
        }

        Span<byte> elfMagic = stackalloc byte[4];
        stream.ReadExactly(elfMagic);
        Assert.IsTrue(elfMagic.SequenceEqual("\u007fELF"u8), "Linux native tests require an ELF executable.");
    }
}