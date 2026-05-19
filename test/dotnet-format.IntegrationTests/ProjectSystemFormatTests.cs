// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Format integration tests for the dotnet/project-system repository.
/// </summary>
public class ProjectSystemFormatTests : FormatIntegrationTestBase
{
    // Commit SHA aligned with .NET 10.0 GA release (v10.0.100).
    // See https://github.com/dotnet/dotnet/blob/v10.0.100/src/source-manifest.json

    protected override string RepoUrl => "https://github.com/dotnet/project-system";
    protected override string Sha => "e660d54d6b3198751bd0502fe270e1657f32a913";
    protected override string TargetSolution => "ProjectSystem.sln";
    protected override string RepoName => "project-system";
    protected override bool UseRepoBuildScript => false;

    public ProjectSystemFormatTests(ITestOutputHelper output) : base(output) { }
}
