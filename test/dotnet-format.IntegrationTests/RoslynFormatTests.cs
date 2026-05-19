// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Format integration tests for the dotnet/roslyn repository.
/// </summary>
public class RoslynFormatTests : FormatIntegrationTestBase
{
    // Commit SHA aligned with .NET 10.0 GA release (v10.0.100).
    // See https://github.com/dotnet/dotnet/blob/v10.0.100/src/source-manifest.json

    protected override string RepoUrl => "https://github.com/dotnet/roslyn";
    protected override string Sha => "739dc0e352a331e8a41cd66c09d2edf359255365";
    protected override string TargetSolution => "Compilers.slnf";
    protected override string RepoName => "roslyn";

    public RoslynFormatTests(ITestOutputHelper output) : base(output) { }
}
