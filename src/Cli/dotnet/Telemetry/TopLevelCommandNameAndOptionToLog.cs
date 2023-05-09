// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TopLevelCommandNameAndOptionToLog : IParseResultLogRule
    {
        public TopLevelCommandNameAndOptionToLog(
            HashSet<string> topLevelCommandName,
            HashSet<Option> optionsToLog)
        {
            _topLevelCommandName = topLevelCommandName;
            _optionsToLog = optionsToLog;
        }

        private HashSet<string> _topLevelCommandName { get; }
        private HashSet<Option> _optionsToLog { get; }

        public List<ApplicationInsightsEntryFormat> AllowList(ParseResult parseResult, Dictionary<string, double> measurements = null)
        {
            var topLevelCommandName = parseResult.RootSubCommandResult();
            var result = new List<ApplicationInsightsEntryFormat>();
            foreach (var option in _optionsToLog)
            {
                if (_topLevelCommandName.Contains(topLevelCommandName)
                    && parseResult.SafelyGetValueForOption(option) is string optionValue)
                {
                    result.Add(new ApplicationInsightsEntryFormat(
                        "sublevelparser/command",
                        new Dictionary<string, string>
                        {
                            { "verb", topLevelCommandName},
                            { option.Name, Stringify(parseResult.GetValue(option)) }
                        },
                        measurements));
                }
            }
            return result;
        }

        /// <summary>
        /// We're dealing with untyped payloads here, so we need to handle arrays vs non-array values
        /// </summary>
        private static string Stringify(object value)
        {
            if (value is null)
            {
                return null;
            }
            if (value is IEnumerable<string> enumerable)
            {
                return string.Join(";", enumerable);
            }
            if (value is IEnumerable<object> enumerableOfObjects)
            {
                return string.Join(";", enumerableOfObjects);
            }
            if (value is object[] arr)
            {
                return string.Join(";", arr);
            }
            return value.ToString();
        }
    }
}
