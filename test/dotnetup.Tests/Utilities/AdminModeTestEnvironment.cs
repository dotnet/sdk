// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// A temporary dotnetup data directory plus a machine policy backed by an in-memory store. Pass
/// <see cref="Policy"/> (or a switcher built by <see cref="CreateSwitcher"/>) to the code under test;
/// nothing here replaces process-wide policy state.
/// </summary>
internal sealed class AdminModeTestEnvironment : IDisposable
{
    private readonly TestEnvironment _files = new();

    public AdminModeTestEnvironment()
    {
        DotnetupPaths.SetTestDataDirectoryOverride(TempRoot);
        Policy = new DotnetupPolicy(Store);
    }

    public string TempRoot => _files.TempRoot;

    /// <summary>The in-memory machine policy values.</summary>
    public FakeDotnetupPolicyStore Store { get; } = new();

    /// <summary>The policy over <see cref="Store"/>.</summary>
    public DotnetupPolicy Policy { get; }

    /// <summary>Creates an elevator whose elevated operation writes <see cref="Policy"/>.</summary>
    public FakeAdminModeElevator CreateElevator(Exception? failure = null, bool appliesChange = true) =>
        new(Policy) { Failure = failure, AppliesChange = appliesChange };

    /// <summary>Creates a switcher over <see cref="Policy"/> that runs as an unprivileged process.</summary>
    public AdminModeSwitcher CreateSwitcher(IAdminModeElevator elevator, bool privileged = false, StringWriter? output = null) =>
        new(Policy, elevator, () => privileged, AdminModeSwitcherTests.CreateConsole(output ?? new StringWriter()));

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        _files.Dispose();
    }
}
