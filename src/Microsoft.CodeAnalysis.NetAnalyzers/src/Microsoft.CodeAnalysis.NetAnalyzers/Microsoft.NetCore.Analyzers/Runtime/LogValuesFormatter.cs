// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Formatter to convert the named format items like {NamedformatItem} to string format.
    /// </summary>
    internal class LogValuesFormatter
    {
        private const string NullValue = "(null)";
        private static readonly object[] EmptyArray = Array.Empty<object>();
        private static readonly char[] FormatDelimiters = { ',', ':' };
        private readonly string _format;

        public LogValuesFormatter(string format)
        {
            OriginalFormat = format;

            var sb = new StringBuilder();
            var scanIndex = 0;
            var endIndex = format.Length;

            while (scanIndex < endIndex)
            {
                var openBraceIndex = FindBraceIndex(format, '{', scanIndex, endIndex);
                var closeBraceIndex = FindBraceIndex(format, '}', openBraceIndex, endIndex);

                if (closeBraceIndex == endIndex)
                {
                    sb.Append(format, scanIndex, endIndex - scanIndex);
                    scanIndex = endIndex;
                }
                else
                {
                    // Format item syntax : { index[,alignment][ :formatString] }.
                    var formatDelimiterIndex = FindIndexOfAny(format, FormatDelimiters, openBraceIndex, closeBraceIndex);

                    sb.Append(format, scanIndex, openBraceIndex - scanIndex + 1);
                    sb.Append(ValueNames.Count.ToString(CultureInfo.InvariantCulture));
                    ValueNames.Add(format.Substring(openBraceIndex + 1, formatDelimiterIndex - openBraceIndex - 1));
                    sb.Append(format, formatDelimiterIndex, closeBraceIndex - formatDelimiterIndex + 1);

                    scanIndex = closeBraceIndex + 1;
                }
            }

            _format = sb.ToString();
        }

        public string OriginalFormat { get; private set; }
        public List<string> ValueNames { get; } = new List<string>();

        private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
        {
            // Example: {{prefix{{{Argument}}}suffix}}.
            var braceIndex = endIndex;
            var scanIndex = startIndex;
            var braceOccurrenceCount = 0;

            while (scanIndex < endIndex)
            {
                if (braceOccurrenceCount > 0 && format[scanIndex] != brace)
                {
                    if (braceOccurrenceCount % 2 == 0)
                    {
                        // Even number of '{' or '}' found. Proceed search with next occurrence of '{' or '}'.
                        braceOccurrenceCount = 0;
                        braceIndex = endIndex;
                    }
                    else
                    {
                        // An unescaped '{' or '}' found.
                        break;
                    }
                }
                else if (format[scanIndex] == brace)
                {
                    if (brace == '}')
                    {
                        if (braceOccurrenceCount == 0)
                        {
                            // For '}' pick the first occurrence.
                            braceIndex = scanIndex;
                        }
                    }
                    else
                    {
                        // For '{' pick the last occurrence.
                        braceIndex = scanIndex;
                    }

                    braceOccurrenceCount++;
                }

                scanIndex++;
            }

            return braceIndex;
        }

        private static int FindIndexOfAny(string format, char[] chars, int startIndex, int endIndex)
        {
            var findIndex = format.IndexOfAny(chars, startIndex, endIndex - startIndex);
            return findIndex == -1 ? endIndex : findIndex;
        }

        public string Format(object[] values)
        {
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = FormatArgument(values[i]);
                }
            }

            return string.Format(CultureInfo.InvariantCulture, _format, values ?? EmptyArray);
        }

        internal string Format()
        {
            return _format;
        }

        internal string Format(object arg0)
        {
            return string.Format(CultureInfo.InvariantCulture, _format, FormatArgument(arg0));
        }

        internal string Format(object arg0, object arg1)
        {
            return string.Format(CultureInfo.InvariantCulture, _format, FormatArgument(arg0), FormatArgument(arg1));
        }

        internal string Format(object arg0, object arg1, object arg2)
        {
            return string.Format(CultureInfo.InvariantCulture, _format, FormatArgument(arg0), FormatArgument(arg1), FormatArgument(arg2));
        }

        public KeyValuePair<string, object> GetValue(object[] values, int index)
        {
            if (index < 0 || index > ValueNames.Count)
            {
#pragma warning disable CA2201 // Do not raise reserved exception types
                throw new IndexOutOfRangeException(nameof(index));
#pragma warning restore CA2201 // Do not raise reserved exception types
            }

            if (ValueNames.Count > index)
            {
                return new KeyValuePair<string, object>(ValueNames[index], values[index]);
            }

            return new KeyValuePair<string, object>("{OriginalFormat}", OriginalFormat);
        }

        public IEnumerable<KeyValuePair<string, object>> GetValues(object[] values)
        {
            var valueArray = new KeyValuePair<string, object>[values.Length + 1];
            for (var index = 0; index != ValueNames.Count; ++index)
            {
                valueArray[index] = new KeyValuePair<string, object>(ValueNames[index], values[index]);
            }

            valueArray[^1] = new KeyValuePair<string, object>("{OriginalFormat}", OriginalFormat);
            return valueArray;
        }

        private static object FormatArgument(object value)
        {
            if (value == null)
            {
                return NullValue;
            }

            // since 'string' implements IEnumerable, special case it
            if (value is string)
            {
                return value;
            }

            // if the value implements IEnumerable, build a comma separated string.
            if (value is IEnumerable enumerable)
            {
                return string.Join(", ", enumerable.Cast<object>().Select(o => o ?? NullValue));
            }

            return value;
        }
    }
}