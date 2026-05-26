// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal sealed class ReferenceListCommandDefinition : ListReferenceCommandDefinitionBase
{
    public new const string Name = "list";

    public readonly Option<string?> FileOption = ReferenceCommandDefinition.CreateFileOption();

    public ReferenceListCommandDefinition()
        : base(Name)
    {
        Options.Add(FileOption);
    }

    public ReferenceCommandDefinition Parent => (ReferenceCommandDefinition)Parents.Single();

    internal override string? GetFileOrDirectory(ParseResult parseResult)
    {
        if (parseResult.HasOption(FileOption))
        {
            return parseResult.GetValue(FileOption);
        }

        return parseResult.GetValue(Parent.ProjectOption);
    }

    internal override AppKinds GetAllowedAppKinds(ParseResult parseResult)
        => parseResult.HasOption(FileOption) ? AppKinds.FileBased : AppKinds.ProjectBased;

    internal override (string? FileOptionName, string? ProjectOptionName) GetConflictingPathOptions(ParseResult parseResult)
        => parseResult.HasOption(FileOption) && parseResult.HasOption(Parent.ProjectOption)
            ? (FileOption.Name, Parent.ProjectOption.Name)
            : (null, null);
}
