﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class MockFileSetFactory() : MSBuildFileSetFactory(
    rootProjectFile: "test.csproj",
    buildArguments: [],
    new ProcessRunner(processCleanupTimeout: TimeSpan.Zero),
    new BuildReporter(NullLogger.Instance, new GlobalOptions(), TestOptions.GetEnvironmentOptions(Environment.CurrentDirectory, "dotnet") is var options ? options : options))
{
    public Func<EvaluationResult?>? TryCreateImpl;

    public override ValueTask<EvaluationResult?> TryCreateAsync(bool? requireProjectGraph, CancellationToken cancellationToken)
        => ValueTask.FromResult(TryCreateImpl?.Invoke());
}
