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
using Spectre.Console;
using Spectre.Console.Rendering;

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
        int environmentLine = Array.FindIndex(lines, line => line.Contains("Environment setup", StringComparison.Ordinal));

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

    [TestMethod]
    public void BrowseForm_HidesWelcomeBeforeInstallLocationWhenMoreConstrained()
    {
        const int height = 7;
        string output = RenderForm(width: 120, height, expandChannel: false, out int renderedHeight);

        output.Should().NotContain("Welcome to dotnetup");
        output.Should().Contain("dotnetup will install");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void BrowseForm_HidesInstallLocationAsFinalHeaderFallback()
    {
        const int height = 6;
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
        output.Should().Contain("Environment setup");
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
        output.Should().Contain("Environment setup");
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
        output.Should().NotContain("Environment setup");
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
    public void ExpandedEnvironment_ShowsChoiceHelpInlineWhenConstrained()
    {
        const int height = 14;
        InitFormModel model = CreateExpandedEnvironment(out InitFormState state);

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);
        string[] lines = Lines(output);

        lines.Should().Contain(line =>
            line.Contains("Everywhere", StringComparison.Ordinal)
            && line.Contains("Modify the system PATH", StringComparison.Ordinal));
        output.Should().Contain("Sets DOTNET_ROOT");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedEnvironment_HidesDerivedChangesWhenMoreConstrained()
    {
        const int height = 9;
        InitFormModel model = CreateExpandedEnvironment(out InitFormState state);

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);

        output.Should().Contain("Modify the system PATH");
        output.Should().NotContain("Adds dotnetup's .NET to the system PATH");
        output.Should().NotContain("Sets DOTNET_ROOT");
        output.Should().NotContain("Microsoft.PowerShell_profile.ps1");
        renderedHeight.Should().BeLessThanOrEqualTo(height);
    }

    [TestMethod]
    public void ExpandedEnvironment_HidesFieldDescriptionWhenMoreConstrained()
    {
        const int height = 8;
        InitFormModel model = CreateExpandedEnvironment(out InitFormState state);

        string output = RenderForm(model, state, width: 80, height, out int renderedHeight);

        output.Should().NotContain("Controls where");
        output.Should().Contain("Modify the system PATH");
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

    private static InitFormModel CreateMigrateModel()
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var migration = new MigrationWorkflow.MigrationSelection(
            InstallComponent.SDK,
            new UpdateChannel("8.0"),
            new ReleaseVersion("8.0.100"),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var plan = new WalkthroughPlan(
            installRoot,
            DotnetAccessMode.None,
            Migrations: [migration],
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, ChannelVersionResolver.LatestChannel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        return InitFormModel.Create(plan, shellProvider: null);
    }

    private static InitFormModel CreateExpandedEnvironment(out InitFormState state)
    {
        var installRoot = new DotnetInstallRoot(
            Path.GetTempPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var plan = new WalkthroughPlan(
            installRoot,
            DotnetAccessMode.Everywhere,
            Migrations: [],
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, ChannelVersionResolver.LatestChannel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        var shellProvider = new TestShellProvider(
            Path.GetTempPath(),
            "Microsoft.PowerShell_profile.ps1");
        InitFormModel model = InitFormModel.Create(plan, shellProvider);
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
        var plan = new WalkthroughPlan(
            installRoot,
            DotnetAccessMode.None,
            Migrations: [],
            new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, GlobalJsonPath: null),
            [new MinimalInstallSpec(InstallComponent.SDK, ChannelVersionResolver.LatestChannel)],
            ShellProvider: null,
            InstallRootGlobalJsonPath: null);
        return InitFormModel.Create(plan, shellProvider: null);
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
