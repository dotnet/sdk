// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.List;

internal sealed class ToolListCommandDefinition : Command
{
    public readonly Argument<string> PackageIdArgument = new("packageId")
    {
        HelpName = CommandDefinitionStrings.ToolListPackageIdArgumentName,
        Description = CommandDefinitionStrings.ToolListPackageIdArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne,
    };

    public readonly ToolLocationOptions LocationOptions = new(
        globalOptionDescription: CommandDefinitionStrings.ToolListGlobalOptionDescription,
        localOptionDescription: CommandDefinitionStrings.ToolListLocalOptionDescription,
        toolPathOptionDescription: CommandDefinitionStrings.ToolListToolPathOptionDescription);

    public readonly Option<ToolListOutputFormat> ToolListFormatOption = new("--format")
    {
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => ToolListOutputFormat.table,
        Description = CommandDefinitionStrings.ToolListFormatOptionDescription
    };

    public ToolListCommandDefinition()
        : base("list", CommandDefinitionStrings.ToolListCommandDescription)
    {
        Arguments.Add(PackageIdArgument);
        LocationOptions.AddTo(Options);
        Options.Add(ToolListFormatOption);
    }
}
