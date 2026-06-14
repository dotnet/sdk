// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Format integration tests for the dotnet/efcore repository.
/// </summary>
public class EfCoreFormatTests : FormatIntegrationTestBase
{
    // Commit SHA aligned with .NET 10.0 GA release (v10.0.100).
    // See https://github.com/dotnet/dotnet/blob/v10.0.100/src/source-manifest.json

    protected override string RepoUrl => "https://github.com/dotnet/efcore";
    protected override string Sha => "12b8d44bf691d2e6933a6d1003647cce4f13c3d3";
    protected override string TargetSolution => "EFCore.sln";
    protected override string RepoName => "efcore";

    public EfCoreFormatTests(ITestOutputHelper output) : base(output) { }
}
