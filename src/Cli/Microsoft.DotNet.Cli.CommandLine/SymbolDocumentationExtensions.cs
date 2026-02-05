// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// Extension methods for adding documentation links to commands, options and arguments.
/// </summary>
public static class SymbolDocumentationExtensions
{
    private static readonly Dictionary<Symbol, string> s_documentationLinks = new();
    private static readonly Lock s_lock = new();

    extension(Symbol symbol)
    {
        /// <summary>
        /// Gets or sets the documentation link for this command, option or argument.
        /// This link is intended to be shown in help or error messages to point users to more information.
        /// It is not used by the command line parser itself.
        /// </summary>
        public string? DocsLink
        {
            get => s_documentationLinks.TryGetValue(symbol, out var link) ? link : null;
            set
            {
                lock (s_lock)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        s_documentationLinks.Remove(symbol);
                    }
                    else
                    {
                        s_documentationLinks[symbol] = value;
                    }
                }
            }
        }
    }

}
