// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Describes whether a command can run in admin mode and which of its options and arguments are
/// permitted there. Commands are unsupported unless they opt in (default deny).
/// </summary>
internal sealed class AdminModeSupport
{
    private AdminModeSupport(bool isSupported, bool allowsAnySymbol, IReadOnlySet<Symbol> allowedSymbols)
    {
        IsSupported = isSupported;
        AllowsAnySymbol = allowsAnySymbol;
        AllowedSymbols = allowedSymbols;
    }

    /// <summary>The command cannot run in admin mode.</summary>
    public static AdminModeSupport Unsupported { get; } = new(false, false, CreateSet([]));

    /// <summary>The command runs in admin mode with any of its options and arguments.</summary>
    public static AdminModeSupport AnySymbol { get; } = new(true, true, CreateSet([]));

    /// <summary>The command runs in admin mode, but only with the given options and arguments.</summary>
    public static AdminModeSupport Allow(params Symbol[] symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        return new(true, false, CreateSet(symbols));
    }

    public bool IsSupported { get; }

    public bool AllowsAnySymbol { get; }

    /// <summary>The permitted options and arguments, compared by reference.</summary>
    public IReadOnlySet<Symbol> AllowedSymbols { get; }

    /// <summary>Returns true when <paramref name="symbol"/> may be used in admin mode.</summary>
    public bool Allows(Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return IsSupported && (AllowsAnySymbol || AllowedSymbols.Contains(symbol));
    }

    private static HashSet<Symbol> CreateSet(Symbol[] symbols)
    {
        var set = new HashSet<Symbol>(ReferenceEqualityComparer.Instance);
        foreach (var symbol in symbols)
        {
            ArgumentNullException.ThrowIfNull(symbol, nameof(symbols));
            set.Add(symbol);
        }

        return set;
    }
}
