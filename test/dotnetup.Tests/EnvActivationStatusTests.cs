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
    public void Shell_DotnetupOn_BothResolveToManaged_IsActive()
        => Evaluate(DotnetAccessMode.Shell, dotnetupOnPath: true, DotnetDir, DotnetupDir).IsActive.Should().BeTrue();

    [TestMethod]
    public void Shell_DotnetupOn_NeitherResolved_NeedsAdditionsOnly()
    {
        var state = Evaluate(DotnetAccessMode.Shell, dotnetupOnPath: true, resolvedDotnetDir: null, resolvedDotnetupDir: null);
        state.NeedsAdditions.Should().BeTrue();
        state.NeedsRemovals.Should().BeFalse();
        state.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Shell_DotnetupOn_DotnetResolvesElsewhere_NeedsAdditions()
    {
        // A system dotnet wins resolution, not the managed one → still needs the managed dotnet added.
        var state = Evaluate(DotnetAccessMode.Shell, dotnetupOnPath: true, "/usr/bin", DotnetupDir);
        state.NeedsAdditions.Should().BeTrue();
        state.NeedsRemovals.Should().BeFalse();
    }

    [TestMethod]
    public void None_DotnetupOn_OnlyDotnetupResolves_IsActive()
        => Evaluate(DotnetAccessMode.None, dotnetupOnPath: true, resolvedDotnetDir: null, resolvedDotnetupDir: DotnetupDir).IsActive.Should().BeTrue();

    [TestMethod]
    public void None_DotnetupOn_StaleManagedDotnetResolves_NeedsRemovals()
    {
        // 'none' means the managed dotnet should NOT win; a stale managed dotnet means a removal
        // (only a new terminal can drop it).
        var state = Evaluate(DotnetAccessMode.None, dotnetupOnPath: true, DotnetDir, DotnetupDir);
        state.NeedsRemovals.Should().BeTrue();
        state.NeedsAdditions.Should().BeFalse();
    }

    [TestMethod]
    public void None_DotnetupOff_NeitherResolvesToManaged_IsActive()
        => Evaluate(DotnetAccessMode.None, dotnetupOnPath: false, resolvedDotnetDir: null, resolvedDotnetupDir: null).IsActive.Should().BeTrue();

    [TestMethod]
    public void None_DotnetupOff_StaleManagedDotnetupResolves_NeedsRemovals()
        => Evaluate(DotnetAccessMode.None, dotnetupOnPath: false, resolvedDotnetDir: null, resolvedDotnetupDir: DotnetupDir).NeedsRemovals.Should().BeTrue();

    [TestMethod]
    public void All_DotnetupOn_BothResolveToManaged_IsActive()
        => Evaluate(DotnetAccessMode.Everywhere, dotnetupOnPath: true, DotnetDir, DotnetupDir).IsActive.Should().BeTrue();

    [TestMethod]
    public void Shell_DotnetupOff_AddDotnetButRemoveDotnetup_NeedsBoth()
    {
        // dotnet should be present but isn't (addition); dotnetup should be absent but resolves (removal).
        var state = Evaluate(DotnetAccessMode.Shell, dotnetupOnPath: false, resolvedDotnetDir: null, resolvedDotnetupDir: DotnetupDir);
        state.NeedsAdditions.Should().BeTrue();
        state.NeedsRemovals.Should().BeTrue();
    }
}
