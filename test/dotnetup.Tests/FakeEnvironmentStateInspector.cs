// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

/// <summary>
/// Test double for <see cref="IEnvironmentStateInspector"/> that returns a fixed observed state,
/// so command tests are not affected by the real machine's registry / shell profiles. Defaults to
/// <see cref="ObservedEnvironmentState.Empty"/> (nothing observed as wired).
/// </summary>
internal sealed class FakeEnvironmentStateInspector : IEnvironmentStateInspector
{
    private readonly ObservedEnvironmentState _state;

    public FakeEnvironmentStateInspector(ObservedEnvironmentState? state = null)
        => _state = state ?? ObservedEnvironmentState.Empty;

    public ObservedEnvironmentState Inspect(IEnvShellProvider? shellProvider) => _state;
}
