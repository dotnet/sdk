// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal sealed class EvaluationResult(IReadOnlyDictionary<string, FileItem> files, ProjectGraph? projectGraph)
{
    public readonly IReadOnlyDictionary<string, FileItem> Files = files;
    public readonly ProjectGraph? ProjectGraph = projectGraph;
}
