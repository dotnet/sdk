// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    /// <summary>
    /// Defines a file <see cref="IFileSystemInfo"/> entry.
    /// </summary>
    public interface IFile : IFileSystemInfo
    {
        /// <summary>
        /// Opens the file stream for reading.
        /// </summary>
        /// <returns><see cref="Stream"/> that can be used for reading the file.</returns>
        Stream OpenRead();
    }
}
