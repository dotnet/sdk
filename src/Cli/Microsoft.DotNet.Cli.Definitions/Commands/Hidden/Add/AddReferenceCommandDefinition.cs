// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;

internal sealed class AddReferenceCommandDefinition : ReferenceAddCommandDefinitionBase
{
    public new const string Name = "reference";

    public readonly Option<string?> ProjectOption = ReferenceCommandDefinition.CreateProjectOption();
    public readonly Option<string?> FileOption = ReferenceCommandDefinition.CreateFileOption();

    public AddReferenceCommandDefinition()
        : base(Name)
    {
        Options.Add(ProjectOption);
        Options.Add(FileOption);
    }

    public override string? GetFileOrDirectory(ParseResult parseResult)
    {
        if (parseResult.HasOption(FileOption))
        {
            return parseResult.GetValue(FileOption);
        }

        if (parseResult.HasOption(ProjectOption))
        {
            return parseResult.GetValue(ProjectOption);
        }

        return parseResult.GetValue(Parent.ProjectOrFileArgument);
    }

    public override AppKinds GetAllowedAppKinds(ParseResult parseResult)
        => parseResult.HasOption(FileOption)
            ? AppKinds.FileBased
            : parseResult.HasOption(ProjectOption)
                ? AppKinds.ProjectBased
                : AppKinds.Any;

    public override (string? FileOptionName, string? ProjectOptionName) GetConflictingPathOptions(ParseResult parseResult)
        => parseResult.HasOption(FileOption) && parseResult.HasOption(ProjectOption)
            ? (FileOption.Name, ProjectOption.Name)
            : (null, null);

    public AddCommandDefinition Parent => (AddCommandDefinition)Parents.Single();
}
