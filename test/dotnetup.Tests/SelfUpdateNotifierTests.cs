// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Update;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Spectre.Console;
using BootstrapperStrings = Microsoft.DotNet.Tools.Bootstrapper.Strings;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateNotifierTests : IDisposable
{
    private const string Channel = "preview";
    private readonly string _tempDir;
    private readonly string _statePath;
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    public SelfUpdateNotifierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-notifier-tests", Guid.NewGuid().ToString("N"));
        _statePath = Path.Combine(_tempDir, "data", "dotnetup.update-check.json");
    }

    public TestContext TestContext { get; set; } = null!;

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [TestMethod]
    public void Refresh_CachesNewerVersion_AndReportsIt()
    {
        var notifier = CreateNotifier("0.2.0-preview.1.26400.1", _ => ReleaseVersion.Parse("0.2.0-preview.1.26465.1"));

        notifier.GetAvailableUpdate().Should().BeNull();
        notifier.Refresh();

        notifier.GetAvailableUpdate().Should().Be(ReleaseVersion.Parse("0.2.0-preview.1.26465.1"));
        File.Exists(_statePath + ".lock").Should().BeFalse();
        Directory.GetFiles(Path.GetDirectoryName(_statePath)!, "*.tmp").Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("0.2.0-preview.1.26465.1")]
    [DataRow("0.2.0-preview.1.26465.1+abc123")]
    [DataRow("0.3.0-preview.1.26500.1")]
    [DataRow("0.2.0-dev")]
    public void GetAvailableUpdate_IgnoresSameOlderOrDifferentLabelBuilds(string loadedVersion)
    {
        var notifier = CreateNotifier(loadedVersion, _ => ReleaseVersion.Parse("0.2.0-preview.1.26465.1"));

        notifier.Refresh();

        notifier.GetAvailableUpdate().Should().BeNull();
    }

    [TestMethod]
    public void GetAvailableUpdate_IgnoresCacheForAnotherChannel()
    {
        CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.2.0-preview.1"), channel: "daily").Refresh();

        CreateNotifier("0.1.0-preview.1", _ => null).GetAvailableUpdate().Should().BeNull();
    }

    [TestMethod]
    public void StartRefreshIfStale_SkipsNetworkWhileCacheIsFresh()
    {
        int calls = 0;
        var notifier = CreateNotifier("0.1.0-preview.1", _ =>
        {
            Interlocked.Increment(ref calls);
            return ReleaseVersion.Parse("0.2.0-preview.1");
        });

        notifier.StartRefreshIfStale();
        notifier.RefreshTask.Should().NotBeNull();
        notifier.RefreshTask!.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken).Should().BeTrue();

        _time.Advance(TimeSpan.FromHours(23));
        var secondNotifier = CreateNotifier("0.1.0-preview.1", _ => throw new InvalidOperationException("Should not refresh."));
        secondNotifier.StartRefreshIfStale();
        secondNotifier.RefreshTask.Should().BeNull();
        secondNotifier.GetAvailableUpdate().Should().Be(ReleaseVersion.Parse("0.2.0-preview.1"));

        _time.Advance(TimeSpan.FromHours(2));
        notifier.StartRefreshIfStale();
        notifier.RefreshTask!.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken).Should().BeTrue();
        calls.Should().Be(2);
    }

    [TestMethod]
    public void StartRefreshIfStale_RefreshesWhenClockMovedBackward()
    {
        CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.2.0-preview.1")).Refresh();
        _time.Advance(TimeSpan.FromHours(-1));

        var notifier = CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.2.0-preview.1"));
        notifier.StartRefreshIfStale();

        notifier.RefreshTask.Should().NotBeNull();
        notifier.RefreshTask!.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken).Should().BeTrue();
    }

    [TestMethod]
    public void Refresh_ResolverFailure_LeavesCacheUnchangedForRetry()
    {
        CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.2.0-preview.1")).Refresh();
        var before = File.ReadAllText(_statePath);
        _time.Advance(TimeSpan.FromDays(2));

        var notifier = CreateNotifier("0.1.0-preview.1", _ => throw new HttpRequestException("offline"));
        notifier.Refresh();

        File.ReadAllText(_statePath).Should().Be(before);
        notifier.StartRefreshIfStale();
        notifier.RefreshTask.Should().NotBeNull();
    }

    [TestMethod]
    public void Refresh_ChannelWithoutBuild_IsCachedWithoutNotifying()
    {
        var notifier = CreateNotifier("1.0.0", _ => null, channel: "stable");

        notifier.Refresh();
        notifier.StartRefreshIfStale();

        notifier.RefreshTask.Should().BeNull();
        notifier.GetAvailableUpdate().Should().BeNull();
    }

    [TestMethod]
    public void Refresh_SkipsWhileAnotherProcessHoldsTheLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        using (new FileStream(_statePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            CreateNotifier("0.1.0-preview.1", _ => throw new InvalidOperationException("Should not refresh.")).Refresh();
        }

        File.Exists(_statePath).Should().BeFalse();
    }

    [TestMethod]
    public void GetAvailableUpdate_CorruptCache_ReturnsNull()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        File.WriteAllText(_statePath, "not valid json{{{");

        CreateNotifier("0.1.0-preview.1", _ => null).GetAvailableUpdate().Should().BeNull();
    }

    [TestMethod]
    public void ShowIfUpdateAvailable_WritesNoticeOnlyWhenNewer()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(output),
        });
        // Avoid wrapping the long notice at the default 80-column test width.
        AnsiConsole.Console.Profile.Width = int.MaxValue;
        try
        {
            var notifier = CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.2.0-preview.1"));
            notifier.ShowIfUpdateAvailable();
            output.ToString().Should().BeEmpty();

            notifier.Refresh();
            notifier.ShowIfUpdateAvailable();

            output.ToString().Should().Contain(BootstrapperStrings.SelfUpdateAvailableNotice);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    [TestMethod]
    public void Start_ReturnsNullWhenNotInteractive()
    {
        SelfUpdateNotifier.Start(interactive: false).Should().BeNull();
    }

    [TestMethod]
    public void OnlyInstallAndUpdateCommandsShowUpdateNotification()
    {
        var property = typeof(CommandBase).GetProperty("ShowsUpdateNotification", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var overrides = typeof(CommandBase).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(CommandBase).IsAssignableFrom(type))
            .Where(type => type.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.NonPublic)!.DeclaringType != typeof(CommandBase))
            .ToArray();

        // Bare 'dotnetup' and 'dotnetup install' run SdkInstallCommand; 'dotnetup update' runs SdkUpdateCommand.
        overrides.Should().BeEquivalentTo(
        [
            typeof(SdkInstallCommand),
            typeof(RuntimeInstallCommand),
            typeof(SdkUpdateCommand),
            typeof(RuntimeUpdateCommand),
        ]);
        ((bool)property.GetValue(new SdkInstallCommand(Parser.Parse([])))!).Should().BeTrue();
        ((bool)property.GetValue(new SdkUpdateCommand(Parser.Parse(["update"])))!).Should().BeTrue();
    }

    private SelfUpdateNotifier CreateNotifier(string loadedVersion, Func<string, ReleaseVersion?> resolveLatest, string channel = Channel)
        => new(ReleaseVersion.Parse(loadedVersion), channel, _statePath, resolveLatest, _time);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
