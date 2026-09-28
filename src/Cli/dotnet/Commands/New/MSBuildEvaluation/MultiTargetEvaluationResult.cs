// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MSBuildProject = Microsoft.Build.Evaluation.Project;

namespace Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

/// <summary>
/// Represents the result of evaluation for multi-target project.
/// </summary>
internal class MultiTargetEvaluationResult : MSBuildEvaluationResult
{
    private MultiTargetEvaluationResult(string projectPath) : base(EvalStatus.Succeeded, projectPath) { }

    internal IReadOnlyDictionary<string, MSBuildProject?> EvaluatedProjects { get; private set; } = new Dictionary<string, MSBuildProject?>();

    internal IEnumerable<string> TargetFrameworks => EvaluatedProjects.Keys;

    internal static MultiTargetEvaluationResult CreateSuccess(string path, MSBuildProject project, IReadOnlyDictionary<string, MSBuildProject?> frameworkBasedResults)
    {
        return new MultiTargetEvaluationResult(path)
        {
            EvaluatedProject = project,
            EvaluatedProjects = frameworkBasedResults,
        };
    }
}
