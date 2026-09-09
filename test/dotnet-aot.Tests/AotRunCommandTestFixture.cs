// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
/// Contains paths and launch artifacts for an <see cref="AotRunCommandTests"/> fixture.
/// </summary>
/// <param name="TestDirectory">The fixture source directory.</param>
/// <param name="EntryPointPath">The file-based application entry point.</param>
/// <param name="ArtifactsPath">The application artifacts directory.</param>
/// <param name="SuccessCachePath">The successful-build cache path.</param>
/// <param name="LaunchArtifacts">The synthetic application launch artifacts.</param>
internal sealed record AotRunCommandTestFixture(
    string TestDirectory,
    string EntryPointPath,
    string ArtifactsPath,
    string SuccessCachePath,
    (string AppHost, string Assembly, string RuntimeConfig) LaunchArtifacts);
