// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for the pure <see cref="EnvActivationStatus.Evaluate"/> logic, which compares the
/// configured access against the directories <c>dotnet</c> / <c>dotnetup</c> currently resolve to
/// and reports whether the current terminal needs additions, removals, or is already active. The
/// live-process wrapper (which calls GetCommandPath) is not exercised here.
/// </summary>
[TestClass]
public class EnvActivationStatusTests
{
    private const string DotnetDir = "/managed/dotnet";
    private const string DotnetupDir = "/managed/dotnetup";

    private static EnvTerminalState Evaluate(
        DotnetAccessMode accessMode,
        bool dotnetupOnPath,
        string? resolvedDotnetDir,
        string? resolvedDotnetupDir)
        => EnvActivationStatus.Evaluate(
            new DotnetupConfigData { AccessMode = accessMode, DotnetupOnPath = dotnetupOnPath },
            DotnetDir,
            DotnetupDir,
            resolvedDotnetDir,
            resolvedDotnetupDir);

    [TestMethod]
    [DataRow(DotnetAccessMode.Shell, true, DotnetDir, DotnetupDir, false, false)]
    [DataRow(DotnetAccessMode.Shell, true, null, null, true, false)]
    // A system dotnet wins resolution, not the managed one → still needs the managed dotnet added.
    [DataRow(DotnetAccessMode.Shell, true, "/usr/bin", DotnetupDir, true, false)]
    [DataRow(DotnetAccessMode.None, true, null, DotnetupDir, false, false)]
    // 'none' means the managed dotnet should NOT win; a stale managed dotnet means a removal (only a new terminal can drop it).
    [DataRow(DotnetAccessMode.None, true, DotnetDir, DotnetupDir, false, true)]
    [DataRow(DotnetAccessMode.None, false, null, null, false, false)]
    [DataRow(DotnetAccessMode.None, false, null, DotnetupDir, false, true)]
    [DataRow(DotnetAccessMode.Everywhere, true, DotnetDir, DotnetupDir, false, false)]
    // dotnet should be present but isn't (addition); dotnetup should be absent but resolves (removal).
    [DataRow(DotnetAccessMode.Shell, false, null, DotnetupDir, true, true)]
    internal void Evaluate_ReportsExpectedTerminalState(
        DotnetAccessMode accessMode,
        bool dotnetupOnPath,
        string? resolvedDotnetDir,
        string? resolvedDotnetupDir,
        bool expectedNeedsAdditions,
        bool expectedNeedsRemovals)
    {
        var state = Evaluate(accessMode, dotnetupOnPath, resolvedDotnetDir, resolvedDotnetupDir);

        state.NeedsAdditions.Should().Be(expectedNeedsAdditions);
        state.NeedsRemovals.Should().Be(expectedNeedsRemovals);
        state.IsActive.Should().Be(!expectedNeedsAdditions && !expectedNeedsRemovals);
    }
}
