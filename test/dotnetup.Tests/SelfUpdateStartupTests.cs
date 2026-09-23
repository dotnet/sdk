// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;
using DotnetCommand = Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet.DotnetCommand;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateStartupTests : SdkTest
{
    [TestMethod]
    public void RemovedBuildIdentityOptionIsRejected()
    {
        Assert.IsNotEmpty(Parser.Parse(["--build-identity"]).Errors);
    }

    [TestMethod]
    public void BlockedInstallCommandsDoNotInitializeReleaseManifestServices()
    {
        using var files = new SelfUpdateTestFiles();
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(updater);
        InstallCommand[] commands =
        [
            new SdkInstallCommand(Parser.Parse(["sdk", "install"])),
            new RuntimeInstallCommand(Parser.Parse(["runtime", "install"])),
            new InitCommand(Parser.Parse(["init"])),
        ];
        var resolverField = typeof(InstallCommand).GetField("_channelVersionResolver", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var command in commands)
        {
            var resolver = (Lazy<ChannelVersionResolver>)resolverField.GetValue(command)!;
            Assert.IsFalse(resolver.IsValueCreated);
            using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);

            Assert.AreEqual(1, command.Execute());
            Assert.IsFalse(resolver.IsValueCreated);
        }
    }

    [TestMethod]
    public void OnlyForwardingAndSelfUpdateCommandsOverrideDefaultSafety()
    {
        var safeProperty = typeof(CommandBase).GetProperty("SafeDuringSelfUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var overrides = typeof(CommandBase).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(CommandBase).IsAssignableFrom(type))
            .Where(type => type.GetProperty(safeProperty.Name, BindingFlags.Instance | BindingFlags.NonPublic)!.DeclaringType != typeof(CommandBase))
            .ToArray();

        Assert.HasCount(2, overrides);
        Assert.Contains(typeof(DotnetCommand), overrides);
        Assert.Contains(typeof(SelfUpdateCommand), overrides);
        Assert.IsTrue((bool)safeProperty.GetValue(new DotnetCommand(Parser.Parse(["dotnet"])))!);
        Assert.IsTrue((bool)safeProperty.GetValue(new SelfUpdateCommand(Parser.Parse(["self", "update"])))!);
        Assert.IsFalse((bool)safeProperty.GetValue(new SelfUpdateStartupCommand(new RootCommand().Parse([])))!);
    }

    [TestMethod]
    public void DirectManagedParserInvocationWithoutProcessContextRunsCommandBody()
    {
        Assert.IsNull(SelfUpdateInvocation.Current);
        var root = new RootCommand();
        SelfUpdateStartupCommand? command = null;
        root.SetAction(result =>
        {
            command = new SelfUpdateStartupCommand(result);
            return command.Execute();
        });

        Assert.AreEqual(0, Parser.Invoke(root.Parse([])));
        Assert.IsNotNull(command);
        Assert.IsTrue(command.Ran);
        Assert.IsNull(SelfUpdateInvocation.Current);
    }

    [TestMethod]
    public async Task ProductionParserDispatchReachesGateBeforeInstallationStateAccess()
    {
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(ProductionParserDispatchReachesGateBeforeInstallationStateAccess)).ConfigureAwait(false);
            return;
        }

        using var environment = new TestEnvironment(configureEnvironment: true);
        using var files = new SelfUpdateTestFiles();
        File.WriteAllText(environment.ManifestPath, "unreadable-installation-manifest");
        using var manifest = new FileStream(environment.ManifestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(updater);
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Constants.Telemetry.BootstrapperSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        string[][] commands =
        [
            [],
            ["--info"],
            ["list"],
            ["install"],
            ["update"],
            ["uninstall", "10.0"],
            ["sdk", "install"],
            ["sdk", "update"],
            ["sdk", "uninstall", "10.0"],
            ["runtime", "install"],
            ["runtime", "update"],
            ["runtime", "uninstall", "10.0"],
            ["init"],
            ["env", "show"],
            ["env", "set", "none"],
            ["env", "clear"],
            ["env", "script"],
            ["print-env-script"],
            ["elevatedsystempath", "removedotnet", "blocked-log.txt", "--dotnet-dir", environment.InstallPath],
        ];
        foreach (var args in commands)
        {
            activities.Clear();
            DotnetupTelemetry.Instance.IsShellStartupCommand = false;
            using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);
            using var rootOperation = DotnetupTelemetry.Instance.StartTrackedProcess("dotnetup");
            var parsed = Parser.Parse(args);
            Assert.IsEmpty(parsed.Errors, string.Join(' ', args));
            Assert.IsNotNull(rootOperation.Activity);
            Assert.IsEmpty(activities);

            Assert.AreEqual(1, Parser.Invoke(parsed), string.Join(' ', args));
            Assert.ContainsSingle(activities);
            Assert.AreEqual(nameof(DotnetInstallErrorCode.DotnetupUpdateInProgress), activities[0].GetTagItem("error.type"), string.Join(' ', args));
            Assert.IsFalse(DotnetupTelemetry.Instance.IsShellStartupCommand);
            Assert.AreEqual(0L, manifest.Position);
            Assert.IsFalse(File.Exists("blocked-log.txt"));
        }
    }

    [TestMethod]
    public async Task GateFailuresRecordCommandAndRootTelemetry()
    {
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(GateFailuresRecordCommandAndRootTelemetry)).ConfigureAwait(false);
            return;
        }

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Constants.Telemetry.BootstrapperSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        Assert.IsTrue(DotnetupTelemetry.Instance.Enabled);

        foreach (bool busy in new[] { true, false })
        {
            using var files = new SelfUpdateTestFiles();
            using var updater = busy ? ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath) : null;
            if (busy)
            {
                Assert.IsNotNull(updater);
            }

            using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath,
                busy ? SelfUpdateTestFiles.OriginalVersion : SelfUpdateTestFiles.ReplacementVersion);
            var root = new RootCommand();
            root.Options.Add(new Option<bool>("--flag"));
            var command = new SelfUpdateStartupCommand(root.Parse(["--flag"]));
            activities.Clear();
            using var rootOperation = DotnetupTelemetry.Instance.StartTrackedProcess("dotnetup");

            Assert.AreEqual(1, command.Execute());
            Assert.IsFalse(command.Ran);
            Assert.ContainsSingle(activities);
            var commandActivity = activities[0];
            string expectedError = (busy
                ? DotnetInstallErrorCode.DotnetupUpdateInProgress
                : DotnetInstallErrorCode.DotnetupExecutableChanged).ToString();
            Assert.AreEqual("command/selfupdate-startup-test", commandActivity.OperationName);
            Assert.AreEqual(expectedError, commandActivity.GetTagItem("error.type"));
            Assert.AreEqual("user", commandActivity.GetTagItem("error.category"));
            Assert.AreEqual(1, commandActivity.GetTagItem(TelemetryTagNames.ExitCode));
            Assert.AreEqual(ActivityStatusCode.Error, commandActivity.Status);
            Assert.IsNull(commandActivity.GetTagItem("option.flag"));
            Assert.IsNotNull(rootOperation.Activity);
            Assert.AreSame(rootOperation.Activity, commandActivity.Parent);
            Assert.AreEqual(expectedError, rootOperation.Activity.GetTagItem("error.type"));
        }
    }

    [TestMethod]
    public void EnvScriptConstructionDoesNotMarkShellStartupOrStartRootOperation()
    {
        using var files = new SelfUpdateTestFiles();
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);
        var telemetry = DotnetupTelemetry.Instance;
        bool previous = telemetry.IsShellStartupCommand;
        try
        {
            telemetry.IsShellStartupCommand = false;
            var previousActivity = Activity.Current;
            _ = new EnvScriptCommand(Parser.Parse(["env", "script", "--shell", "pwsh"]));

            Assert.IsFalse(telemetry.IsShellStartupCommand);
            Assert.AreSame(previousActivity, Activity.Current);
        }
        finally
        {
            telemetry.IsShellStartupCommand = previous;
        }
    }

    [TestMethod]
    public void IdleCommandRetainsActivityLeaseUntilInvocationIsDisposed()
    {
        using var files = new SelfUpdateTestFiles();
        var previous = SelfUpdateInvocation.Current;
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);
        var command = new SelfUpdateStartupCommand(new RootCommand().Parse([]));
        using var rootOperation = DotnetupTelemetry.Instance.StartTrackedProcess("dotnetup");

        Assert.AreEqual(0, command.Execute());
        Assert.IsTrue(command.Ran);
        Assert.AreSame(invocation, SelfUpdateInvocation.Current);
        using (var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath))
        {
            Assert.IsNull(update);
        }

        rootOperation.Dispose();
        using (var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath))
        {
            Assert.IsNull(update);
        }

        invocation.Dispose();
        Assert.AreSame(previous, SelfUpdateInvocation.Current);
        using var released = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(released);
    }

    [TestMethod]
    public void BusyGateSuppressesCommandBody()
    {
        using var files = new SelfUpdateTestFiles();
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(updater);
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);
        var command = new SelfUpdateStartupCommand(new RootCommand().Parse([]));

        Assert.AreEqual(1, command.Execute());
        Assert.IsFalse(command.Ran);
    }

    [TestMethod]
    public void StaleImageSuppressesCommandBodyAndReleasesActivityLease()
    {
        using var files = new SelfUpdateTestFiles();
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementVersion);
        var command = new SelfUpdateStartupCommand(new RootCommand().Parse([]));

        Assert.AreEqual(1, command.Execute());
        Assert.IsFalse(command.Ran);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public async Task ProgramRecordsEncodingSetupAndDisposalFailuresOnEarlyRoot()
    {
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(ProgramRecordsEncodingSetupAndDisposalFailuresOnEarlyRoot)).ConfigureAwait(false);
            return;
        }

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Constants.Telemetry.BootstrapperSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        foreach (var failDuringDisposal in new[] { false, true })
        {
            activities.Clear();
            Activity? root = null;
            var disposed = false;
            var failure = new InvalidOperationException("Injected encoding setup or disposal failure.");
            var exitCode = DotnetupProgram.InvokeCommand(["--version"], () =>
            {
                root = Activity.Current;
                Assert.IsNotNull(root);
                Assert.AreEqual("dotnetup", root.OperationName);
                if (!failDuringDisposal)
                {
                    throw failure;
                }

                return new StartupEncodingScope(() =>
                {
                    disposed = true;
                    Assert.AreSame(root, Activity.Current);
                    throw failure;
                });
            });

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(failDuringDisposal, disposed);
            Assert.ContainsSingle(activities);
            Assert.AreSame(root, activities[0]);
            Assert.AreEqual(ActivityStatusCode.Error, activities[0].Status);
            Assert.AreEqual(1, activities[0].GetTagItem(TelemetryTagNames.ExitCode));
            Assert.IsNotNull(activities[0].GetTagItem("error.type"));
            Assert.AreNotEqual("ParseError", activities[0].GetTagItem("error.type"));
        }
    }

    [TestMethod]
    public async Task ProgramVersionOptionUsesNormalSetupAndTelemetry()
    {
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(ProgramVersionOptionUsesNormalSetupAndTelemetry)).ConfigureAwait(false);
            return;
        }

        var started = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Constants.Telemetry.BootstrapperSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => started++,
        };
        ActivitySource.AddActivityListener(listener);
        string[][] invocations = [["--version"], ["--version", "--interactive", "false"]];
        foreach (var args in invocations)
        {
            var setupCalled = false;
            Assert.AreEqual(0, DotnetupProgram.InvokeCommand(args, () =>
            {
                setupCalled = true;
                return new StartupEncodingScope(() => { });
            }));
            Assert.IsTrue(setupCalled);
        }

        Assert.AreEqual(invocations.Length, started);
    }

    [TestMethod]
    public async Task ProgramPrivateVersionEncodingOverridesOptOutAndCodePage()
    {
        const string stdoutMarker = "version-stdout-é-日本語";
        const string stderrMarker = "version-stderr-é-日本語";
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(ProgramPrivateVersionEncodingOverridesOptOutAndCodePage),
                stdoutMarker, stderrMarker).ConfigureAwait(false);
            return;
        }

        var originalEncoding = Console.OutputEncoding;
        var originalOptOut = Environment.GetEnvironmentVariable("DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING");
        var originalRequest = Environment.GetEnvironmentVariable(SelfUpdateVerifier.Utf8EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING", "1");
            foreach (var initialEncoding in new[] { Encoding.Latin1, Encoding.Unicode })
            {
                foreach (var privateRequest in new[] { false, true })
                {
                    Console.OutputEncoding = initialEncoding;
                    Environment.SetEnvironmentVariable(SelfUpdateVerifier.Utf8EnvironmentVariable, privateRequest ? "1" : null);
                    var expectedCodePage = privateRequest ? Encoding.UTF8.CodePage : initialEncoding.CodePage;
                    Assert.AreEqual(0, DotnetupProgram.InvokeCommand(["--version"], () => new StartupEncodingScope(() =>
                    {
                        Assert.AreEqual(expectedCodePage, Console.OutputEncoding.CodePage);
                        if (privateRequest)
                        {
                            Assert.AreEqual(Encoding.UTF8.CodePage, Console.Out.Encoding.CodePage);
                            Assert.AreEqual(Encoding.UTF8.CodePage, Console.Error.Encoding.CodePage);
                            Assert.IsEmpty(Console.OutputEncoding.GetPreamble());
                            Console.WriteLine(stdoutMarker);
                        }
                    })));
                }

                Console.OutputEncoding = initialEncoding;
                Environment.SetEnvironmentVariable(SelfUpdateVerifier.Utf8EnvironmentVariable, "1");
                Assert.AreEqual(1, DotnetupProgram.InvokeCommand(["--version"], () => throw new IOException(stderrMarker)));
            }
        }
        finally
        {
            Console.OutputEncoding = originalEncoding;
            Environment.SetEnvironmentVariable("DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING", originalOptOut);
            Environment.SetEnvironmentVariable(SelfUpdateVerifier.Utf8EnvironmentVariable, originalRequest);
        }
    }

    [TestMethod]
    public void ProductionVersionParserBypassesBothBusyLocksAndCleanup()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        File.WriteAllText(files.BackupPath, "retained");
        File.SetLastWriteTimeUtc(files.BackupPath, DateTime.UtcNow.AddDays(-8));
        using var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);

        Assert.AreEqual(0, Parser.Invoke(["--version"]));

        Assert.IsTrue(File.Exists(files.BackupPath));
        Assert.IsFalse(File.Exists(files.Paths.InstalledPath + ".invocations"));
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNull(update);
        Assert.IsNull(activity);
    }

    [TestMethod]
    public async Task ProgramShowsFirstRunNoticeBeforeGateRejection()
    {
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(ProgramShowsFirstRunNoticeBeforeGateRejection)).ConfigureAwait(false);
            return;
        }

        using var files = new SelfUpdateTestFiles();
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalVersion);
        var previousNoLogo = Environment.GetEnvironmentVariable("DOTNET_NOLOGO");
        var previousError = Console.Error;
        using var output = new StringWriter();
        DotnetupPaths.SetTestDataDirectoryOverride(Path.Combine(files.Paths.DirectoryPath, "notice-data"));
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_NOLOGO", "0");
            Console.SetError(output);
            Assert.IsFalse(File.Exists(DotnetupPaths.TelemetrySentinelPath));

            Assert.AreEqual(1, DotnetupProgram.InvokeCommand(["list"], () => new StartupEncodingScope(() => { })));

            Assert.IsTrue(File.Exists(DotnetupPaths.TelemetrySentinelPath));
            Assert.Contains(Microsoft.DotNet.Tools.Bootstrapper.Strings.TelemetryNotice, output.ToString());
            using var competing = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
            Assert.IsNull(competing);
        }
        finally
        {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable("DOTNET_NOLOGO", previousNoLogo);
            DotnetupPaths.ClearTestDataDirectoryOverride();
        }
    }

    [TestMethod]
    public async Task ProgramVersionAndHelpStillRunSetup()
    {
        if (Environment.GetEnvironmentVariable(SelfUpdateStartupTelemetryProcess.ChildEnvironmentVariable) != "1")
        {
            await SelfUpdateStartupTelemetryProcess.RunAsync(
                typeof(SelfUpdateStartupTests).FullName + "." + nameof(ProgramVersionAndHelpStillRunSetup)).ConfigureAwait(false);
            return;
        }

        string[][] invocations = [["--help", "--version"], ["--version", "--help"], ["dotnet", "--version"], ["self", "update", "--help"]];
        foreach (var args in invocations)
        {
            var setupCalled = false;
            Assert.AreEqual(1, DotnetupProgram.InvokeCommand(args, () =>
            {
                setupCalled = true;
                throw new InvalidOperationException("Injected setup failure before command dispatch.");
            }));
            Assert.IsTrue(setupCalled, string.Join(' ', args));
        }
    }
}