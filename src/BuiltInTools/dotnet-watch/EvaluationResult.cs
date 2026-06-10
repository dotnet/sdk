// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher;

internal sealed class EvaluationResult(IReadOnlyDictionary<string, FileItem> files)
{
    public readonly IReadOnlyDictionary<string, FileItem> Files = files;
}
