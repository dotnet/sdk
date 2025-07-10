// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

internal class MockFileSetFactory() : MSBuildFileSetFactory(
    rootProjectFile: "test.csproj",
    buildArguments: [],
    new ProcessRunner((TestOptions.GetEnvironmentOptions(Environment.CurrentDirectory, "dotnet") is var options ? options : options).ProcessCleanupTimeout, CancellationToken.None),
    new BuildReporter(NullReporter.Singleton, options))
{
    public Func<EvaluationResult?>? TryCreateImpl;

    public override ValueTask<EvaluationResult?> TryCreateAsync(bool? requireProjectGraph, CancellationToken cancellationToken)
        => ValueTask.FromResult(TryCreateImpl?.Invoke());
}
