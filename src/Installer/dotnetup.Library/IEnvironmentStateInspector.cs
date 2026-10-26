// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Reads the live environment into an <see cref="ObservedEnvironmentState"/>. Abstracted so the
/// <c>env</c> commands can be unit-tested with a controlled observation instead of the real
/// registry / shell profiles. The production implementation is <see cref="EnvironmentStateInspector"/>.
/// </summary>
internal interface IEnvironmentStateInspector
{
    ObservedEnvironmentState Inspect(IEnvShellProvider? shellProvider);
}
