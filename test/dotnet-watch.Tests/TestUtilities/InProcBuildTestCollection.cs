// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// All tests that validate msbuild build in-proc must be included in this collection
/// as mutliple builds can't run in parallel in the same process.
/// </summary>
[CollectionDefinition(nameof(InProcBuildTestCollection), DisableParallelization = true)]
public sealed class InProcBuildTestCollection
{
}
