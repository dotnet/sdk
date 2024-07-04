// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watcher
{
    internal abstract class FileSetFactory
    {
        protected abstract ValueTask<(ProjectInfo project, FileSet files)?> CreateAsync(bool waitOnError, CancellationToken cancellationToken);

        public async ValueTask<(ProjectInfo project, FileSet files)> CreateAsync(CancellationToken cancellationToken)
        {
            var result = await CreateAsync(waitOnError: true, cancellationToken);
            Debug.Assert(result != null);
            return result.Value;
        }

        public ValueTask<(ProjectInfo project, FileSet files)?> TryCreateAsync(CancellationToken cancellationToken)
            => CreateAsync(waitOnError: false, cancellationToken);
    }
}
