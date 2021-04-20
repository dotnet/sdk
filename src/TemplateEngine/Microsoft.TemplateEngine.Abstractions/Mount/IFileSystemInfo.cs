// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IFileSystemInfo
    {
        bool Exists { get; }

        string FullPath { get; }

        FileSystemInfoKind Kind { get; }

        IDirectory Parent { get; }

        string Name { get; }

        IMountPoint MountPoint { get; }
    }
}
