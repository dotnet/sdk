// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestBuildEvaluator(DotNetWatchContext context, MSBuildFileSetFactory factory)
    : BuildEvaluator(context)
{
    public Dictionary<string, DateTime> Timestamps { get; } = [];

    protected override DateTime GetLastWriteTimeUtcSafely(string file) => Timestamps[file];
    protected override MSBuildFileSetFactory CreateMSBuildFileSetFactory() => factory;
}
