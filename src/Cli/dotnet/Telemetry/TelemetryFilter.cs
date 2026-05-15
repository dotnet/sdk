// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.VSTest;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class TelemetryFilter(Func<string, string>? hash) : ITelemetryFilter
{
    private const string ExceptionEventName = "mainCatchException/exception";
    private readonly Func<string, string> _hash = hash ?? throw new ArgumentNullException(nameof(hash));

    public IEnumerable<TelemetryEntryFormat> Filter(ParseResult parseResult) =>
        Hash(FilterImpl(parseResult, globalJsonState: null));

    public IEnumerable<TelemetryEntryFormat> Filter(ParseResultWithGlobalJsonState parseData) =>
        Hash(FilterImpl(parseData.ParseResult, parseData.GlobalJsonState));

    public IEnumerable<TelemetryEntryFormat> Filter(InstallerSuccessReport report)
    {
        var reportProperties = new Dictionary<string, string?>
        {
            { "exeName", report.ExeName }
        };
        return Hash([new TelemetryEntryFormat("install/reportsuccess", reportProperties)]);
    }

    public IEnumerable<TelemetryEntryFormat> Filter(Exception exception)
    {
        var exceptionProperties = new Dictionary<string, string?>
        {
            { "exceptionType", exception.GetType().ToString() },
            { "detail", ExceptionToStringWithoutMessage(exception) }
        };
        return Hash([new TelemetryEntryFormat(ExceptionEventName, exceptionProperties)]);
    }

    private static IEnumerable<TelemetryEntryFormat> FilterImpl(ParseResult parseResult, string? globalJsonState)
    {
        var topLevelCommandName = parseResult.RootSubCommandResult();
        if (topLevelCommandName is null)
        {
            yield break;
        }

        Dictionary<string, string?> properties = new() { ["verb"] = topLevelCommandName };
        if (!string.IsNullOrEmpty(globalJsonState))
        {
            properties["globalJson"] = globalJsonState;
        }

        yield return new TelemetryEntryFormat("toplevelparser/command", properties);

        if (parseResult.IsDotnetBuiltInCommand() &&
            parseResult.SafelyGetValueForOption<VerbosityOptions>("--verbosity") is VerbosityOptions verbosity)
        {
            var verbosityProperties = new Dictionary<string, string?>()
            {
                { "verb", topLevelCommandName},
                { "verbosity", Enum.GetName(verbosity)}
            };
            yield return new TelemetryEntryFormat("sublevelparser/command", verbosityProperties);
        }

        if (topLevelCommandName == "package" &&
            parseResult.CommandResult.Command != null &&
            parseResult.CommandResult.Command.Name == "update")
        {
            var hasVulnerableOption = parseResult.HasOption("--vulnerable");
            var vulnerableProperties = new Dictionary<string, string?>()
            {
                { "verb", "package update" },
                { "vulnerable", hasVulnerableOption.ToString()}
            };
            yield return new TelemetryEntryFormat("sublevelparser/command", vulnerableProperties);
        }

        foreach (IParseResultLogRule rule in ParseResultLogRules)
        {
            foreach (TelemetryEntryFormat allowList in rule.AllowList(parseResult))
            {
                yield return allowList;
            }
        }
    }

    public IEnumerable<TelemetryEntryFormat> Hash(IEnumerable<TelemetryEntryFormat> entries) =>
        entries.Select(entry => entry.EventName == ExceptionEventName ? entry : entry.WithAppliedToPropertiesValue(_hash));

    private static List<IParseResultLogRule> ParseResultLogRules =>
    [
        new AllowListToSendFirstArgument(["new", "help"]),
        new AllowListToSendFirstAppliedOptions(["add", "remove", "list", "solution", "nuget"]),
        new TopLevelCommandNameAndOptionToLog
        (
            topLevelCommandName: ["build", "publish"],
            optionsToLog: [ CommonOptions.FrameworkOptionName, TargetPlatformOptions.RuntimeOptionName, CommonOptions.ConfigurationOptionName ]
        ),
        new TopLevelCommandNameAndOptionToLog
        (
            topLevelCommandName: ["run", "clean", "test"],
            optionsToLog: [CommonOptions.FrameworkOptionName, CommonOptions.ConfigurationOptionName]
        ),
        new TopLevelCommandNameAndOptionToLog
        (
            topLevelCommandName: ["pack"],
            optionsToLog: [CommonOptions.ConfigurationOptionName]
        ),
        new TopLevelCommandNameAndOptionToLog
        (
            topLevelCommandName: ["vstest"],
            optionsToLog: [VSTestCommandDefinition.TestPlatformOptionName, VSTestCommandDefinition.TestFrameworkOptionName, VSTestCommandDefinition.TestLoggerOptionName]
        ),
        new TopLevelCommandNameAndOptionToLog
        (
            topLevelCommandName: ["publish"],
            optionsToLog: [TargetPlatformOptions.RuntimeOptionName]
        ),
        new AllowListToSendVerbSecondVerbFirstArgument(["workload", "tool", "new"]),
    ];

    private static string ExceptionToStringWithoutMessage(Exception e)
    {
        const string AggregateException_ToString = "{0}{1}---> (Inner Exception #{2}) {3}{4}{5}";
        if (e is AggregateException aggregate)
        {
            string text = NonAggregateExceptionToStringWithoutMessage(aggregate);

            for (int i = 0; i < aggregate.InnerExceptions.Count; i++)
            {
                text = string.Format(CultureInfo.InvariantCulture,
                                     AggregateException_ToString,
                                     text,
                                     Environment.NewLine,
                                     i,
                                     ExceptionToStringWithoutMessage(aggregate.InnerExceptions[i]),
                                     "<---",
                                     Environment.NewLine);
            }

            return text;
        }
        else
        {
            return NonAggregateExceptionToStringWithoutMessage(e);
        }
    }

    private static string NonAggregateExceptionToStringWithoutMessage(Exception e)
    {
        string s;
        const string Exception_EndOfInnerExceptionStack = "--- End of inner exception stack trace ---";

        s = e.GetType().ToString();
        if (e.InnerException != null)
        {
            s = s + " ---> " + ExceptionToStringWithoutMessage(e.InnerException) + Environment.NewLine +
            "   " + Exception_EndOfInnerExceptionStack;
        }

        var stackTrace = e.StackTrace;
        if (stackTrace != null)
        {
            s += Environment.NewLine + stackTrace;
        }
        return s;
    }
}
