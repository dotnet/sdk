// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

using Xunit;

/// <summary>
/// Collection definition for tests that require MSBuild to be run.
/// </summary>
/// <remarks>
/// This collection is used to ensure that tests that require MSBuild are run serially.
/// The MSBuild engine only allows a single logical Build to run at once, so running tests
/// that require MSBuild in parallel can cause tests to fail./
/// </remarks>
[CollectionDefinition(nameof(MSBuildCollection), DisableParallelization = true)]
public class MSBuildCollection : ICollectionFixture<MSBuildCollection>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
