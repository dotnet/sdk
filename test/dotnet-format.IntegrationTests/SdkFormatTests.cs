// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Format integration tests for the dotnet/sdk repository.
/// </summary>
public class SdkFormatTests : FormatIntegrationTestBase
{
    // Commit SHA aligned with .NET 10.0 GA release (v10.0.100).
    // See https://github.com/dotnet/dotnet/blob/v10.0.100/src/source-manifest.json

    protected override string RepoUrl => "https://github.com/dotnet/sdk";
    protected override string Sha => "e6bc966cc3d1348265b0831c6daca23267169d8f";
    protected override string TargetSolution => "sdk.slnx";
    protected override string RepoName => "sdk";
    protected override int Priority => 0;

    public SdkFormatTests(ITestOutputHelper output) : base(output) { }
}
