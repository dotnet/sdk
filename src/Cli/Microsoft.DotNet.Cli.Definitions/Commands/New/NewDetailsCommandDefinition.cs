// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewDetailsCommandDefinition : Command
{
    public new const string Name = "details";

    // Option disabled until https://github.com/dotnet/templating/issues/6811 is solved
    //internal static Option<string> VersionOption = new("-version", "--version")
    //{
    //    Description = CommandDefinitionStrings.DetailsCommand_Option_Version,
    //    Arity = new ArgumentArity(1, 1)
    //};

    public readonly Argument<string> NameArgument = new("package-identifier")
    {
        Description = CommandDefinitionStrings.DetailsCommand_Argument_PackageIdentifier,
        Arity = new ArgumentArity(1, 1)
    };

    public readonly Option<bool> InteractiveOption = SharedOptionsFactory.CreateInteractiveOption();
    public readonly Option<string[]> AddSourceOption = SharedOptionsFactory.CreateAddSourceOption();

    public NewDetailsCommandDefinition()
        : base(Name, CommandDefinitionStrings.Command_Details_Description)
    {
        Arguments.Add(NameArgument);
        Options.Add(InteractiveOption);
        Options.Add(AddSourceOption);
    }
}
