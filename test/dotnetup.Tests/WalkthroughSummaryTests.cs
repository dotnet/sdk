// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class WalkthroughSummaryTests
{
    [Theory]
    [InlineData(0, WalkthroughDecision.Proceed)]
    [InlineData(1, WalkthroughDecision.Customize)]
    [InlineData(2, WalkthroughDecision.Exit)]
    internal void EvaluateSummaryDecision_MapsIndexToDecision(int index, WalkthroughDecision expected)
    {
        WalkthroughSummary.EvaluateSummaryDecision(index).Should().Be(expected);
    }

    [Fact]
    public void EvaluateSummaryDecision_TreatsUnknownIndexAsExit()
    {
        WalkthroughSummary.EvaluateSummaryDecision(99).Should().Be(WalkthroughDecision.Exit);
    }

    [Fact]
    public void BuildSummaryOptions_Unconfigured_DefaultsToProceed()
    {
        var (options, defaultIndex) = WalkthroughSummary.BuildSummaryOptions(isConfigured: false);

        defaultIndex.Should().Be(0);
        options.Should().HaveCount(3);
        options[0].Title.Should().Contain("proceed");
        options[1].Title.Should().Contain("customize");
        options[2].Title.Should().Contain("Exit");
    }

    [Fact]
    public void BuildSummaryOptions_Configured_DefaultsToCustomizeAndOffersOverride()
    {
        var (options, defaultIndex) = WalkthroughSummary.BuildSummaryOptions(isConfigured: true);

        defaultIndex.Should().Be(1);
        options.Should().HaveCount(3);
        options[0].Title.Should().Contain("override");
        options[1].Title.Should().Contain("customize");
        options[2].Title.Should().Contain("Exit");
    }
}
