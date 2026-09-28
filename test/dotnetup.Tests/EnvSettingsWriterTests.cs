// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for the pure message-selection logic in <see cref="EnvSettingsWriter"/>, which
/// decides how to tell the user to make an applied env change effective.
/// </summary>
[TestClass]
public class EnvSettingsWriterTests
{
    private static EnvTerminalState Active => new(NeedsAdditions: false, NeedsRemovals: false);
    private static EnvTerminalState NeedsAdditions => new(NeedsAdditions: true, NeedsRemovals: false);

    [TestMethod]
    public void BuildEffectMessage_ActiveAndUnchanged_ReturnsNull()
    {
        EnvSettingsWriter.BuildEffectMessage(Active, changedPersistedEnvironment: false, activationCommand: null)
            .Should().BeNull();
    }

    [TestMethod]
    public void BuildEffectMessage_ActiveButChanged_RemindsAboutOtherSurfaces()
    {
        EnvSettingsWriter.BuildEffectMessage(Active, changedPersistedEnvironment: true, activationCommand: null)
            .Should().Be(Strings.EnvOpenNewTerminalForOtherSurfaces);
    }

    [TestMethod]
    public void BuildEffectMessage_NotActiveWithActivationCommand_OffersTheCommand()
    {
        string expected = string.Format(CultureInfo.InvariantCulture, Strings.EnvApplyToTerminalPrompt, "the-activation-command");

        EnvSettingsWriter.BuildEffectMessage(NeedsAdditions, changedPersistedEnvironment: true, activationCommand: "the-activation-command")
            .Should().Be(expected);
    }

    [TestMethod]
    public void BuildEffectMessage_NotActiveWithoutActivationCommand_TellsToOpenNewTerminal()
    {
        EnvSettingsWriter.BuildEffectMessage(NeedsAdditions, changedPersistedEnvironment: true, activationCommand: null)
            .Should().Be(Strings.EnvOpenNewTerminalToTakeEffect);
    }
}
