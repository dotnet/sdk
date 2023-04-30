// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Watcher
{
    internal readonly struct FileItem
    {
        public string FilePath { get; init; }

        public string ProjectPath { get; init; }

        public bool IsStaticFile { get; init; }

        public string StaticWebAssetPath { get; init; }

        public bool IsNewFile { get; init; }
    }
}
