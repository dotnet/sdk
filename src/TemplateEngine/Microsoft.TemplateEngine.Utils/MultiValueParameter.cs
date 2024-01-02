// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

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

        public static bool TryPerformMultiValueEqual(object x, object y, out bool result)
        {
            bool isxMv = x is MultiValueParameter;
            bool isyMv = y is MultiValueParameter;

            if (!isxMv && !isyMv)
            {
                result = false;
                return false;
            }

            {
                if (x is MultiValueParameter mv && y is string sv)
                {
                    result = MultiValueEquals(mv, sv);
                    return true;
                }
            }

            {
                if (y is MultiValueParameter mv && x is string sv)
                {
                    result = MultiValueEquals(mv, sv);
                    return true;
                }
            }

            result = Equals(x, y);
            return true;
        }

        public override string ToString() => string.Join(MultiValueSeparator.ToString(), Values);

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((MultiValueParameter)obj);
        }

        public override int GetHashCode() => Values.OrderBy(v => v).ToCsvString().GetHashCode();

        protected bool Equals(MultiValueParameter other)
        {
            var set1 = new HashSet<string>(Values);
            var set2 = new HashSet<string>(other.Values);
            return set1.SetEquals(set2);
        }

        private static bool MultiValueEquals(MultiValueParameter mv, string comparand)
        {
            foreach (string s in mv.Values)
            {
                if (string.Equals(s, comparand, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
