// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Container type holding the values for parameters with multiple values.
    /// </summary>
    public class MultiValueParameter
    {
        /// <summary>
        /// Separator of multi valued parameters (currently applicable only to choices).
        /// </summary>
        public const char MultiValueSeparator = '|';

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiValueParameter"/> class.
        /// </summary>
        /// <param name="values"></param>
        public MultiValueParameter(IReadOnlyList<string> values)
        {
            Values = values;
        }

        /// <summary>
        /// Set of characters that can be used for separating multi valued parameters (currently applicable only to choices).
        /// </summary>
        public static char[] MultiValueSeparators { get; } = new[] { MultiValueSeparator, ',' };

        /// <summary>
        /// The actual atomic values specified for the parameter.
        /// </summary>
        public IReadOnlyList<string> Values { get; private init; }

        /// <inheritdoc/>
        public override string ToString() => string.Join(MultiValueSeparator.ToString(), Values);
    }
}
