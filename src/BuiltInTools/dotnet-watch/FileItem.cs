// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher
{
    internal readonly record struct FileItem
    {
        public string FilePath { get; init; }

        /// <summary>
        /// List of all projects that contain this file (does not contain duplicates).
        /// </summary>
        public List<string> ContainingProjectPaths { get; init; }

        public string? StaticWebAssetPath { get; init; }

        public ChangeKind Change { get; init; }

        public bool IsStaticFile => StaticWebAssetPath != null;
    }
}
