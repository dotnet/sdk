// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Rejects commands, and explicitly supplied options or arguments, that are not permitted in
/// admin mode. Only the presence of a symbol is checked; value rules belong to the command.
/// </summary>
internal static class AdminModeGate
{
    private const string RootCommandDisplayName = "dotnetup";

    /// <summary>
    /// Throws a <see cref="DotnetInstallException"/> when <paramref name="accessMode"/> is admin
    /// and the parsed command or one of its explicitly supplied symbols is not allowed by
    /// <paramref name="support"/>. Does nothing outside admin mode.
    /// </summary>
    public static void Enforce(ParseResult parseResult, AdminModeSupport support, EffectiveAccessMode accessMode)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(accessMode);

        if (!accessMode.IsAdmin)
        {
            return;
        }

        if (!support.IsSupported)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminModeCommandNotSupported,
                $"'{GetCommandPath(parseResult.CommandResult)}' is not available in admin mode. " +
                "In admin mode, only 'sdk install', 'runtime install', 'init', 'env set', 'env script', and 'self update' are available. " +
                "Run 'dotnetup init --mode <mode>' to leave admin mode.");
        }

        if (support.AllowsAnySymbol)
        {
            return;
        }

        foreach (var symbol in GetExplicitSymbols(parseResult.CommandResult))
        {
            if (!support.Allows(symbol))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.AdminModeSymbolNotSupported,
                    $"{DescribeSymbol(symbol)} is not available for '{GetCommandPath(parseResult.CommandResult)}' in admin mode.");
            }
        }
    }

    /// <summary>
    /// Returns the options and arguments the user supplied on the command line for
    /// <paramref name="commandResult"/> and its parent commands, so options inherited from a
    /// parent count as well. Defaulted (implicit) results and help/version options are skipped.
    /// </summary>
    internal static IEnumerable<Symbol> GetExplicitSymbols(CommandResult commandResult)
    {
        for (SymbolResult? current = commandResult; current is not null; current = current.Parent)
        {
            if (current is not CommandResult command)
            {
                continue;
            }

            foreach (var child in command.Children)
            {
                if (GetExplicitSymbol(child) is Symbol symbol)
                {
                    yield return symbol;
                }
            }
        }
    }

    private static Symbol? GetExplicitSymbol(SymbolResult result) => result switch
    {
        OptionResult { Option: HelpOption or VersionOption } => null,
        OptionResult { Implicit: false } optionResult => optionResult.Option,
        ArgumentResult { Implicit: false, Tokens.Count: > 0 } argumentResult => argumentResult.Argument,
        _ => null,
    };

    private static string DescribeSymbol(Symbol symbol) => symbol switch
    {
        Option option => $"The option '{option.Name}'",
        Argument argument => $"The argument '<{argument.Name}>'",
        _ => $"'{symbol.Name}'",
    };

    /// <summary>Builds the command path as the user typed it, for example "dotnetup sdk install".</summary>
    internal static string GetCommandPath(CommandResult commandResult)
    {
        var segments = new List<string>();
        for (SymbolResult? current = commandResult; current is not null; current = current.Parent)
        {
            if (current is CommandResult command)
            {
                segments.Add(command.Command is RootCommand
                    ? RootCommandDisplayName
                    : command.IdentifierToken.Value);
            }
        }

        segments.Reverse();
        return string.Join(' ', segments);
    }
}
