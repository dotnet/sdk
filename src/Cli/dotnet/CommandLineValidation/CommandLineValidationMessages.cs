// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class SymbolResultExtensions
    {
        internal static Token Token(this SymbolResult symbolResult)
        {
            return symbolResult switch
            {
                CommandResult commandResult => commandResult.IdentifierToken,
                OptionResult optionResult => optionResult.IdentifierToken is null ?
                                             new Token($"--{optionResult.Option.Name}", TokenType.Option, optionResult.Option)
                                             : optionResult.IdentifierToken,
                ArgumentResult argResult => new Token(argResult.GetValueOrDefault<string>(), TokenType.Argument, argResult.Argument),
                _ => default
            };
        }
    }
}
