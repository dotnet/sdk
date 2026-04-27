// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    internal class PhysicalFileSystemEx : PhysicalFileSystem, IPhysicalFileSystemEx
    {
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
            => File.ReadAllTextAsync(path, cancellationToken);

        public Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
            => File.WriteAllTextAsync(path, contents, cancellationToken);
    }
}
