// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal interface IFileSystemWatcher : IDisposable
    {
        event EventHandler<(string filePath, bool newFile)> OnFileChange;

        event EventHandler<Exception> OnError;

        string BasePath { get; }

        bool EnableRaisingEvents { get; set; }
    }
}
