// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewCreateCommandDefinition : Command
{
    public new const string Name = "create";

    public readonly Argument<string> ShortNameArgument = CreateShortNameArgument();

    public readonly Argument<string[]> RemainingArguments = new("template-args")
    {
        Description = CommandDefinitionStrings.Command_Instantiate_Argument_TemplateOptions,
        Arity = new ArgumentArity(0, 999)
    };

    public readonly InstantiateOptions InstantiateOptions = new();

    public NewCreateCommandDefinition()
        : base(Name, CommandDefinitionStrings.Command_Instantiate_Description)
    {
        Arguments.Add(ShortNameArgument);
        Arguments.Add(RemainingArguments);

        Options.AddRange(InstantiateOptions.AllOptions);

        foreach (var option in InstantiateOptions.AllOptions)
        {
            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(option.Name));
        }

        this.AddNoLegacyUsageValidators();
    }

    public static Argument<string> CreateShortNameArgument() => new("template-short-name")
    {
        Description = CommandDefinitionStrings.Command_Instantiate_Argument_ShortName,
        Arity = new ArgumentArity(0, 1)
    };
}
