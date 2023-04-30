// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal static class FileWatcherFactory
    {
        public static bool IsPollingEnabled
            => Environment.GetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER") is { } value &&
               (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase));

        public static IFileSystemWatcher CreateWatcher(string watchedDirectory)
            => CreateWatcher(watchedDirectory, IsPollingEnabled);

        public static IFileSystemWatcher CreateWatcher(string watchedDirectory, bool usePollingWatcher)
        {
            return usePollingWatcher ?
                new PollingFileWatcher(watchedDirectory) :
                new DotnetFileWatcher(watchedDirectory) as IFileSystemWatcher;
        }
    }
}
