// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem
{
    [Obsolete("Use IPhysicalFileSystem properties instead.")]
    public interface IFileLastWriteTimeSource
    {
        DateTime GetLastWriteTimeUtc(string file);

        void SetLastWriteTimeUtc(string file, DateTime lastWriteTimeUtc);
    }
}
