// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines baseline configuration information.
    /// Baseline configuration is a set of predefined template parameters and their values.
    /// </summary>
    public interface IBaselineInfo
    {
        /// <summary>
        /// Gets baseline description.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets collection of parameters to be set with given baseline.
        /// </summary>
        IReadOnlyDictionary<string, string> DefaultOverrides { get; }
    }
}
