// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Extensions helper methods for work with <see cref="ITemplateParameter"/>.
    /// </summary>
    public static class TemplateParameterExtensions
    {
        /// <summary>
        /// Indicates whether the input parameter is of a choice type.
        /// </summary>
        /// <param name="parameter">Parameter to be inspected.</param>
        /// <returns>True if given parameter is of a choice type, false otherwise.</returns>
        public static bool IsChoice(this ITemplateParameter parameter)
        {
            return parameter.DataType?.Equals("choice", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// Splits a string value representing a multi valued parameter (currently applicable only to choices) into atomic tokens.
        /// </summary>
        /// <param name="literal">A string representing multi valued parameter.</param>
        /// <returns>List of atomic string tokens.</returns>
        public static IReadOnlyList<string> TokenizeMultiValueParameter(this string literal)
        {
            return literal.Split(MultiValueParameter.MultiValueSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Check a multi valued parameter value (currently applicable only to choices), whether it doesn't contain any disallowed (separator) characters.
        /// </summary>
        /// <param name="value">Parameter value to be checked.</param>
        /// <returns>True if given value doesn't contain any disallowed characters, false otherwise.</returns>
        public static bool IsValidMultiValueParameterValue(this string value)
        {
            return value.IndexOfAny(MultiValueParameter.MultiValueSeparators) == -1;
        }
    }

}
