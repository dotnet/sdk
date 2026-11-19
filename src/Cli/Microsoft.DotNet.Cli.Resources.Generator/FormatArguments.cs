// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Describes format placeholders parsed from a resource value.
/// </summary>
internal sealed class FormatArguments
{
    private FormatArguments(bool usesNames, ImmutableArray<string> arguments, string? error = null)
    {
        UsesNames = usesNames;
        Arguments = arguments;
        Error = error;
    }

    /// <summary>
    ///  Gets a value indicating whether the placeholders use names instead of numeric indexes.
    /// </summary>
    internal bool UsesNames { get; }

    /// <summary>
    ///  Gets the placeholder names or numeric indexes in generated parameter order.
    /// </summary>
    internal ImmutableArray<string> Arguments { get; }

    /// <summary>
    ///  Gets the validation error, or <see langword="null"/> when the placeholders are valid.
    /// </summary>
    internal string? Error { get; }

    /// <summary>
    ///  Gets a value indicating whether the resource value contains format arguments.
    /// </summary>
    internal bool HasArguments => !Arguments.IsDefaultOrEmpty;

    /// <summary>
    ///  Parses format placeholders from a resource value.
    /// </summary>
    /// <param name="value">The resource value to parse.</param>
    /// <returns>The parsed format arguments and any validation error.</returns>
    internal static FormatArguments Parse(string value)
    {
        ImmutableArray<string> namedArguments = FindNamedArguments(value, out bool tooManyNamedArguments);
        if (tooManyNamedArguments)
        {
            return new(
                usesNames: true,
                arguments: [],
                error: $"uses more than {ResxParser.MaxFormatArguments} named format arguments");
        }

        FormatArguments numericArguments = ParseNumericArguments(value);
        if (numericArguments.Error is not null || namedArguments.IsDefaultOrEmpty)
        {
            return numericArguments;
        }

        return numericArguments.HasArguments
            ? new(usesNames: false, arguments: [])
            : new(usesNames: true, namedArguments);
    }

    private static ImmutableArray<string> FindNamedArguments(string value, out bool tooManyArguments)
    {
        List<string> arguments = [];
        HashSet<string> distinct = [with(StringComparer.Ordinal)];
        tooManyArguments = false;

        for (int index = 0; index < value.Length - 2; index++)
        {
            if (value[index] != '{')
            {
                continue;
            }

            if (value[index + 1] == '{')
            {
                index++;
                continue;
            }

            int start = index + 1;
            if (!char.IsLetter(value[start]))
            {
                continue;
            }

            int end = start + 1;
            while (end < value.Length && CSharpIdentifier.IsIdentifierPartCharacter(value[end]))
            {
                end++;
            }

            if (end >= value.Length || value[end] != '}')
            {
                continue;
            }

            string argument = value.Substring(start, end - start);
            if (distinct.Add(argument))
            {
                if (arguments.Count == ResxParser.MaxFormatArguments)
                {
                    tooManyArguments = true;
                    return [];
                }

                arguments.Add(argument);
            }

            index = end;
        }

        return [.. arguments];
    }

    private static FormatArguments ParseNumericArguments(string value)
    {
        int maximumIndex = -1;

        for (int index = 0; index < value.Length - 2; index++)
        {
            if (value[index] != '{')
            {
                continue;
            }

            if (value[index + 1] == '{')
            {
                index++;
                continue;
            }

            if (!char.IsDigit(value[index + 1]))
            {
                continue;
            }

            int end = index + 1;
            int argumentIndex = 0;

            while (end < value.Length && char.IsDigit(value[end]))
            {
                int digit = value[end] - '0';
                if (argumentIndex > (ResxParser.MaxFormatArguments - 1 - digit) / 10)
                {
                    return new(
                        usesNames: false,
                        arguments: [],
                        error: $"uses a numeric format argument greater than "
                            + $"{ResxParser.MaxFormatArguments - 1}");
                }

                argumentIndex = (argumentIndex * 10) + digit;
                end++;
            }

            if (end >= value.Length || value[end] != '}')
            {
                continue;
            }

            maximumIndex = Math.Max(maximumIndex, argumentIndex);
            index = end;
        }

        if (maximumIndex < 0)
        {
            return new(usesNames: false, arguments: []);
        }

        ImmutableArray<string>.Builder arguments = ImmutableArray.CreateBuilder<string>(maximumIndex + 1);
        for (int index = 0; index <= maximumIndex; index++)
        {
            arguments.Add(index.ToString());
        }

        return new(usesNames: false, arguments.ToImmutable());
    }
}