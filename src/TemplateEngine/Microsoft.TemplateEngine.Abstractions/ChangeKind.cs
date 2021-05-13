// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines the kind of file change done during template creation.
    /// </summary>
    public enum ChangeKind
    {
        /// <summary>
        /// The file is created.
        /// </summary>
        Create,

        /// <summary>
        /// The file is removed.
        /// </summary>
        Delete,

        /// <summary>
        /// The file is overwritten.
        /// </summary>
        Overwrite,

        /// <summary>
        /// The file is modified.
        /// </summary>
        Change
    }
}
