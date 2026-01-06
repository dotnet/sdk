// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.List;

internal sealed class ToolListCommandDefinition : Command
{
    public readonly Argument<string> PackageIdArgument = new("packageId")
    {
        HelpName = CliCommandStrings.ToolListPackageIdArgumentName,
        Description = CliCommandStrings.ToolListPackageIdArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne,
    };

    public readonly ToolLocationOptions LocationOptions = new(
        globalOptionDescription: CliCommandStrings.ToolListGlobalOptionDescription,
        localOptionDescription: CliCommandStrings.ToolListLocalOptionDescription,
        toolPathOptionDescription: CliCommandStrings.ToolListToolPathOptionDescription);

    public readonly Option<ToolListOutputFormat> ToolListFormatOption = new("--format")
    {
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => ToolListOutputFormat.table,
        Description = CliCommandStrings.ToolListFormatOptionDescription
    };

    public ToolListCommandDefinition()
        : base("list", CliCommandStrings.ToolListCommandDescription)
    {
        Arguments.Add(PackageIdArgument);
        LocationOptions.AddTo(Options);
        Options.Add(ToolListFormatOption);
    }
}
