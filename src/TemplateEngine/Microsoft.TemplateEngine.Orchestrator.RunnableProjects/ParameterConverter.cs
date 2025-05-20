// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    internal static class ParameterConverter
    {
        /// <summary>
        /// Tries to convert <paramref name="untypedValue"/> to data type defined in <paramref name="parameter"/>.
        /// Supported types are: choice, bool, int, float, hex, string, text.
        /// </summary>
        /// <param name="parameter">the parameter to convert value to.</param>
        /// <param name="untypedValue">the value to convert.</param>
        /// <param name="convertedValue">the converted value, if the conversion was successful.</param>
        /// <returns>True if the conversion was successful.</returns>
        internal static bool TryConvertParameterValueToType(
            ITemplateParameter parameter,
            string untypedValue,
            out object? convertedValue)
        {
            convertedValue = null;
            if (parameter.IsChoice())
            {
                if (parameter.AllowMultipleValues)
                {
                    List<string> val =
                        untypedValue
                            .TokenizeMultiValueParameter()
                            .Select(t => ResolveChoice(t, parameter))
                            .Where(r => !string.IsNullOrEmpty(r))
                            .Select(r => r!)
                            .ToList();

                    convertedValue = new MultiValueParameter(val);
                    return true;
                }
                else
                {
                    convertedValue = ResolveChoice(untypedValue, parameter);
                    return convertedValue != null;
                }
            }
            return TryConvertLiteralToDatatype(untypedValue, parameter.DataType, out convertedValue);
        }

        /// <summary>
        /// Converts <paramref name="literal"/> to closest data type.
        /// Supported types are: bool, null, float, int, hex, string/text (in order of attempting).
        /// </summary>
        /// <param name="literal">the string value to convert.</param>
        /// <returns>Converted value.</returns>
        internal static object? InferTypeAndConvertLiteral(string literal)
        {
            if (literal == null)
            {
                return null;
            }

            if (!literal.Contains('"'))
            {
                if (TryResolveBooleanValue(literal, out bool parsedBool))
                {
                    return parsedBool;
                }
                if (TryResolveNullValue(literal, out _))
                {
                    return null;
                }
                if ((literal.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
                    || literal.Contains(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator))
                    && TryResolveFloatValue(literal, out double parsedFloat))
                {
                    return parsedFloat;
                }
                if (TryResolveIntegerValue(literal, out long parsedInteger))
                {
                    return parsedInteger;
                }
                if (TryResolveHexValue(literal, out long parsedHex))
                {
                    return parsedHex;
                }
            }
            return literal;
        }

        /// <summary>
        /// Tries to convert <paramref name="literal"/> to <paramref name="dataType"/>.
        /// If <paramref name="dataType"/> is null or empty, the type to be inferred. See <see cref="InferTypeAndConvertLiteral(string)"/> for more details.
        /// </summary>
        internal static bool TryConvertLiteralToDatatype(string literal, string? dataType, out object? value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(dataType))
            {
                value = InferTypeAndConvertLiteral(literal);
                return true;
            }

            if (string.Equals(dataType, "bool", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dataType, "boolean", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveBooleanValue(literal, out bool parsedBool))
                {
                    value = parsedBool;
                    return true;
                }
                return false;
            }
            else if (string.Equals(dataType, "float", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveFloatValue(literal, out double convertedFloat))
                {
                    value = convertedFloat;
                    return true;
                }
                return false;
            }
            else if (string.Equals(dataType, "int", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dataType, "integer", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveIntegerValue(literal, out long convertedInt))
                {
                    value = convertedInt;
                    return true;
                }
                return false;
            }
            else if (string.Equals(dataType, "hex", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveHexValue(literal, out long convertedHex))
                {
                    value = convertedHex;
                    return true;
                }
                return false;
            }
            else if (string.Equals(dataType, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dataType, "string", StringComparison.OrdinalIgnoreCase))
            {
                value = literal;
                return true;
            }
            return false;
        }

        internal static string? GetDefault(string? dataType)
        {
            return dataType switch
            {
                string s when string.IsNullOrEmpty(s) => null,
                string s when s.Equals("bool", StringComparison.OrdinalIgnoreCase) => false.ToString(),
                string s when s.Equals("choice", StringComparison.OrdinalIgnoreCase) => string.Empty,
                string s when s.Equals("int", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Equals("integer", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Equals("float", StringComparison.OrdinalIgnoreCase) => 0.ToString(),
                string s when s.Equals("hex", StringComparison.OrdinalIgnoreCase) => "0x0",
                // this includes text/string as well
                _ => null,
            };
        }

        private static string? ResolveChoice(string? literal, ITemplateParameter param)
        {
            if (TryResolveChoiceValue(literal, param, out string? match))
            {
                return match;
            }

            //TODO: here we should likely reevaluate once again after the conditions - but that is another possibility for infinite cycle
            if (literal == null && param.Precedence.PrecedenceDefinition != PrecedenceDefinition.Required)
            {
                return param.DefaultValue;
            }
            return literal == string.Empty ? string.Empty : null;
        }

        private static bool TryResolveChoiceValue(string? literal, ITemplateParameter param, out string? match)
        {
            if (literal == null || param.Choices == null)
            {
                match = null;
                return false;
            }

            string? partialMatch = null;
            bool multiplePartialMatches = false;

            foreach (string choiceValue in param.Choices.Keys)
            {
                if (string.Equals(choiceValue, literal, StringComparison.OrdinalIgnoreCase))
                {
                    // exact match is good, regardless of partial matches
                    match = choiceValue;
                    return true;
                }
                else if (choiceValue.StartsWith(literal, StringComparison.OrdinalIgnoreCase))
                {
                    if (partialMatch == null)
                    {
                        partialMatch = choiceValue;
                    }
                    else
                    {
                        // Multiple partial matches, keep searching for exact match, fail if we don't find one
                        // because we don't know which partial match we should select.
                        multiplePartialMatches = true;
                    }
                }
            }

            if (multiplePartialMatches)
            {
                match = null;
                return false;
            }

            match = partialMatch;
            return match != null;
        }

        private static bool TryResolveBooleanValue(string? literal, out bool parsed) => bool.TryParse(literal, out parsed);

        private static bool TryResolveNullValue(string? literal, out object? parsed)
        {
            parsed = null;
            if (string.Equals(literal, "null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static bool TryResolveFloatValue(string? literal, out double parsed) => ParserExtensions.DoubleTryParseCurrentOrInvariant(literal, out parsed);

        private static bool TryResolveIntegerValue(string? literal, out long parsed) => long.TryParse(literal, out parsed);

        private static bool TryResolveHexValue(string? literal, out long parsed)
        {
            parsed = default;
            if (literal == null)
            {
                return false;
            }
            if (literal.Length < 3)
            {
                return false;
            }

            if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                  && long.TryParse(literal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }
            return false;
        }
    }
}
