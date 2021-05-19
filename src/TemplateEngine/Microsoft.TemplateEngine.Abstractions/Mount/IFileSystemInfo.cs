// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    /// <summary>
    /// Defines a file system entry.
    /// </summary>
    /// <seealso cref="IDirectory"/>
    /// <seealso cref="IFile"/>
    public interface IFileSystemInfo
    {
        /// <summary>
        /// Returns true if file system entry exists, false otherwise.
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Gets the path to file system entry inside mount point <see cref="MountPoint"/>.
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// Gets file system entry kind.
        /// </summary>
        FileSystemInfoKind Kind { get; }

        /// <summary>
        /// Gets parent directory of file system entry.
        /// </summary>
        IDirectory Parent { get; }

        /// <summary>
        /// Gets file system entry name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets mount point for file system entry.
        /// </summary>
        IMountPoint MountPoint { get; }
    }
}
