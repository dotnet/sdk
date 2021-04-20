// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Abstractions.TemplateFiltering
{
    /// <summary>
    /// Defines the match kind: how the filter with its input values matches the property(ies) of <see cref="ITemplateInfo"/>.
    /// </summary>
    public enum MatchKind
    {
        /// <summary>
        /// The filter exactly matches the value of <see cref="ITemplateInfo"/>.
        /// </summary>
        Exact,

        /// <summary>
        /// The filter partially matches the value of <see cref="ITemplateInfo"/>.
        /// For example, <see cref="ITemplateInfo.Name"/> contains the value of the filter but not equal to it.
        /// </summary>
        Partial,

        /// <summary>
        /// The filter does not match the value of <see cref="ITemplateInfo"/>.
        /// Example: the filter is checking <see cref="ITemplateInfo.CacheParameters"/> and the parameter name is not defined in <see cref="ITemplateInfo"/>.
        /// </summary>
        Mismatch,

        /// <summary>
        /// The input passed to the filter is incorrect, impossible to identify property of <see cref="ITemplateInfo"/> to check.
        /// Example: the filter is checking <see cref="ITemplateInfo.CacheParameters"/> and the parameter name is not defined in <see cref="ITemplateInfo"/>.
        /// </summary>
        InvalidName,

        /// <summary>
        /// The input passed to the filter is incorrect: the value is different format that is supported by <see cref="ITemplateInfo"/> property.
        /// Example: the filter is checking <see cref="ITemplateInfo.CacheParameters"/> and the parameter is boolean but value to match with is string.
        /// </summary>
        InvalidValue,

        [Obsolete("This value will be removed in next release, use + " + nameof(MatchKind.Mismatch) + " or " + nameof(MatchKind.InvalidValue) + " instead")]
        AmbiguousValue,

        [Obsolete("This value will be removed in next release, use + " + nameof(MatchKind.Mismatch) + " or " + nameof(MatchKind.InvalidValue) + " instead")]
        SingleStartsWith
    }
}
