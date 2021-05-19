// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    /// <summary>
    /// Defines directory <see cref="IFileSystemInfo"/> entry.
    /// </summary>
    public interface IDirectory : IFileSystemInfo
    {
        /// <summary>
        /// Enumerates the <see cref="IFileSystemInfo"/> entries in the directory.
        /// </summary>
        /// <param name="pattern">Matching pattern for the entries, see <see cref="Directory.EnumerateFileSystemEntries(string, string, SearchOption)"/> for more details.</param>
        /// <param name="searchOption">Matching pattern for the entries, see <see cref="Directory.EnumerateFileSystemEntries(string, string, SearchOption)"/> for more details.</param>
        /// <returns>The enumerator to <see cref="IFileSystemInfo"/> entries in the directory.</returns>
        IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string pattern, SearchOption searchOption);

        /// <summary>
        /// Enumerates the <see cref="IFile"/> entries in the directory.
        /// </summary>
        /// <param name="pattern">Matching pattern for the entries, see <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/> for more details.</param>
        /// <param name="searchOption">Matching pattern for the entries, see <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/> for more details.</param>
        /// <returns>The enumerator to <see cref="IFile"/> entries in the directory.</returns>
        IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption);

        /// <summary>
        /// Enumerates the <see cref="IDirectory"/> entries in the directory.
        /// </summary>
        /// <param name="pattern">Matching pattern for the entries, see <see cref="Directory.EnumerateDirectories(string, string, SearchOption)"/> for more details.</param>
        /// <param name="searchOption">Matching pattern for the entries, see <see cref="Directory.EnumerateDirectories(string, string, SearchOption)"/> for more details.</param>
        /// <returns>The enumerator to <see cref="IDirectory"/> entries in the directory.</returns>
        IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption);
    }
}
