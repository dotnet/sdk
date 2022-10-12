// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    internal interface IPhysicalFileSystemEx : IPhysicalFileSystem
    {
        /// <summary>
        /// Same behavior as <see cref="System.IO.File.ReadAllTextAsync(string,CancellationToken)"/>.
        /// </summary>
        Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Same behavior as <see cref="File.WriteAllTextAsync(string,string?,CancellationToken)"/>.
        /// </summary>
        Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default);
    }
}
