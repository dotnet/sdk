// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IFile : IFileSystemInfo
    {
        Stream OpenRead();
    }
}
