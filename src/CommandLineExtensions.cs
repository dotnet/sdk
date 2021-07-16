// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class CommandLineExtensions
    {
        internal static OptionResult? GetOptionResult(this ParseResult result, string alias)
        {
            return GetOptionResult(result.CommandResult, alias);
        }

        internal static ArgumentResult? GetArgumentResult(this ParseResult result, string alias)
        {
            return GetArgumentResult(result.CommandResult, alias);
        }

        internal static OptionResult? GetOptionResult(this CommandResult result, string alias)
        {
            return result.Children.GetByAlias(alias) as OptionResult;
        }

        internal static ArgumentResult? GetArgumentResult(this CommandResult result, string alias)
        {
            return result.Children.GetByAlias(alias) as ArgumentResult;
        }

        [return: MaybeNull]
        internal static T GetValueForArgument<T>(this ParseResult result, string alias)
        {
            return GetValueForArgument<T>(result.CommandResult, alias);
        }

        [return: MaybeNull]
        internal static T GetValueForOption<T>(this ParseResult result, string alias)
        {
            return GetValueForOption<T>(result.CommandResult, alias);
        }

        [return: MaybeNull]
        internal static T GetValueForArgument<T>(this CommandResult result, string alias)
        {
            if (result.GetArgumentResult(alias) is ArgumentResult argument &&
                argument.GetValueOrDefault<T>() is { } t)
            {
                return t;
            }

            return default;
        }

        [return: MaybeNull]
        internal static T GetValueForOption<T>(this CommandResult result, string alias)
        {
            if (result.GetOptionResult(alias) is OptionResult option &&
                option.GetValueOrDefault<T>() is { } t)
            {
                return t;
            }

            return default;
        }

        internal static bool WasOptionUsed(this ParseResult result, params string[] aliases)
        {
            return result.Tokens
                .Where(token => token.Type == TokenType.Option)
                .Any(token => aliases.Contains(token.Value));
        }
    }
}
