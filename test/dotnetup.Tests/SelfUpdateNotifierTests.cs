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
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    public SelfUpdateNotifierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-notifier-tests", Guid.NewGuid().ToString("N"));
    }

    public TestContext TestContext { get; set; } = null!;

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [TestMethod]
    [DataRow("0.1.0", "stable", "0.2.0", true)]
    [DataRow("0.2.0-preview.1.26400.1", "preview", "0.2.0-preview.1.26465.1", true)]
    [DataRow("0.2.0-dev.1", "daily", "0.2.0-dev.2", true)]
    [DataRow("0.2.0-preview.1.26465.1", "preview", "0.2.0-preview.1.26465.1", false)]
    [DataRow("0.2.0-preview.1.26465.1", "preview", "0.2.0-preview.1.26400.1", false)]
    [DataRow("0.2.0-dev.1", "daily", "0.2.0-preview.1.26465.1", false)]
    public void Refresh_NotifiesOnlyForNewerVersionOnCurrentChannel(
        string loadedVersion,
        string channel,
        string latestVersion,
        bool expectedNotice)
    {
        string? requestedChannel = null;
        var notifier = CreateNotifier(loadedVersion, requested =>
        {
            requestedChannel = requested;
            return ReleaseVersion.Parse(latestVersion);
        }, SelfUpdateDefaultChannel.FromLoadedVersion(loadedVersion));

        var output = CaptureOutput(() =>
        {
            notifier.Refresh();
            notifier.ShowIfUpdateAvailable();
        });

        requestedChannel.Should().Be(channel);
        output.Contains(BootstrapperStrings.SelfUpdateAvailableNotice, StringComparison.Ordinal)
            .Should().Be(expectedNotice);
        var expectedMarkerSuffix = expectedNotice ? "" : $"_{latestVersion}";
        Directory.GetFiles(_tempDir, "*.dnupc").Should().ContainSingle()
            .Which.Should().EndWith($"{_time.GetUtcNow().ToUnixTimeSeconds()}{expectedMarkerSuffix}.dnupc");
    }

    [TestMethod]
    public void StartRefreshIfStale_SkipsNetworkWhileMarkerIsFresh()
    {
        int calls = 0;
        var notifier = CreateNotifier("0.1.0-preview.1", _ =>
        {
            Interlocked.Increment(ref calls);
            return ReleaseVersion.Parse("0.1.0-preview.1");
        });

        notifier.StartRefreshIfStale();
        WaitForRefresh(notifier);

        _time.Advance(TimeSpan.FromHours(23));
        var secondNotifier = CreateNotifier("0.2.0", _ => throw new InvalidOperationException("Should not refresh."), "stable");
        secondNotifier.StartRefreshIfStale();

        secondNotifier.RefreshTask.Should().BeNull();
        calls.Should().Be(1);
    }

    [TestMethod]
    [DataRow("0.2.0-preview.2", "preview")]
    [DataRow("1.0.0", "stable")]
    public void StartRefreshIfStale_FreshMarkerAppliesAcrossVersionsAndChannels(
        string secondLoadedVersion,
        string secondChannel)
    {
        CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.1.0-preview.1")).Refresh();
        var secondNotifier = CreateNotifier(
            secondLoadedVersion,
            _ => throw new InvalidOperationException("Should not refresh."),
            secondChannel);

        secondNotifier.StartRefreshIfStale();

        secondNotifier.RefreshTask.Should().BeNull();
    }

    [TestMethod]
    public void StartRefreshIfStale_RefreshesAfterIntervalAndDeletesOldMarker()
    {
        var notifier = CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.1.0-preview.1"));
        notifier.Refresh();
        var oldMarker = Directory.GetFiles(_tempDir, "*.dnupc").Should().ContainSingle().Subject;

        _time.Advance(TimeSpan.FromHours(25));
        notifier.StartRefreshIfStale();
        WaitForRefresh(notifier);

        var marker = Directory.GetFiles(_tempDir, "*.dnupc").Should().ContainSingle().Subject;
        marker.Should().NotBe(oldMarker);
        File.Exists(oldMarker).Should().BeFalse();
    }

    [TestMethod]
    public void Refresh_DoesNotDeleteNewerMarkerCreatedByAnotherCheck()
    {
        var newerTime = new ManualTimeProvider(_time.GetUtcNow().AddMinutes(1));
        var olderNotifier = CreateNotifier("0.1.0-preview.1", _ =>
        {
            CreateNotifier("0.1.0-preview.1", _ => null, timeProvider: newerTime).Refresh();
            return null;
        });

        olderNotifier.Refresh();

        Directory.GetFiles(_tempDir, "*.dnupc").Should().HaveCount(2);
    }

    [TestMethod]
    public void StartRefreshIfStale_RefreshesWhenClockMovedBackward()
    {
        CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.1.0-preview.1")).Refresh();
        _time.Advance(TimeSpan.FromHours(-1));

        var notifier = CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.1.0-preview.1"));
        notifier.StartRefreshIfStale();

        WaitForRefresh(notifier);
    }

    [TestMethod]
    public void Refresh_ResolverFailure_DoesNotCreateMarkerSoNextCommandRetries()
    {
        var notifier = CreateNotifier("0.1.0-preview.1", _ => throw new HttpRequestException("offline"));

        notifier.Refresh();
        Directory.GetFiles(_tempDir, "*.dnupc").Should().BeEmpty();

        notifier.StartRefreshIfStale();
        WaitForRefresh(notifier);
        Directory.GetFiles(_tempDir, "*.dnupc").Should().BeEmpty();
    }

    [TestMethod]
    public void Refresh_ChannelWithoutBuild_CreatesMarkerWithoutNotifying()
    {
        var notifier = CreateNotifier("1.0.0", _ => null, "stable");

        var output = CaptureOutput(() =>
        {
            notifier.Refresh();
            notifier.ShowIfUpdateAvailable();
        });
        notifier.StartRefreshIfStale();

        output.Should().BeEmpty();
        notifier.RefreshTask.Should().BeNull();
        Directory.GetFiles(_tempDir, "*.dnupc").Should().ContainSingle();
    }

    [TestMethod]
    public void ShowIfUpdateAvailable_PrefersVersionedMarkerAtSameTimestamp()
    {
        Directory.CreateDirectory(_tempDir);
        var timestamp = _time.GetUtcNow().ToUnixTimeSeconds();
        File.Create(Path.Combine(_tempDir, $"{timestamp}.dnupc")).Dispose();
        File.Create(Path.Combine(_tempDir, $"{timestamp}_0.2.0-preview.1.dnupc")).Dispose();
        var notifier = CreateNotifier("0.1.0-preview.1", _ => null);

        var output = CaptureOutput(notifier.ShowIfUpdateAvailable);

        output.Should().Contain(BootstrapperStrings.SelfUpdateAvailableNotice);
    }

    [TestMethod]
    public void ShowIfUpdateAvailable_ShowsOnlyOncePerRefresh()
    {
        var notifier = CreateNotifier("0.1.0-preview.1", _ => ReleaseVersion.Parse("0.2.0-preview.1"));
        notifier.Refresh();

        var firstOutput = CaptureOutput(notifier.ShowIfUpdateAvailable);
        var secondOutput = CaptureOutput(notifier.ShowIfUpdateAvailable);

        firstOutput.Should().Contain(BootstrapperStrings.SelfUpdateAvailableNotice);
        secondOutput.Should().BeEmpty();
        Directory.GetFiles(_tempDir, "*.dnupc").Should().ContainSingle()
            .Which.Should().EndWith($"{_time.GetUtcNow().ToUnixTimeSeconds()}.dnupc");
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

    private SelfUpdateNotifier CreateNotifier(
        string loadedVersion,
        Func<string, ReleaseVersion?> resolveLatest,
        string channel = Channel,
        TimeProvider? timeProvider = null)
        => new(ReleaseVersion.Parse(loadedVersion), channel, _tempDir, resolveLatest, timeProvider ?? _time);

    private void WaitForRefresh(SelfUpdateNotifier notifier)
    {
        notifier.RefreshTask.Should().NotBeNull();
        notifier.RefreshTask!.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken).Should().BeTrue();
    }

    private static string CaptureOutput(Action action)
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
        AnsiConsole.Console.Profile.Width = int.MaxValue;
        try
        {
            action();
            return output.ToString();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
