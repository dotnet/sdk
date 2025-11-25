// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.MSBuildEvaluation;

namespace Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

/// <summary>
/// Represents the result of evaluation for mon-SDK style project.
/// </summary>
internal class NonSDKStyleEvaluationResult : MSBuildEvaluationResult
{
    private NonSDKStyleEvaluationResult(string projectPath) : base(EvalStatus.Succeeded, projectPath) { }

    internal string? TargetFrameworkVersion => EvaluatedProject?.GetPropertyValue("TargetFrameworkVersion");

    internal string? PlatformTarget => EvaluatedProject?.GetPropertyValue("PlatformTarget");

    internal static NonSDKStyleEvaluationResult CreateSuccess(string path, DotNetProject project)
    {
        return new NonSDKStyleEvaluationResult(path)
        {
            EvaluatedProject = project,
        };
    }
}
