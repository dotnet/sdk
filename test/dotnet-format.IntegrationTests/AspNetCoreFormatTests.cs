// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Format integration tests for the dotnet/aspnetcore repository.
/// </summary>
public class AspNetCoreFormatTests : FormatIntegrationTestBase
{
    // Commit SHA aligned with .NET 10.0 GA release (v10.0.100).
    // See https://github.com/dotnet/dotnet/blob/v10.0.100/src/source-manifest.json

    protected override string RepoUrl => "https://github.com/dotnet/aspnetcore";
    protected override string Sha => "7387de91234d3ef751fa50b3d1bfede4130213ff";
    protected override string TargetSolution => "AspNetCore.slnx";
    protected override string RepoName => "aspnetcore";

    public AspNetCoreFormatTests(ITestOutputHelper output) : base(output) { }
}
