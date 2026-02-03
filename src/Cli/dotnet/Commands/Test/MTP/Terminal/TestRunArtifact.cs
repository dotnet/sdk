// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// An artifact / attachment that was reported during run.
/// </summary>
internal sealed record TestRunArtifact(bool OutOfProcess, string? Assembly, string? TargetFramework, string? Architecture, string? ExecutionId, string? TestName, string Path);
