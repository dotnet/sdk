// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tools;

internal class MockFileSetFactory : FileSetFactory
{
    public Func<bool, (ProjectInfo, FileSet)> CreateImpl;

    protected override ValueTask<(ProjectInfo project, FileSet files)?> CreateAsync(bool waitOnError, CancellationToken cancellationToken)
        => ValueTask.FromResult(CreateImpl?.Invoke(waitOnError));
}
