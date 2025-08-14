// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal interface IDirectoryWatcher : IDisposable
    {
        event EventHandler<ChangedPath> OnFileChange;

        event EventHandler<Exception> OnError;

        string WatchedDirectory { get; }

        bool EnableRaisingEvents { get; set; }
    }
}
