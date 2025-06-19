// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal static class FileWatcherFactory
    {
        public static IDirectoryWatcher CreateWatcher(string watchedDirectory, bool includeSubdirectories)
            => CreateWatcher(watchedDirectory, EnvironmentVariables.IsPollingEnabled, includeSubdirectories);

        public static IDirectoryWatcher CreateWatcher(string watchedDirectory, bool usePollingWatcher, bool includeSubdirectories)
        {
            return usePollingWatcher ?
                new PollingDirectoryWatcher(watchedDirectory, includeSubdirectories) :
                new EventBasedDirectoryWatcher(watchedDirectory, includeSubdirectories);
        }
    }
}
