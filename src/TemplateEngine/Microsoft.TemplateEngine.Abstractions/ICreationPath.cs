// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Contains information about primary output on template instantiation.
    /// </summary>
    public interface ICreationPath
    {
        /// <summary>
        /// The relative path to primary output. The path is relative to output directory.
        /// </summary>
        string Path { get; }
    }
}
