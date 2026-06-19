// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class SpectreDisplayHelpersTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key)
        => new('\0', key, shift: false, alt: false, control: false);

    // ── MapPlainScrollKey ──────────────────────────────────────────────

    [Fact]
    public void PlainScroll_UpArrow_ScrollsUp()
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(ConsoleKey.UpArrow)).Should().Be(ScrollAction.ScrollUp);

    [Fact]
    public void PlainScroll_DownArrow_ScrollsDown()
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(ConsoleKey.DownArrow)).Should().Be(ScrollAction.ScrollDown);

    [Fact]
    public void PlainScroll_Enter_Accepts()
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(ConsoleKey.Enter)).Should().Be(ScrollAction.Accept);

    [Theory]
    [InlineData(ConsoleKey.Y)]
    [InlineData(ConsoleKey.N)]
    [InlineData(ConsoleKey.P)]
    [InlineData(ConsoleKey.A)]
    public void PlainScroll_LetterKeys_AreIgnored(ConsoleKey key)
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(key)).Should().Be(ScrollAction.None);

    // ── MapConfirmScrollKey ────────────────────────────────────────────

    [Fact]
    public void ConfirmScroll_UpArrow_ScrollsUp()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.UpArrow)).Should().Be(ScrollAction.ScrollUp);

    [Fact]
    public void ConfirmScroll_DownArrow_ScrollsDown()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.DownArrow)).Should().Be(ScrollAction.ScrollDown);

    [Fact]
    public void ConfirmScroll_Enter_Accepts()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.Enter)).Should().Be(ScrollAction.Accept);

    [Fact]
    public void ConfirmScroll_Y_Accepts()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.Y)).Should().Be(ScrollAction.Accept);

    [Fact]
    public void ConfirmScroll_N_Declines()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.N)).Should().Be(ScrollAction.Decline);

    [Theory]
    [InlineData(ConsoleKey.A)]
    [InlineData(ConsoleKey.P)]
    [InlineData(ConsoleKey.Spacebar)]
    [InlineData(ConsoleKey.Escape)]
    public void ConfirmScroll_UnrecognizedKeys_AreIgnored(ConsoleKey key)
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(key)).Should().Be(ScrollAction.None);
}
