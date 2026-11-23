// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SystemInstallDiscoveryFailureTests
{
    private static readonly InstallArchitecture s_nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow(typeof(IOException))]
    [DataRow(typeof(UnauthorizedAccessException))]
    public void GetExistingSystemInstalls_ReportsUnreadableRootAndContinues(Type exceptionType)
    {
        var failingRoot = new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-unreadable-root"), s_nativeArch);
        var healthyRoot = new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-healthy-root"), s_nativeArch);
        var healthyInstall = new DotnetInstall(healthyRoot, new ReleaseVersion("11.0.100"), InstallComponent.SDK);
        var failure = (Exception)Activator.CreateInstance(exceptionType, "Access to the root failed.")!;

        List<DotnetInstall> installs = [];
        Activity activity = RunWithTelemetryAndConsole(out string output, () =>
            installs = DotnetEnvironmentManager.GetExistingSystemInstalls(
                [failingRoot, healthyRoot],
                root => root == failingRoot ? throw failure : new[] { healthyInstall }));

        installs.Should().Equal(healthyInstall);
        output.Should().Contain("Warning:").And.Contain(failingRoot.Path).And.Contain("Access to the root failed.");
        activity.GetTagItem(TelemetryTagNames.SystemInstallEnumerationFailed).Should().Be("true");
        activity.GetTagItem(TelemetryTagNames.SystemInstallEnumerationError).Should().Be(exceptionType.Name);
        activity.GetTagItem(TelemetryTagNames.SystemInstallEnumerationPath).Should().Be(failingRoot.Path);
    }

    [TestMethod]
    public void GetExistingSystemInstalls_PropagatesUnexpectedExceptions()
    {
        var root = new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-broken-root"), s_nativeArch);

        Action act = () => DotnetEnvironmentManager.GetExistingSystemInstalls(
            [root], _ => throw new InvalidOperationException("Unexpected."));

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void GetExistingSystemInstalls_DoesNotReadRootsOfOtherArchitectures()
    {
        InstallArchitecture foreignArch = Enum.GetValues<InstallArchitecture>().First(arch => arch != s_nativeArch);
        var foreignRoot = new DotnetInstallRoot(Path.Combine(Path.GetTempPath(), "dotnetup-foreign-root"), foreignArch);
        var enumerated = new List<DotnetInstallRoot>();

        DotnetEnvironmentManager.GetExistingSystemInstalls([foreignRoot], root =>
        {
            enumerated.Add(root);
            return [];
        });

        enumerated.Should().BeEmpty();
    }

    private static Activity RunWithTelemetryAndConsole(out string output, Action action)
    {
        using var source = new ActivitySource(nameof(SystemInstallDiscoveryFailureTests));
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var writer = new StringWriter();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        AnsiConsole.Console.Profile.Width = 500;

        try
        {
            using Activity activity = source.StartActivity("discover")!;
            action();
            output = writer.ToString();
            return activity;
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
