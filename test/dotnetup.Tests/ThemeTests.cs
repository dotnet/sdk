// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ThemeTests
{
    [Fact]
    public void DefaultTheme_HasExpectedValues()
    {
        var theme = DotnetupTheme.Current;

        theme.Success.Should().Be("green");
        theme.Error.Should().Be("red");
        theme.Warning.Should().Be("yellow");
        theme.Accent.Should().Be("#9780E5");
        theme.Brand.Should().Be("#9780E5");
        theme.Dim.Should().Be("dim");
    }

    [Fact]
    public void MarkupHelpers_WrapTextCorrectly()
    {
        DotnetupTheme.Success("ok").Should().Be("[green]ok[/]");
        DotnetupTheme.Error("fail").Should().Be("[red]fail[/]");
        DotnetupTheme.Warning("warn").Should().Be("[yellow]warn[/]");
        DotnetupTheme.Accent("v10").Should().Be("[#9780E5]v10[/]");
        DotnetupTheme.Brand("dotnet").Should().Be("[#9780E5]dotnet[/]");
        DotnetupTheme.Dim("note").Should().Be("[dim]note[/]");
    }
}
