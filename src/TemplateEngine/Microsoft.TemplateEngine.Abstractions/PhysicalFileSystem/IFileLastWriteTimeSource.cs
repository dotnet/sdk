// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem
{
    public interface IFileLastWriteTimeSource
    {
        DateTime GetLastWriteTimeUtc(string file);

        void SetLastWriteTimeUtc(string file, DateTime lastWriteTimeUtc);
    }
}
