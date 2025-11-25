// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.MSBuildEvaluation;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

/// <summary>
/// Represents the result of evaluation for single-target SDK style project.
/// </summary>
internal class SDKStyleEvaluationResult : MSBuildEvaluationResult
{
    private SDKStyleEvaluationResult(string projectPath, NuGetFramework targetFramework) : base(EvalStatus.Succeeded, projectPath)
    {
        TargetFramework = targetFramework;
    }

    internal NuGetFramework TargetFramework { get; }

    internal static SDKStyleEvaluationResult CreateSuccess(string path, NuGetFramework targetFramework, DotNetProject project)
    {
        return new SDKStyleEvaluationResult(path, targetFramework)
        {
            EvaluatedProject = project,
        };
    }
}
