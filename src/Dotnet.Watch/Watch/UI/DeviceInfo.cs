// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Represents a device item returned from the ComputeAvailableDevices MSBuild target.
/// </summary>
internal sealed record DeviceInfo(string Id, string? Description, string? Type, string? Status, string? RuntimeIdentifier);
