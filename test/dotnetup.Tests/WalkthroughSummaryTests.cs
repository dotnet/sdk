// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class WalkthroughSummaryTests
{
    [Fact]
    internal void BuildSummaryChoices_OrdersProceedCustomizeExit()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: false);

        choices.Should().HaveCount(3);
        choices[0].Decision.Should().Be(WalkthroughDecision.Proceed);
        choices[1].Decision.Should().Be(WalkthroughDecision.Customize);
        choices[2].Decision.Should().Be(WalkthroughDecision.Exit);
    }

    [Fact]
    internal void BuildSummaryChoices_Unconfigured_FirstChoiceProceeds()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: false);

        choices[0].Option.Title.Should().Contain("proceed");
    }

    [Fact]
    internal void BuildSummaryChoices_Configured_FirstChoiceOffersOverride()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: true);

        choices[0].Option.Title.Should().Contain("override");
    }

    [Fact]
    internal void GetDefaultChoiceIndex_Unconfigured_DefaultsToProceed()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: false);

        int index = WalkthroughSummary.GetDefaultChoiceIndex(choices, isConfigured: false);

        choices[index].Decision.Should().Be(WalkthroughDecision.Proceed);
    }

    [Fact]
    internal void GetDefaultChoiceIndex_Configured_DefaultsToCustomize()
    {
        var choices = WalkthroughSummary.BuildSummaryChoices(isConfigured: true);

        int index = WalkthroughSummary.GetDefaultChoiceIndex(choices, isConfigured: true);

        choices[index].Decision.Should().Be(WalkthroughDecision.Customize);
    }
}
