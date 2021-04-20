// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.TemplateEngine.Utils
{
    public static class ParserExtensions
    {
        /// <summary>
        /// <see cref="double.TryParse(string, out double)"/> extension that try to parse <paramref name="stringValue"/> first in current culture, then in invariant culture.
        /// </summary>
        /// <param name="stringValue">The value to parse</param>
        /// <param name="doubleValue">Parsed double value if <paramref name="stringValue"/> can be parsed</param>
        /// <returns>
        /// true when <paramref name="stringValue"/> can be parsed in current or invariant culture.
        /// false when <paramref name="stringValue"/> cannot be parsed in current or invariant culture.
        /// </returns>
        public static bool DoubleTryParseСurrentOrInvariant(string stringValue, out double doubleValue)
        {
            if (double.TryParse(stringValue, NumberStyles.Float, CultureInfo.CurrentCulture, out doubleValue))
            {
                return true;
            }
            else
            {
                return double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue);
            }
        }

        /// <summary>
        /// <see cref="Convert.ToDouble(object)"/> extension that try to parse <paramref name="value"/> first in current culture, then in invariant culture.
        /// </summary>
        /// <param name="value">The value to parse</param>
        /// <returns>Parsed double value if <paramref name="value"/> can be parsed</returns>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format for a <see cref="Double"/> type.</exception>
        public static double ConvertToDoubleCurrentOrInvariant(object value)
        {
            try
            {
                if (value is string s)
                {
                    return double.Parse(s, NumberStyles.Float);
                }
                return Convert.ToDouble(value, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                if (value is string s)
                {
                    return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
