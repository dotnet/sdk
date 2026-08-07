// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal static class FileWatcherFactory
    {
        public static IDirectoryWatcher CreateWatcher(string watchedDirectory)
            => CreateWatcher(watchedDirectory, EnvironmentVariables.IsPollingEnabled);

        public static IDirectoryWatcher CreateWatcher(string watchedDirectory, bool usePollingWatcher)
        {
            return usePollingWatcher ?
                new PollingDirectoryWatcher(watchedDirectory) :
                new EventBasedDirectoryWatcher(watchedDirectory);
        }
    }
}
