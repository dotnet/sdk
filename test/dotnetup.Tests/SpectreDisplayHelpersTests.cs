// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SpectreDisplayHelpersTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key)
        => new('\0', key, shift: false, alt: false, control: false);

    // ── MapPlainScrollKey ──────────────────────────────────────────────

    [TestMethod]
    public void PlainScroll_UpArrow_ScrollsUp()
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(ConsoleKey.UpArrow)).Should().Be(ScrollAction.ScrollUp);

    [TestMethod]
    public void PlainScroll_DownArrow_ScrollsDown()
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(ConsoleKey.DownArrow)).Should().Be(ScrollAction.ScrollDown);

    [TestMethod]
    public void PlainScroll_Enter_Accepts()
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(ConsoleKey.Enter)).Should().Be(ScrollAction.Accept);

    [TestMethod]
    [DataRow(ConsoleKey.Y)]
    [DataRow(ConsoleKey.N)]
    [DataRow(ConsoleKey.P)]
    [DataRow(ConsoleKey.A)]
    public void PlainScroll_LetterKeys_AreIgnored(ConsoleKey key)
        => SpectreDisplayHelpers.MapPlainScrollKey(Key(key)).Should().Be(ScrollAction.None);

    // ── MapConfirmScrollKey ────────────────────────────────────────────

    [TestMethod]
    public void ConfirmScroll_UpArrow_ScrollsUp()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.UpArrow)).Should().Be(ScrollAction.ScrollUp);

    [TestMethod]
    public void ConfirmScroll_DownArrow_ScrollsDown()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.DownArrow)).Should().Be(ScrollAction.ScrollDown);

    [TestMethod]
    public void ConfirmScroll_Enter_Accepts()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.Enter)).Should().Be(ScrollAction.Accept);

    [TestMethod]
    public void ConfirmScroll_Y_Accepts()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.Y)).Should().Be(ScrollAction.Accept);

    [TestMethod]
    public void ConfirmScroll_N_Declines()
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(ConsoleKey.N)).Should().Be(ScrollAction.Decline);

    [TestMethod]
    [DataRow(ConsoleKey.A)]
    [DataRow(ConsoleKey.P)]
    [DataRow(ConsoleKey.Spacebar)]
    [DataRow(ConsoleKey.Escape)]
    public void ConfirmScroll_UnrecognizedKeys_AreIgnored(ConsoleKey key)
        => SpectreDisplayHelpers.MapConfirmScrollKey(Key(key)).Should().Be(ScrollAction.None);
}
