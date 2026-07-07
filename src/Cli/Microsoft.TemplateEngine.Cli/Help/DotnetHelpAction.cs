// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Microsoft.TemplateEngine.Cli.Help;

/// <summary>
/// Provides command line help.
/// </summary>
public sealed class DotnetHelpAction : SynchronousCommandLineAction
{
    private HelpBuilder? _builder;

    /// <summary>
    /// Specifies an <see cref="Builder"/> to be used to format help output when help is requested.
    /// </summary>
    public HelpBuilder Builder
    {
        get => _builder ??= new HelpBuilder(Console.IsOutputRedirected ? int.MaxValue : Console.WindowWidth);
        set => _builder = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    public override bool ClearsParseErrors => true;

    /// <inheritdoc />
    public override int Invoke(ParseResult parseResult)
    {
        var output = parseResult.InvocationConfiguration.Output;

        var helpContext = new HelpContext(
            Builder,
            parseResult.CommandResult.Command,
            output,
            parseResult);

        Builder.Write(helpContext);

        return 0;
    }
}
