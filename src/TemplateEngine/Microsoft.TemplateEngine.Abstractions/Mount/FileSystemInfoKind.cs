// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    /// <summary>
    /// Defines the kind of a <see cref="IFileSystemInfo"/> entry.
    /// </summary>
    public enum FileSystemInfoKind
    {
        /// <summary>
        /// Entry is a file.
        /// </summary>
        File,

        /// <summary>
        /// Entry is a directory.
        /// </summary>
        Directory
    }
}
