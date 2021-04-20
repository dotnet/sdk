// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IDirectory : IFileSystemInfo
    {
        IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string patten, SearchOption searchOption);

        IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption);

        IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption);
    }
}
