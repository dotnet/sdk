// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for <see cref="EnvScriptSelectionResolver"/> — how <c>env script</c> chooses what to
/// wire from the selection flags and the stored config (the no-flag, config-driven default plus the
/// no-config fallback to both).
/// </summary>
[TestClass]
public class EnvScriptSelectionResolverTests
{
    private static EnvScriptSelection Resolve(bool dotnet = false, bool dotnetup = false, bool dotnetupOnly = false, DotnetupConfigData? config = null)
        => EnvScriptSelectionResolver.Resolve(dotnet, dotnetup, dotnetupOnly, config);

    private static DotnetupConfigData Config(DotnetAccessMode accessMode, bool dotnetupOnPath)
        => new() { AccessMode = accessMode, DotnetupOnPath = dotnetupOnPath };

    [TestMethod]
    public void NoFlags_NoConfig_WiresBoth()
    {
        var result = Resolve();
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: true, IncludeDotnetup: true));
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.Shell, true, true, true)]
    [DataRow(DotnetAccessMode.Everywhere, true, true, true)]
    [DataRow(DotnetAccessMode.None, true, false, true)]
    [DataRow(DotnetAccessMode.Shell, false, true, false)]
    [DataRow(DotnetAccessMode.None, false, false, false)]
    internal void NoFlags_WithConfig_FollowsConfig(DotnetAccessMode accessMode, bool dotnetupOnPath, bool expectedDotnet, bool expectedDotnetup)
    {
        var result = Resolve(config: Config(accessMode, dotnetupOnPath));
        result.Should().Be(new EnvScriptSelection(expectedDotnet, expectedDotnetup));
    }

    [TestMethod]
    public void ExplicitFlags_WinOverConfig()
    {
        // Config says none/off, but an explicit --dotnet should still wire dotnet only.
        var result = Resolve(dotnet: true, config: Config(DotnetAccessMode.None, dotnetupOnPath: false));
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: true, IncludeDotnetup: false));
    }

    [TestMethod]
    public void ExplicitDotnetup_WiresDotnetupOnly()
    {
        var result = Resolve(dotnetup: true, config: Config(DotnetAccessMode.Everywhere, dotnetupOnPath: true));
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true));
    }

    [TestMethod]
    public void DotnetupOnly_LegacyAlias_WiresDotnetupOnly()
    {
        var result = Resolve(dotnetupOnly: true, config: Config(DotnetAccessMode.Shell, dotnetupOnPath: true));
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true));
    }

    [TestMethod]
    public void DotnetupOnly_CombinedWithNewFlags_Throws()
    {
        Action act = () => Resolve(dotnet: true, dotnetupOnly: true);
        act.Should().Throw<DotnetInstallException>();
    }

    [TestMethod]
    public void AdminMode_NoConfig_WiresOnlyDotnetup()
    {
        var result = EnvScriptSelectionResolver.Resolve(false, false, false, config: null, isAdminMode: true);

        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true));
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.Shell, true)]
    [DataRow(DotnetAccessMode.Everywhere, false)]
    [DataRow(DotnetAccessMode.None, true)]
    internal void AdminMode_PreservesDotnetupSettingWithoutActivatingDotnet(DotnetAccessMode mode, bool dotnetupOnPath)
    {
        var result = EnvScriptSelectionResolver.Resolve(false, false, false, Config(mode, dotnetupOnPath), isAdminMode: true);

        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: dotnetupOnPath));
    }

    [TestMethod]
    [DataRow(true, false, false, false)]
    [DataRow(true, true, false, true)]
    [DataRow(false, true, false, true)]
    [DataRow(false, false, true, true)]
    public void AdminMode_ProfileFlagsNeverActivateDotnet(bool dotnet, bool dotnetup, bool dotnetupOnly, bool expectedDotnetup)
    {
        var result = EnvScriptSelectionResolver.Resolve(dotnet, dotnetup, dotnetupOnly, config: null, isAdminMode: true);

        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: expectedDotnetup));
    }

    [TestMethod]
    public void AdminMode_StillRejectsConflictingFlags()
    {
        Action act = () => EnvScriptSelectionResolver.Resolve(true, false, true, config: null, isAdminMode: true);

        act.Should().Throw<DotnetInstallException>();
    }

    [TestMethod]
    public void LeavingAdminMode_ResumesPreservedConfiguration()
    {
        var config = Config(DotnetAccessMode.Shell, dotnetupOnPath: true);

        EnvScriptSelectionResolver.Resolve(false, false, false, config, isAdminMode: true).IncludeDotnet.Should().BeFalse();
        EnvScriptSelectionResolver.Resolve(false, false, false, config, isAdminMode: false)
            .Should().Be(new EnvScriptSelection(IncludeDotnet: true, IncludeDotnetup: true));
        config.AccessMode.Should().Be(DotnetAccessMode.Shell);
    }

    [TestMethod]
    [DataRow("bash")]
    [DataRow("zsh")]
    [DataRow("fish")]
    [DataRow("pwsh")]
    public void AdminMode_ScriptDoesNotEmitManagedDotnetPathOrDotnetRoot(string shell)
    {
        var parseResult = Parser.Parse(["env", "script", "--shell", shell, "--dotnet", "--dotnetup"]);
        parseResult.Errors.Should().BeEmpty();
        IEnvShellProvider provider = parseResult.GetValue(EnvScriptCommandParser.ShellOption)!;
        var selection = EnvScriptSelectionResolver.Resolve(true, true, false, config: null, isAdminMode: true);
        const string dotnetPath = "managed-dotnet-must-not-be-activated";
        const string dotnetupPath = "dotnetup-tool-directory";

        string script = provider.GenerateEnvScript(dotnetPath, selection.IncludeDotnetup ? dotnetupPath : "", selection.IncludeDotnet);

        script.Should().Contain(dotnetupPath).And.NotContain(dotnetPath).And.NotContain("DOTNET_ROOT");
    }
}
