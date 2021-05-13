// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Represents file change occurred during template instantiation.
    /// The new version of the interface <see cref="IFileChange2"/>.
    /// </summary>
    public interface IFileChange
    {
        /// <summary>
        /// Gets the file target path. The path is relative to output directory.
        /// </summary>
        string TargetRelativePath { get; }

        /// <summary>
        /// Gets the change kind.
        /// </summary>
        ChangeKind ChangeKind { get; }

        /// <summary>
        /// Gets the file content.
        /// </summary>
        byte[] Contents { get; }
    }

    /// <summary>
    /// Represents file change occurred during template instantiation.
    /// </summary>
    public interface IFileChange2 : IFileChange
    {
        /// <summary>
        /// Gets the file source path in template definition. The path is relative to template root directory.
        /// </summary>
        string SourceRelativePath { get; }
    }
}
