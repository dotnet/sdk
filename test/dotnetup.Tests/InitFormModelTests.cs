// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InitFormModelTests
{
    [TestMethod]
    [DataRow("9.0")]
    [DataRow("9.0.102")]
    internal void CommandLineVersionOrChannel_IsSelectedByDefault(string versionOrChannel)
    {
        var model = CreateModel(new DefaultChannelDisplay(versionOrChannel, GlobalJsonPath: null));

        FormField channelField = model.Fields[0];
        channelField.DisplayValue.Should().Be(versionOrChannel);
        channelField.IsChangedFromDefault.Should().BeFalse();
        model.SelectedChannel().Should().Be(versionOrChannel);
    }

    [TestMethod]
    public void CommandLineLatestChannel_IsNotListedTwice()
    {
        var model = CreateModel(new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null));

        model.Fields[0].Choices
            .Count(choice => choice.Title == ChannelVersionResolver.LatestChannel)
            .Should().Be(1);
    }

    [TestMethod]
    public void ChangedRuntimeChannel_PreservesRuntimeComponent()
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        List<ResolvedInstallRequest> requests =
        [
            new(
                new DotnetInstallRequest(
                    installRoot,
                    new UpdateChannel("9.0"),
                    InstallComponent.Runtime,
                    new InstallRequestOptions()),
                new ReleaseVersion("9.0.12")),
        ];

        MinimalInstallSpec[] specs = InitWorkflows.BuildChangedChannelSpecs(requests, "10.0");

        specs.Should().ContainSingle();
        specs[0].Component.Should().Be(InstallComponent.Runtime);
        specs[0].VersionOrChannel.Should().Be("10.0");
    }

    [TestMethod]
    public void ChangingChannel_RefreshesDisplayedMigrationCandidates()
    {
        var model = CreateModel(
            new DefaultChannelDisplay("10.0.1xx", GlobalJsonPath: null),
            [
                CreateMigration("10.0.1xx", "10.0.100"),
                CreateMigration("9.0.3xx", "9.0.300"),
            ]);
        FormField migrationField = model.Fields.Single(field => field.Label == "Migrate system installs");

        model.BuildDetail(migrationField, choiceIndex: 0).Lines
            .Should().ContainSingle().Which.Value.Should().Be("9.0.300");

        FormField channelField = model.Fields[0];
        int customIndex = channelField.Choices.Count - 1;
        channelField.SetCustomValue(customIndex, "9.0.3xx");

        model.BuildDetail(migrationField, choiceIndex: 0).Lines
            .Should().ContainSingle().Which.Value.Should().Be("10.0.100");
    }

    [TestMethod]
    public void ChangingChannel_ShowsPreviouslyRedundantMigrationField()
    {
        var model = CreateModel(
            new DefaultChannelDisplay("10.0.1xx", GlobalJsonPath: null),
            [CreateMigration("10.0.1xx", "10.0.100")]);
        FormField migrationField = model.Fields.Single(field => field.Label == "Migrate system installs");
        migrationField.IsVisible.Should().BeFalse();

        FormField channelField = model.Fields[0];
        int customIndex = channelField.Choices.Count - 1;
        channelField.SetCustomValue(customIndex, "9.0.3xx");

        migrationField.IsVisible.Should().BeTrue();
        model.MigrateSelected().Should().BeTrue();
    }

    private static InitFormModel CreateModel(
        DefaultChannelDisplay channelDisplay,
        List<MigrationWorkflow.MigrationSelection>? migrations = null)
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var plan = new WalkthroughPlan(
            installRoot,
            DotnetAccessMode.None,
            migrations ?? [],
            channelDisplay,
            [new MinimalInstallSpec(InstallComponent.SDK, channelDisplay.ChannelLabel)]);

        return InitFormModel.Create(plan, shellProvider: null);
    }

    private static MigrationWorkflow.MigrationSelection CreateMigration(string channel, string version)
    {
        return new MigrationWorkflow.MigrationSelection(
            InstallComponent.SDK,
            new UpdateChannel(channel),
            new ReleaseVersion(version),
            InstallerUtilities.GetDefaultInstallArchitecture());
    }
}
