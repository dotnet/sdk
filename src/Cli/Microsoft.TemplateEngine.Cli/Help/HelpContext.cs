// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Help;

/// <summary>
/// Supports formatting command line help.
/// </summary>
/// <param name="helpBuilder">The current help builder.</param>
/// <param name="command">The command for which help is being formatted.</param>
/// <param name="output">A text writer to write output to.</param>
/// <param name="parseResult">The parse result.</param>
public class HelpContext(
    HelpBuilder helpBuilder,
    Command command,
    TextWriter output,
    ParseResult? parseResult = null)
{
    private static readonly Lazy<ParseResult> EmptyParseResult = new(() => new RootCommand().Parse(Array.Empty<string>()));

    /// <summary>
    /// The help builder for the current operation.
    /// </summary>
    public HelpBuilder HelpBuilder { get; } = helpBuilder ?? throw new ArgumentNullException(nameof(helpBuilder));

    /// <summary>
    /// The command for which help is being formatted.
    /// </summary>
    public Command Command { get; } = command ?? throw new ArgumentNullException(nameof(command));

    /// <summary>
    /// A text writer to write output to.
    /// </summary>
    public TextWriter Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public ParseResult ParseResult { get; } = parseResult ?? EmptyParseResult.Value;
}
