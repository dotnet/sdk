// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines the priority of a template parameter.
    /// </summary>
    public enum TemplateParameterPriority
    {
        /// <summary>
        /// The parameter is mandatory.
        /// </summary>
        Required,

        [Obsolete("the value was never used and is deprecated.")]
        Suggested,

        /// <summary>
        /// The parameter is optional.
        /// </summary>
        Optional,

        /// <summary>
        /// The parameter is implicit (built-in).
        /// </summary>
        Implicit
    }
}
