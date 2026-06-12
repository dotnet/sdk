// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

internal class MultipleProjectsEvaluationResult : MSBuildEvaluationResult
{
    private MultipleProjectsEvaluationResult() : base(EvalStatus.MultipleProjectFound) { }

    internal IReadOnlyList<string> ProjectPaths { get; private set; } = [];

    internal static MultipleProjectsEvaluationResult Create(IReadOnlyList<string> projectPaths)
    {
        return new MultipleProjectsEvaluationResult()
        {
            ProjectPaths = projectPaths,
            ErrorMessage = string.Format(CliCommandStrings.MultipleProjectsEvaluationResult_Error, string.Join("; ", projectPaths))
        };
    }
}
