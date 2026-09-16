// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
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
            using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);

            Assert.AreEqual(1, command.Execute());
            using var rootOperation = invocation.RootOperation;
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
            using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);
            var parsed = Parser.Parse(args);
            Assert.IsEmpty(parsed.Errors, string.Join(' ', args));
            Assert.IsNull(invocation.RootOperation);
            Assert.IsEmpty(activities);

            Assert.AreEqual(1, Parser.Invoke(parsed), string.Join(' ', args));
            using var rootOperation = invocation.RootOperation;
            Assert.IsNotNull(rootOperation);
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
                busy ? SelfUpdateTestFiles.OriginalIdentity : SelfUpdateTestFiles.ReplacementIdentity);
            var root = new RootCommand();
            root.Options.Add(new Option<bool>("--flag"));
            var command = new SelfUpdateStartupCommand(root.Parse(["--flag"]));
            activities.Clear();

            Assert.AreEqual(1, command.Execute());
            using var rootOperation = invocation.RootOperation;
            Assert.IsNotNull(rootOperation);
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
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);
        var telemetry = DotnetupTelemetry.Instance;
        bool previous = telemetry.IsShellStartupCommand;
        try
        {
            telemetry.IsShellStartupCommand = false;
            _ = new EnvScriptCommand(Parser.Parse(["env", "script", "--shell", "pwsh"]));

            Assert.IsFalse(telemetry.IsShellStartupCommand);
            Assert.IsNull(invocation.RootOperation);
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
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);
        var command = new SelfUpdateStartupCommand(new RootCommand().Parse([]));

        Assert.AreEqual(0, command.Execute());
        using var rootOperation = invocation.RootOperation;
        Assert.IsNotNull(rootOperation);
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
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);
        var command = new SelfUpdateStartupCommand(new RootCommand().Parse([]));

        Assert.AreEqual(1, command.Execute());
        using var rootOperation = invocation.RootOperation;
        Assert.IsNotNull(rootOperation);
        Assert.IsFalse(command.Ran);
    }

    [TestMethod]
    public void StaleImageSuppressesCommandBodyAndReleasesActivityLease()
    {
        using var files = new SelfUpdateTestFiles();
        using var invocation = new SelfUpdateInvocation(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity);
        var command = new SelfUpdateStartupCommand(new RootCommand().Parse([]));

        Assert.AreEqual(1, command.Execute());
        using var rootOperation = invocation.RootOperation;
        Assert.IsNotNull(rootOperation);
        Assert.IsFalse(command.Ran);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(update);
    }
}