// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

/// <summary>
/// The selected device and the runtime identifier it requires, if any.
/// Used to evaluate, build, and launch the project. See <see cref="DeviceInfo"/> for device selection.
/// </summary>
internal readonly record struct DeviceId
{
    public DeviceId(string name, string? runtimeIdentifier)
    {
        Name = name;
        RuntimeIdentifier = string.IsNullOrEmpty(runtimeIdentifier) ? null : runtimeIdentifier;
    }

    public string Name { get; }
    public string? RuntimeIdentifier { get; }
}
