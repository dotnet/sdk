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
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Spectre.Console;
using Spectre.Console.Rendering;
using Strings = Microsoft.DotNet.Tools.Bootstrapper.Strings;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InitFormRendererTests
{
    [TestMethod]
    public void ExpandedChannel_ShowsAllChoicesWhenTheyFit()
    {
        string output = RenderForm(width: 120, height: 100, expandChannel: true);

        output.Should().Contain("<other>");
        output.Should().NotContain("more below");
    }

    [TestMethod]
    public void ExpandedChannel_ShowsAllChoicesInShortTerminal()
    {
        string output = RenderForm(width: 50, height: 24, expandChannel: true);

        output.Should().Contain("<other>");
        output.Should().NotContain("more below");
        output.Should().NotContain("more above");
    }

    [TestMethod]
    public void ExpandedCustomChoice_ShowsEditorInlineWhenHelpIsNotInline()
    {
        InitFormModel model = CreateDefaultModel();
        var field = new FormField(
            "Custom field",
            [new FieldChoice("<other>", "Enter another value.", IsCustomInput: true)],
            defaultIndex: 0,
            inlineHelp: false);
        var state = new InitFormState([field]);
        state.MoveUp();
        state.Enter();
        foreach (char character in "preview-channel")
        {
            state.AppendChar(character);
        }

        string output = RenderForm(model, state, width: 120, height: 100, out _);
        string[] lines = Lines(output);

        int choiceLine = Array.FindIndex(lines, line => line.Contains("<other>", StringComparison.Ordinal));
        lines[choiceLine].Should().Contain("preview-channel");
        lines[choiceLine + 1].Should().Contain("Enter another value.");
    }

    [TestMethod]
    public void ExpandedCustomChoice_ShowsRememberedValueWhenHelpIsNotInline()
    {
        InitFormModel model = CreateDefaultModel();
        var field = new FormField(
            "Custom field",
            [
                new FieldChoice("default", "Use the default value."),
                new FieldChoice("<other>", "Enter another value.", IsCustomInput: true),
            ],
            defaultIndex: 0,
            inlineHelp: false);
        field.RememberCustomText("preview-channel");
        var state = new InitFormState([field]);
        state.MoveUp();
        state.Enter();

        string output = RenderForm(model, state, width: 120, height: 100, out _);
        string[] lines = Lines(output);

        int customChoiceLine = Array.FindIndex(lines, line => line.Contains("<other>", StringComparison.Ordinal));
        lines[customChoiceLine].Should().Contain("preview-channel");
        lines[customChoiceLine + 1].Should().Contain("Enter another value.");
    }

    [TestMethod]
    public void BrowseForm_RemovesEmptyLinesBetweenFieldsWhenConstrained()
    {
        const int height = 10;
        string output = RenderForm(width: 120, height, expandChannel: false, out int renderedHeight);
        string[] lines = Lines(output);

        int channelLine = Array.FindIndex(lines, line => line.Contains("SDK Channel", StringComparison.Ordinal));
        int environmentLine = Array.FindIndex(lines, line => line.Contains("Access mode", StringComparison.Ordinal));

        environmentLine.Should().Be(channelLine + 1);
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_RemovesBlankLineBetweenHeaderMessagesWhenConstrained()
    {
        string output = RenderForm(width: 120, height: 10, expandChannel: false);
        string[] lines = Lines(output);

        int welcomeLine = Array.FindIndex(lines, line => line.Contains("Welcome to dotnetup", StringComparison.Ordinal));
        int installLine = Array.FindIndex(lines, line => line.Contains("dotnetup will install", StringComparison.Ordinal));

        installLine.Should().Be(welcomeLine + 1);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void AdminSelection_KeepsInstallFormAndShowsSystemInstallLocation()
    {
        InitFormModel model = CreateDefaultModel();
        string userPath = model.InstallPath;
        string adminPath = new AdminInstallRootResolver()
            .Resolve(InstallerUtilities.GetDefaultInstallArchitecture()).Path;
        var state = new InitFormState(model.Fields);
        FormField accessModeField = model.Fields.Single(field => field.Label == "Access mode");
        int userIndex = accessModeField.SelectedIndex;

        accessModeField.SelectChoice(IndexOf(accessModeField, DotnetAccessMode.Admin));
        string output = RenderForm(model, state, width: 240, height: 100, out _);

        output.Should().Contain("SDK Channel");
        output.Should().Contain("Install .NET with these settings?");
        output.Should().Contain("Accept and install");
        output.Should().Contain(adminPath);
        output.Should().NotContain(userPath);

        accessModeField.SelectChoice(userIndex);
        output = RenderForm(model, state, width: 240, height: 100, out _);

        output.Should().Contain(userPath);
        output.Should().NotContain(adminPath);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(120, 40)]
    [DataRow(120, 24)]
    [DataRow(80, 24)]
    public void ExpandedEverywhere_PreservesHeaderAndSectionSpacing(int width, int height)
    {
        InitFormModel model = CreateMigrateModel(DotnetAccessMode.Everywhere, migrateByDefault: true);
        FormField accessMode = model.Fields.Single(field => field.Label == "Access mode");
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.MoveUp();
        state.Enter();
        state.FocusedField.Should().BeSameAs(accessMode);

        string output = RenderForm(model, state, width, height, out int renderedHeight);
        string[] lines = Lines(output);
        int welcomeLine = Array.FindIndex(lines, line => line.Contains("Welcome to dotnetup", StringComparison.Ordinal));
        int locationLine = Array.FindIndex(lines, line => line.Contains("dotnetup will install", StringComparison.Ordinal));
        int channelLine = Array.FindIndex(lines, line => line.Contains("SDK Channel", StringComparison.Ordinal));
        int modeLine = Array.FindIndex(lines, line => line.Contains("Access mode", StringComparison.Ordinal));
        int migrationLine = Array.FindIndex(lines, line => line.Contains("Migrate system installs", StringComparison.Ordinal));
        int confirmationLine = Array.FindIndex(lines, line => line.Contains("Install .NET with these settings?", StringComparison.Ordinal));

        welcomeLine.Should().BeGreaterThanOrEqualTo(0);
        locationLine.Should().BeGreaterThan(welcomeLine + 1);
        lines[welcomeLine + 1].Should().BeNullOrWhiteSpace();
        channelLine.Should().BeGreaterThan(locationLine);
        lines[channelLine - 1].Should().BeNullOrWhiteSpace();
        modeLine.Should().BeGreaterThan(channelLine);
        lines[modeLine - 1].Should().BeNullOrWhiteSpace();
        migrationLine.Should().BeGreaterThan(modeLine);
        lines[migrationLine - 1].Should().BeNullOrWhiteSpace();
        confirmationLine.Should().BeGreaterThan(migrationLine);
        lines[confirmationLine - 1].Should().BeNullOrWhiteSpace();
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(240, 100, 1)]
    [DataRow(120, 40, 1)]
    [DataRow(120, 40, 2)]
    [DataRow(80, 24, 2)]
    [DataRow(50, 16, 2)]
    public void HoveringAccessModes_KeepsCompressionAndRowsAboveTheHighlightInPlace(int width, int height, int profileCount)
    {
        InitFormModel model = CreateMigrateModel(
            DotnetAccessMode.Everywhere,
            migrateByDefault: true,
            new TestShellProvider(
                Path.GetTempPath(),
                [.. Enumerable.Range(0, profileCount).Select(index => $"profile{index}.ps1")]));
        FormField accessMode = model.Fields.Single(field => field.Label == "Access mode");
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.MoveUp();
        state.Enter();
        HighlightAccessMode(state, accessMode, DotnetAccessMode.None);
        string[] baselineLines = Lines(RenderForm(model, state, width, height, out _));
        int baselineBlankLines = baselineLines.Count(string.IsNullOrWhiteSpace);
        bool baselineHasChannelHelp = baselineLines.Any(line => line.Contains("Determines which version", StringComparison.Ordinal));
        string[] choiceTitles = [.. accessMode.Choices.Select(choice => choice.Title)];

        foreach (DotnetAccessMode mode in Enum.GetValues<DotnetAccessMode>())
        {
            HighlightAccessMode(state, accessMode, mode);
            string[] lines = Lines(RenderForm(model, state, width, height, out int renderedHeight));

            // Rows up to and including the highlighted choice never move; only its details push later rows down.
            string[] stableLabels = ["Access mode", .. choiceTitles.Take(Array.IndexOf(choiceTitles, mode.ToString()) + 1)];
            foreach (string label in stableLabels)
            {
                int baselineLine = Array.FindIndex(baselineLines, line => line.Contains($" {label}", StringComparison.Ordinal));
                int currentLine = Array.FindIndex(lines, line => line.Contains($" {label}", StringComparison.Ordinal));
                baselineLine.Should().BeGreaterThanOrEqualTo(0);
                currentLine.Should().Be(baselineLine, $"{label} should not move while highlighting {mode}");
            }

            // The compression level, and so the spacing and descriptions, does not depend on the highlight.
            lines.Count(string.IsNullOrWhiteSpace).Should().BeGreaterThanOrEqualTo(baselineBlankLines,
                $"blank lines should not be removed while highlighting {mode}");
            if (mode != DotnetAccessMode.Admin)
            {
                // Admin hides the SDK Channel rows, so its description is only comparable for other modes.
                lines.Any(line => line.Contains("Determines which version", StringComparison.Ordinal))
                    .Should().Be(baselineHasChannelHelp, $"the SDK Channel description should not toggle while highlighting {mode}");
            }

            renderedHeight.Should().BeLessThanOrEqualTo(height);
            model.SelectedAccessMode().Should().Be(DotnetAccessMode.Everywhere);
            model.MigrateSelected().Should().BeTrue();
        }
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(240, 100)]
    [DataRow(80, 24)]
    [DataRow(50, 30)]
    public void HoveringAdmin_KeepsCommittedInstallLocationOnTheSameRow(int width, int height)
    {
        InitFormModel model = CreateMigrateModel(DotnetAccessMode.Everywhere, migrateByDefault: true);
        FormField accessMode = model.Fields.Single(field => field.Label == "Access mode");
        string adminPath = new AdminInstallRootResolver().Resolve(InstallerUtilities.GetDefaultInstallArchitecture()).Path;
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.MoveUp();
        state.Enter();

        HighlightAccessMode(state, accessMode, DotnetAccessMode.None);
        string[] userLines = Lines(RenderForm(model, state, width, height, out _));
        HighlightAccessMode(state, accessMode, DotnetAccessMode.Admin);
        string[] adminLines = Lines(RenderForm(model, state, width, height, out _));

        int userRow = Array.FindIndex(userLines, line => line.Contains("dotnetup will install", StringComparison.Ordinal));
        int adminRow = Array.FindIndex(adminLines, line => line.Contains("dotnetup will install", StringComparison.Ordinal));
        userRow.Should().BeGreaterThanOrEqualTo(0);
        adminRow.Should().Be(userRow);
        adminLines[adminRow].Should().Be(userLines[userRow]);
        adminLines[adminRow].Should().NotContain(adminPath);
        Array.FindIndex(adminLines, line => line.Contains("Access mode", StringComparison.Ordinal)).Should()
            .Be(Array.FindIndex(userLines, line => line.Contains("Access mode", StringComparison.Ordinal)));
        model.SelectedAccessMode().Should().Be(DotnetAccessMode.Everywhere);
    }

    [TestMethod, OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void AccessModeChoices_ExcludeWindowsOnlyModesOnOtherPlatforms()
    {
        InitFormModel model = CreateDefaultModel();
        FormField accessMode = model.Fields.Single(field => field.Label == "Access mode");

        accessMode.Choices.Select(choice => choice.Title).Should().Equal(
            DotnetAccessMode.None.ToString(),
            DotnetAccessMode.Shell.ToString());
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(240, 100)]
    [DataRow(80, 24)]
    [DataRow(50, 16)]
    public void HighlightingAdmin_KeepsInstallFormWithoutChangingSelections(int width, int height)
    {
        InitFormModel model = CreateMigrateModel(DotnetAccessMode.Shell, migrateByDefault: true);
        FormField accessMode = model.Fields.Single(field => field.Label == "Access mode");
        FormField migration = model.Fields.Single(field => field.Label == "Migrate system installs");
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.MoveUp();
        state.FocusedField.Should().BeSameAs(accessMode);
        state.Enter();
        HighlightAccessMode(state, accessMode, DotnetAccessMode.Admin);

        string output = RenderForm(model, state, width, height, out int renderedHeight);

        renderedHeight.Should().BeLessThanOrEqualTo(height);
        if (width == 240)
        {
            output.Should().Contain("SDK Channel");
            output.Should().Contain("Install .NET with these settings?");
            output.Should().Contain(Strings.AccessModeAdminDescription);
            output.Should().Contain(Strings.AccessModeAdminDetails);
        }
        model.SelectedAccessMode().Should().Be(DotnetAccessMode.Shell);
        model.MigrateSelected().Should().BeTrue();
        migration.SelectedIndex.Should().Be(0);
        state.FocusedField.Should().BeSameAs(accessMode);
        state.IsDone.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void SelectedAdmin_HidesMigrationAndLeavingRestoresIt()
    {
        InitFormModel model = CreateMigrateModel(DotnetAccessMode.Shell, migrateByDefault: true);
        FormField accessMode = model.Fields.Single(field => field.Label == "Access mode");
        FormField migration = model.Fields.Single(field => field.Label == "Migrate system installs");
        var state = new InitFormState(model.Fields);
        int fieldCount = state.VisibleFields.Count;

        accessMode.SelectChoice(IndexOf(accessMode, DotnetAccessMode.Admin));
        string output = RenderForm(model, state, width: 240, height: 100, out _);

        state.VisibleFields.Should().HaveCount(fieldCount - 1);
        migration.IsVisible.Should().BeFalse();
        model.MigrateSelected().Should().BeFalse();
        output.Should().NotContain("Migrate system installs");
        output.Should().NotContain(Strings.InitMigrationNotSupportedInAdminMode);
        output.Should().Contain("Accept and install");

        accessMode.SelectChoice(IndexOf(accessMode, DotnetAccessMode.Shell));

        migration.IsVisible.Should().BeTrue();
        state.VisibleFields.Should().HaveCount(fieldCount);
        model.MigrateSelected().Should().BeTrue();
        RenderForm(model, state, width: 240, height: 100, out _)
            .Should().NotContain(Strings.InitMigrationNotSupportedInAdminMode);
    }


    [TestMethod]
    public void BrowseForm_HidesWelcomeBeforeInstallLocationWhenMoreConstrained()
    {
        const int height = 6;
        string output = RenderForm(width: 120, height, expandChannel: false, out int renderedHeight);

        output.Should().NotContain("Welcome to dotnetup");
        output.Should().Contain("dotnetup will install");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_HidesInstallLocationAsFinalHeaderFallback()
    {
        const int height = 5;
        string output = RenderForm(width: 120, height, expandChannel: false, out int renderedHeight);

        output.Should().NotContain("Welcome to dotnetup");
        output.Should().NotContain("dotnetup will install");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_HidesFocusedFieldDescriptionWhenMoreConstrained()
    {
        const int height = 6;
        InitFormModel model = CreateDefaultModel();
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.MoveUp();

        string output = RenderForm(model, state, width: 120, height, out int renderedHeight);

        output.Should().Contain("SDK Channel");
        output.Should().NotContain("Determines which version");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_KeepsAllFieldsWhenMigrateDescriptionDoesNotFit()
    {
        const int height = 6;
        InitFormModel model = CreateMigrateModel();
        var state = new InitFormState(model.Fields);
        state.MoveUp();

        string output = RenderForm(model, state, width: 120, height, out int renderedHeight);
        string[] lines = Lines(output);

        output.Should().Contain("SDK Channel");
        output.Should().Contain("Access mode");
        output.Should().Contain("Migrate system installs");
        output.Should().NotContain("Install the SDK and runtime versions");
        int acceptLine = Array.FindIndex(lines, line => line.Contains("Accept and install", StringComparison.Ordinal));
        int legendLine = Array.FindIndex(lines, line => line.Contains("Enter edit/accept", StringComparison.Ordinal));
        legendLine.Should().Be(acceptLine + 1);
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_HidesConfirmationPromptBeforeNavigationLegend()
    {
        const int height = 5;
        InitFormModel model = CreateMigrateModel();
        var state = new InitFormState(model.Fields);
        state.MoveUp();

        string output = RenderForm(model, state, width: 120, height, out int renderedHeight);

        output.Should().NotContain("Install .NET with these settings?");
        output.Should().Contain("Accept and install");
        output.Should().Contain("Enter edit/accept");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_HidesNavigationLegendAsFinalFallback()
    {
        const int height = 4;
        InitFormModel model = CreateMigrateModel();
        var state = new InitFormState(model.Fields);
        state.MoveUp();

        string output = RenderForm(model, state, width: 120, height, out int renderedHeight);

        output.Should().Contain("SDK Channel");
        output.Should().Contain("Access mode");
        output.Should().Contain("Migrate system installs");
        output.Should().Contain("Accept and install");
        output.Should().NotContain("Install .NET with these settings?");
        output.Should().NotContain("Enter edit/accept");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedChannel_HidesUnrelatedFieldsWhenConstrained()
    {
        const int height = 8;
        string output = RenderForm(width: 120, height, expandChannel: true, out int renderedHeight);

        output.Should().Contain("<other>");
        output.Should().NotContain("Access mode");
        output.Should().NotContain("Accept and install");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedChannel_UsesHorizontalChoicesWhenMoreConstrained()
    {
        const int height = 6;
        string output = RenderForm(width: 50, height, expandChannel: true, out int renderedHeight);
        string[] lines = Lines(output);

        output.Should().NotContain("Determines which version");
        output.Should().NotContain("(default)");
        lines.Should().Contain(line =>
            line.Contains("latest", StringComparison.Ordinal)
            && line.Contains("lts", StringComparison.Ordinal));
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedAccessMode_ShowsChoiceHelpInlineWhenConstrained()
    {
        // The required height depends on the OS (Windows adds Everywhere and Admin) and on how the temp
        // install path wraps, so use the tightest height that still shows the derived details. Spacing
        // is retained at that level, so only inline choice help can make it fit.
        const string derivedDetail = "Only applications launched from the shell";
        int height = Enumerable.Range(1, 100).First(candidate =>
        {
            InitFormModel candidateModel = CreateExpandedAccessMode(out InitFormState candidateState);
            return RenderForm(candidateModel, candidateState, width: 80, candidate, out _).Contains(derivedDetail, StringComparison.Ordinal);
        });
        InitFormModel model = CreateExpandedAccessMode(out InitFormState state);

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);
        string[] lines = Lines(output);

        lines.Should().Contain(line =>
            line.Contains("Shell", StringComparison.Ordinal)
            && line.Contains("Configure your shell profile", StringComparison.Ordinal));
        output.Should().Contain("Only applications launched from the shell");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedAccessMode_HidesDerivedChangesWhenMoreConstrained()
    {
        int height = OperatingSystem.IsWindows() ? 10 : 7;
        InitFormModel model = CreateExpandedAccessMode(out InitFormState state);

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);

        output.Should().Contain("Configure your shell profile");
        output.Should().NotContain("Only applications launched from the shell");
        output.Should().NotContain("Microsoft.PowerShell_profile.ps1");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedAccessMode_HidesFieldDescriptionWhenMoreConstrained()
    {
        int height = OperatingSystem.IsWindows() ? 10 : 6;
        InitFormModel model = CreateExpandedAccessMode(out InitFormState state);

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);

        output.Should().NotContain("Controls where");
        output.Should().Contain("Configure your shell profile");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedMigrate_ShowsChoiceHelpInlineWhenConstrained()
    {
        const int height = 6;
        InitFormModel model = CreateMigrateModel();
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.Enter();

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);
        string[] lines = Lines(output);

        lines.Should().Contain(line =>
            line.Contains("Yes", StringComparison.Ordinal)
            && line.Contains("Install the SDK and runtime versions", StringComparison.Ordinal));
        renderedHeight.Should().BeLessThanOrEqualTo(height);

        state.MoveDown();
        output = RenderForm(model, state, width: 80, height, out renderedHeight);
        lines = Lines(output);

        lines.Should().Contain(line =>
            line.Contains("No", StringComparison.Ordinal)
            && line.Contains("Don't install any additional", StringComparison.Ordinal));
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedMigrate_HidesNavigationLegendAsFinalFallback()
    {
        const int height = 3;
        InitFormModel model = CreateMigrateModel();
        var state = new InitFormState(model.Fields);
        state.MoveUp();
        state.Enter();

        string output = RenderForm(model, state, width: 120, height, out int renderedHeight);

        output.Should().Contain("Migrate system installs");
        output.Should().Contain("Install the SDK and runtime versions");
        output.Should().Contain("Don't install any additional");
        output.Should().NotContain("Enter select");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    private static InitFormModel CreateMigrateModel(
        DotnetAccessMode accessMode = DotnetAccessMode.None,
        bool migrateByDefault = false,
        IEnvShellProvider? shellProvider = null)
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var migration = new MigrationWorkflow.MigrationSelection(
            InstallComponent.SDK,
            new UpdateChannel("8.0"),
            new ReleaseVersion("8.0.100"),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var defaults = new InitFormDefaults(
            installRoot,
            accessMode,
            MigrateSystemInstalls: migrateByDefault,
            Migrations: [migration],
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, ChannelVersionResolver.LatestChannel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        return InitFormModel.Create(defaults, shellProvider);
    }

    private static int IndexOf(FormField field, DotnetAccessMode mode) =>
        field.Choices.Select((choice, choiceIndex) => (choice, choiceIndex))
            .Single(item => item.choice.Title == mode.ToString()).choiceIndex;

    private static void HighlightAccessMode(InitFormState state, FormField field, DotnetAccessMode mode)
    {
        int index = field.Choices.Select((choice, choiceIndex) => (choice, choiceIndex))
            .Single(item => item.choice.Title == mode.ToString()).choiceIndex;
        while (state.EditChoiceIndex < index)
        {
            state.MoveDown();
        }
        while (state.EditChoiceIndex > index)
        {
            state.MoveUp();
        }
    }

    private static InitFormModel CreateExpandedAccessMode(out InitFormState state)
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var defaults = new InitFormDefaults(
            installRoot,
            DotnetAccessMode.Shell,
            MigrateSystemInstalls: true,
            Migrations: [],
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, ChannelVersionResolver.LatestChannel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        var shellProvider = new TestShellProvider(
            Path.GetPathRoot(Path.GetTempPath())!,
            "Microsoft.PowerShell_profile.ps1");
        InitFormModel model = InitFormModel.Create(defaults, shellProvider);
        state = new InitFormState(model.Fields);
        state.MoveUp();
        state.Enter();

        return model;
    }

    private static string RenderForm(int width, int height, bool expandChannel)
        => RenderForm(width, height, expandChannel, out _);

    private static string RenderForm(int width, int height, bool expandChannel, out int renderedHeight)
    {
        InitFormModel model = CreateDefaultModel();
        var state = new InitFormState(model.Fields);
        if (expandChannel)
        {
            for (int index = state.AcceptRow; index > 0; index--)
            {
                state.MoveUp();
            }
            state.Enter();
        }

        return RenderForm(model, state, width, height, out renderedHeight);
    }

    private static InitFormModel CreateDefaultModel()
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var defaults = new InitFormDefaults(
            installRoot,
            DotnetAccessMode.None,
            MigrateSystemInstalls: false,
            Migrations: [],
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, ChannelVersionResolver.LatestChannel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        return InitFormModel.Create(defaults, shellProvider: null);
    }

    private static string RenderForm(
        InitFormModel model,
        InitFormState state,
        int width,
        int height,
        out int renderedHeight)
    {
        var writer = new StringWriter();
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = AnsiSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        });
        console.Profile.Width = width;
        console.Profile.Height = height;
        var renderable = InitFormRenderer.BuildRenderable(model, state, showArrow: true, console);
        RenderOptions renderOptions = RenderOptions.Create(console, console.Profile.Capabilities);
        renderedHeight = InitFormRenderer.RenderedHeight(renderable, renderOptions, width);
        console.Write(renderable);
        return writer.ToString();
    }

    private static string[] Lines(string text) =>
        text.Split(["\r\n", "\n"], StringSplitOptions.None);
}
