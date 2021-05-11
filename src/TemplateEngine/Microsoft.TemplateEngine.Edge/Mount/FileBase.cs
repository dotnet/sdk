// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount
{
    internal abstract class FileBase : FileSystemInfoBase, IFile
    {
        protected FileBase(IMountPoint mountPoint, string fullPath, string name)
            : base(mountPoint, fullPath, name, FileSystemInfoKind.File)
        {
        }

        public abstract Stream OpenRead();
    }
}
