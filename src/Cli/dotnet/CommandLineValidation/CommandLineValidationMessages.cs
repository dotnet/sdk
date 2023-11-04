// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using System.Reflection;

namespace Microsoft.DotNet.Cli
{
    internal static class SymbolResultExtensions
    {
        internal static CliToken Token(this SymbolResult symbolResult)
        {
            return symbolResult switch
            {
                CommandResult commandResult => commandResult.IdentifierToken,
                OptionResult optionResult => optionResult.IdentifierToken is null ?
                                             new CliToken($"--{optionResult.Option.Name}", CliTokenType.Option, optionResult.Option)
                                             : optionResult.IdentifierToken,
                ArgumentResult argResult => new CliToken(GetArgResultValue(argResult), CliTokenType.Argument, argResult.Argument),
                DirectiveResult dirResult => dirResult.Token,
                _ => default
            };

            // TODO: WE SHOULD NOT NEED TO DO THIS LONGTERM! There seems to be no mechanism currently to get at the raw value of an ArgumentResult without knowing the type you're expecting.
            static string GetArgResultValue(ArgumentResult argResult)
            {
                var methodInfo = argResult.GetType().GetMethod("GetArgumentConversionResult", BindingFlags.Instance | BindingFlags.NonPublic);
                var conversionResult = methodInfo.Invoke(argResult, null);
                var value = conversionResult.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(conversionResult);
                return value.ToString();
            }
        }
    }
}
