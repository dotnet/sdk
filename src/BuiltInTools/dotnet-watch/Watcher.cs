// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Tools;

namespace Microsoft.DotNet.Watcher
{
    internal abstract class Watcher(DotNetWatchContext context, MSBuildFileSetFactory rootFileSetFactory)
    {
        public DotNetWatchContext Context => context;
        public MSBuildFileSetFactory RootFileSetFactory => rootFileSetFactory;

        public abstract Task WatchAsync(CancellationToken cancellationToken);
    }
}
