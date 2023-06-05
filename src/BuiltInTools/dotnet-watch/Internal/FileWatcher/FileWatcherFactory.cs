﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
