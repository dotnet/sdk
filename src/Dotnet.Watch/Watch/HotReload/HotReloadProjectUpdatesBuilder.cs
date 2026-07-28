// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Managed code update annotated with the path of the project it originates from.
/// Allows tracking updates produced by both the Roslyn (C#/VB) and the F# update paths.
/// </summary>
internal readonly record struct ManagedCodeUpdateEnvelope(string ProjectPath, HotReloadManagedCodeUpdate Update);

internal sealed class HotReloadProjectUpdatesBuilder
{
    public List<ManagedCodeUpdateEnvelope> ManagedCodeUpdates { get; } = [];
    public Dictionary<RunningProject, List<StaticWebAsset>> StaticAssetsToUpdate { get; } = [];
    public List<string> ProjectsToRebuild { get; } = [];
    public List<string> ProjectsToRedeploy { get; } = [];
    public List<RunningProject> ProjectsToRestart { get; } = [];
}
