// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli;

public static class OptionExtensions
{
    public static Option<T> AsHidden<T>(this Option<T> o)
    {
        o.Hidden = true;
        return o;
    }

    public static Option<T> WithDescription<T>(this Option<T> o, string description)
    {
        o.Description = description;
        return o;
    }

    public static Option<T> DisableAllowMultipleArgumentsPerToken<T>(this Option<T> o)
    {
        o.AllowMultipleArgumentsPerToken = false;
        return o;
    }


    /// <summary>
    /// Set up an option to be forwarded as an output path to MSBuild
    /// </summary>
    /// <param name="option">The command line option</param>
    /// <param name="outputPropertyName">The property name for the output path (such as OutputPath or PublishDir)</param>
    /// <param name="surroundWithDoubleQuotes">Whether the path should be surrounded with double quotes.  This may not be necessary but preserves the previous behavior of "dotnet test"</param>
    /// <returns>The option</returns>
    public static Option<string> ForwardAsOutputPath(this Option<string> option, string outputPropertyName, bool surroundWithDoubleQuotes = false)
    {
        return option.SetForwardingFunction((string? o) =>
        {
            if (o is null)
            {
                return [];
            }
            string argVal = CommandDirectoryContext.GetFullPath(o);
            if (!Path.EndsInDirectorySeparator(argVal))
            {
                argVal += Path.DirectorySeparatorChar;
            }
            if (surroundWithDoubleQuotes)
            {
                //  Not sure if this is necessary, but this is what "dotnet test" previously did and so we are
                //  preserving the behavior here after refactoring
                argVal = MSBuildPropertyParser.SurroundWithDoubleQuotes(argVal);
            }
            return [
                $"--property:{outputPropertyName}={argVal}",
                "--property:_CommandLineDefinedOutputPath=true"
            ];
        });
    }

    /// <summary>
    /// Set up an option to be forwarded as an MSBuild property
    /// This will parse the values as MSBuild properties and forward them in the format <c>optionName:key=value</c>.
    /// For example, if the option is named "--property", and the values are "A=B" and "C=D", it will be forwarded as:
    /// <c>--property:A=B --property:C=D</c>.
    /// This is useful for options that can take multiple key-value pairs, such as --property.
    /// </summary>
    public static Option<ReadOnlyDictionary<string, string>?> ForwardAsMSBuildProperty(this Option<ReadOnlyDictionary<string, string>?> option) =>
        option.SetForwardingFunction(propertyDict => ForwardedMSBuildPropertyValues(propertyDict, option.Name));

    private static IEnumerable<string> ForwardedMSBuildPropertyValues(ReadOnlyDictionary<string, string>? properties, string optionName)
    {
        if (properties is null || properties.Count == 0)
        {
            return Enumerable.Empty<string>();
        }

        return properties.Select(kv => $"{optionName}:{kv.Key}={kv.Value}");
    }
}
