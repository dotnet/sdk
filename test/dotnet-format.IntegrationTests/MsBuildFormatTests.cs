// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Format integration tests for the dotnet/msbuild repository.
/// </summary>
public class MsBuildFormatTests : FormatIntegrationTestBase
{
    // Commit SHA aligned with .NET 10.0 GA release (v10.0.100).
    // See https://github.com/dotnet/dotnet/blob/v10.0.100/src/source-manifest.json

    protected override string RepoUrl => "https://github.com/dotnet/msbuild";
    protected override string Sha => "995a3dce41788caebf2b8ca6602a7431f08bfd06";
    protected override string TargetSolution => "MSBuild.sln";
    protected override string RepoName => "msbuild";

    public MsBuildFormatTests(ITestOutputHelper output) : base(output) { }
}
