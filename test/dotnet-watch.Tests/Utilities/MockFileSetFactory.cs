// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools;

internal class MockFileSetFactory() : MSBuildFileSetFactory(
    rootProjectFile: "test.csproj",
    targetFramework: null,
    buildProperties: [],
    new EnvironmentOptions(Environment.CurrentDirectory, "dotnet"),
    NullReporter.Singleton)
{
    public Func<EvaluationResult> TryCreateImpl;

    public override ValueTask<EvaluationResult> TryCreateAsync(bool? requireProjectGraph, CancellationToken cancellationToken)
        => ValueTask.FromResult(TryCreateImpl?.Invoke());
}
