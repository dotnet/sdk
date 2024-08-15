﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools;

internal class MockFileSetFactory() : MSBuildFileSetFactory(
    rootProjectFile: "test.csproj",
    targetFramework: null,
    buildProperties: null,
    new EnvironmentOptions(Environment.CurrentDirectory, "dotnet"),
    NullReporter.Singleton,
    outputSink: null,
    trace: false)
{
    public Func<EvaluationResult> TryCreateImpl;

    public override ValueTask<EvaluationResult> TryCreateAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(TryCreateImpl?.Invoke());
}
