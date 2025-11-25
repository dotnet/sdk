// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.MSBuildEvaluation;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

/// <summary>
/// Represents the result of evaluation for multi-target project.
/// </summary>
internal class MultiTargetEvaluationResult : MSBuildEvaluationResult
{
    private MultiTargetEvaluationResult(string projectPath) : base(EvalStatus.Succeeded, projectPath) { }

    internal IReadOnlyDictionary<NuGetFramework, DotNetProject> EvaluatedProjects { get; private set; } = new Dictionary<NuGetFramework, DotNetProject>();

    internal IEnumerable<NuGetFramework> TargetFrameworks => EvaluatedProjects.Keys;

    internal static MultiTargetEvaluationResult CreateSuccess(string path, DotNetProject project, IReadOnlyDictionary<NuGetFramework, DotNetProject> frameworkBasedResults)
    {
        return new MultiTargetEvaluationResult(path)
        {
            EvaluatedProject = project,
            EvaluatedProjects = frameworkBasedResults,
        };
    }
}
