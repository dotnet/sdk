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
    public void SelectedChannel_MapsFixedAndCustomChoices()
    {
        var model = CreateModel(new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null));
        FormField channelField = model.Fields[0];

        int ltsIndex = channelField.Choices
            .Select((choice, index) => (choice, index))
            .Single(item => item.choice.Title == ChannelVersionResolver.LtsChannel)
            .index;
        channelField.SelectChoice(ltsIndex);
        model.SelectedChannel().Should().Be(ChannelVersionResolver.LtsChannel);

        int customIndex = channelField.Choices.Count - 1;
        channelField.SetCustomValue(customIndex, "9.0.2xx");
        model.SelectedChannel().Should().Be("9.0.2xx");
    }

    [TestMethod]
    [DataRow("latest", false)]
    [DataRow("LATEST", false)]
    [DataRow("10.0", true)]
    internal void CustomChannel_IsComparedWithDefaultByValue(string customChannel, bool expectedToDiffer)
    {
        var channelDisplay = new DefaultChannelDisplay(
            ChannelVersionResolver.LatestChannel,
            GlobalJsonPath: null);
        InitFormDefaults defaults = CreateDefaults(channelDisplay);
        InitFormModel model = InitFormModel.Create(defaults, shellProvider: null);
        FormField channelField = model.Fields[0];
        channelField.SetCustomValue(channelField.Choices.Count - 1, customChannel);

        InitWorkflows.SelectedChannelDiffersFromDefault(
            model.SelectedChannel(),
            defaults.ChannelDisplay.ChannelLabel)
            .Should().Be(expectedToDiffer);
    }

    [TestMethod]
    public void GlobalJsonChannelDetail_ShowsSourcePath()
    {
        const string globalJsonPath = @"C:\repo\global.json";
        var model = CreateModel(new DefaultChannelDisplay("9.0", globalJsonPath));

        IReadOnlyList<DetailLine> lines = model.BuildDerivedDetailLines(model.Fields[0], choiceIndex: 0);

        lines.Should().ContainSingle()
            .Which.Should().Be(new DetailLine("From global.json:", globalJsonPath));
    }

    [TestMethod]
    public void ConfiguredAccessMode_IsSelectedByDefault()
    {
        var model = CreateModel(
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            accessMode: DotnetAccessMode.Shell);
        FormField accessModeField = model.Fields.Single(field => field.Label == "Access mode");

        accessModeField.DisplayValue.Should().Be(DotnetAccessMode.Shell.ToString());
        accessModeField.IsChangedFromDefault.Should().BeFalse();
        model.SelectedAccessMode().Should().Be(DotnetAccessMode.Shell);
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

        model.BuildDerivedDetailLines(migrationField, choiceIndex: 0)
            .Should().ContainSingle().Which.Value.Should().Be("9.0.300");

        FormField channelField = model.Fields[0];
        int customIndex = channelField.Choices.Count - 1;
        channelField.SetCustomValue(customIndex, "9.0.3xx");

        model.BuildDerivedDetailLines(migrationField, choiceIndex: 0)
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
        model.MigrateSelected().Should().BeFalse();

        FormField channelField = model.Fields[0];
        int customIndex = channelField.Choices.Count - 1;
        channelField.SetCustomValue(customIndex, "9.0.3xx");

        migrationField.IsVisible.Should().BeTrue();
        model.MigrateSelected().Should().BeTrue();
    }

    [TestMethod]
    public void TypingDefaultChannel_KeepsRedundantMigrationFieldHidden()
    {
        var model = CreateModel(
            new DefaultChannelDisplay("10.0.1xx", GlobalJsonPath: null),
            [CreateMigration("10.0.1xx", "10.0.100")]);
        FormField migrationField = model.Fields.Single(field => field.Label == "Migrate system installs");
        FormField channelField = model.Fields[0];
        channelField.SetCustomValue(channelField.Choices.Count - 1, "10.0.1XX");

        migrationField.IsVisible.Should().BeFalse();
        model.MigrateSelected().Should().BeFalse();
    }

    [TestMethod]
    public void MigrationField_IsOmittedWhenThereAreNoCandidates()
    {
        var model = CreateModel(new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null));

        model.Fields.Should().NotContain(field => field.Label == "Migrate system installs");
        model.MigrateSelected().Should().BeFalse();
    }

    [TestMethod]
    public void MigrationNoChoice_DisablesMigration()
    {
        var model = CreateModel(
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [CreateMigration("10.0.1xx", "10.0.100")]);
        FormField migrationField = model.Fields.Single(field => field.Label == "Migrate system installs");

        migrationField.SelectChoice(1);

        model.MigrateSelected().Should().BeFalse();
        model.BuildDerivedDetailLines(migrationField, choiceIndex: 1).Should().BeEmpty();
    }

    [TestMethod]
    public void MigrationDetail_GroupsComponentsAndTruncatesVersions()
    {
        var model = CreateModel(
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [
                CreateMigration("10.0.1xx", "10.0.100"),
                CreateMigration("9.0.3xx", "9.0.300"),
                CreateMigration("8.0.4xx", "8.0.400"),
                CreateMigration("7.0.4xx", "7.0.400"),
                CreateMigration("10.0", "10.0.5", InstallComponent.Runtime),
            ]);
        FormField migrationField = model.Fields.Single(field => field.Label == "Migrate system installs");

        IReadOnlyList<DetailLine> lines = model.BuildDerivedDetailLines(migrationField, choiceIndex: 0);

        lines.Should().HaveCount(2);
        lines[0].Label.Should().Be(".NET SDKs:");
        lines[0].Value.Should().EndWith("and 1 more");
        lines[1].Label.Should().ContainEquivalentOf("runtime");
        lines[1].Value.Should().Be("10.0.5");
    }

    private static InitFormModel CreateModel(
        DefaultChannelDisplay channelDisplay,
        List<MigrationWorkflow.MigrationSelection>? migrations = null,
        DotnetAccessMode accessMode = DotnetAccessMode.None)
        => InitFormModel.Create(CreateDefaults(channelDisplay, migrations, accessMode), shellProvider: null);

    private static InitFormDefaults CreateDefaults(
        DefaultChannelDisplay channelDisplay,
        List<MigrationWorkflow.MigrationSelection>? migrations = null,
        DotnetAccessMode accessMode = DotnetAccessMode.None)
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        return new InitFormDefaults(
            installRoot,
            accessMode,
            migrations ?? [],
            channelDisplay,
            [new MinimalInstallSpec(InstallComponent.SDK, channelDisplay.ChannelLabel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
    }

    private static MigrationWorkflow.MigrationSelection CreateMigration(
        string channel,
        string version,
        InstallComponent component = InstallComponent.SDK)
    {
        return new MigrationWorkflow.MigrationSelection(
            component,
            new UpdateChannel(channel),
            new ReleaseVersion(version),
            InstallerUtilities.GetDefaultInstallArchitecture());
    }
}
