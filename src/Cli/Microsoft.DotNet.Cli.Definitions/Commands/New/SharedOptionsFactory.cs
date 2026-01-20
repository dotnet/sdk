// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.Commands.New;

public static class SharedOptionsFactory
{
    public const string InteractiveOptionName = "--interactive";

    public static Option<bool> CreateInteractiveOption()
    {
        return new Option<bool>(InteractiveOptionName)
        {
            Arity = new ArgumentArity(0, 1),
            Description = CommandDefinitionStrings.Option_Interactive
        };
    }

    public const string AddSourceOptionName = "--add-source";

    public static Option<string[]> CreateAddSourceOption()
    {
        return new(AddSourceOptionName, "--nuget-source")
        {
            Arity = new ArgumentArity(1, 99),
            Description = CommandDefinitionStrings.Option_AddSource,
            AllowMultipleArgumentsPerToken = true,
            HelpName = "nuget-source"
        };
    }

    public const string ForceOptionName = "--force";

    public static Option<bool> CreateForceOption()
    {
        return new(ForceOptionName)
        {
            Arity = new ArgumentArity(0, 1),
            Description = CommandDefinitionStrings.TemplateCommand_Option_Force,
        };
    }

    public const string AuthorOptionName = "--author";

    public static Option<string> CreateAuthorOption()
    {
        return new(AuthorOptionName)
        {
            Arity = new ArgumentArity(1, 1),
            Description = CommandDefinitionStrings.Option_AuthorFilter
        };
    }

    public const string BaselineOptionName = "--baseline";

    public static Option<string> CreateBaselineOption()
    {
        return new(BaselineOptionName)
        {
            Arity = new ArgumentArity(1, 1),
            Description = CommandDefinitionStrings.Option_BaselineFilter,
            Hidden = true
        };
    }

    public const string LanguageOptionName = "--language";

    public static Option<string> CreateLanguageOption()
    {
        return new(LanguageOptionName, "-lang")
        {
            Arity = new ArgumentArity(1, 1),
            Description = CommandDefinitionStrings.Option_LanguageFilter
        };
    }

    public const string TypeOptionName = "--type";

    public static Option<string> CreateTypeOption()
    {
        return new(TypeOptionName)
        {
            Arity = new ArgumentArity(1, 1),
            Description = CommandDefinitionStrings.Option_TypeFilter
        };
    }

    public const string TagOptionName = "--tag";

    public static Option<string> CreateTagOption()
    {
        return new(TagOptionName)
        {
            Arity = new ArgumentArity(1, 1),
            Description = CommandDefinitionStrings.Option_TagFilter
        };
    }

    public const string PackageOptionName = "--package";

    public static Option<string> CreatePackageOption()
    {
        return new(PackageOptionName)
        {
            Arity = new ArgumentArity(1, 1),
            Description = CommandDefinitionStrings.Option_PackageFilter
        };
    }

    public static Option<FileInfo> CreateOutputOption() => new("--output", "-o")
    {
        Description = CommandDefinitionStrings.Option_Output,
        Required = false,
        Arity = new ArgumentArity(1, 1)
    };

    public const string ColumnsAllOptionName = "--columns-all";

    internal static Option<bool> CreateColumnsAllOption()
    {
        return new(ColumnsAllOptionName)
        {
            Arity = ArgumentArity.Zero,
            Description = CommandDefinitionStrings.Option_ColumnsAll
        };
    }

    public const string ColumnsOptionName = "--columns";

    public static Option<string[]> CreateColumnsOption()
    {
        Option<string[]> option = new(ColumnsOptionName)
        {
            Arity = new ArgumentArity(1, 4),
            Description = CommandDefinitionStrings.Option_Columns,
            AllowMultipleArgumentsPerToken = true,
            CustomParser = ParseCommaSeparatedValues
        };
        option.AcceptOnlyFromAmong(
            TabularOutputSettingsColumnNames.Author,
            TabularOutputSettingsColumnNames.Language,
            TabularOutputSettingsColumnNames.Type,
            TabularOutputSettingsColumnNames.Tags);
        return option;
    }

    internal static string[] ParseCommaSeparatedValues(ArgumentResult result)
    {
        List<string> values = new();
        foreach (string value in result.Tokens.Select(t => t.Value))
        {
            values.AddRange(value.Split(",", StringSplitOptions.TrimEntries).Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        return values.ToArray();
    }

    public const string NameOptionName = "--name";

    public static Option<string> CreateNameOption() => new(NameOptionName, "-n")
    {
        Description = CommandDefinitionStrings.TemplateCommand_Option_Name,
        Arity = new ArgumentArity(1, 1)
    };

    public const string DryRunOptionName = "--dry-run";

    public static Option<bool> CreateDryRunOption() => new(DryRunOptionName)
    {
        Description = CommandDefinitionStrings.TemplateCommand_Option_DryRun,
        Arity = new ArgumentArity(0, 1)
    };

    public const string NoUpdateCheckOptionName = "--no-update-check";

    public static Option<bool> CreateNoUpdateCheckOption() => new(NoUpdateCheckOptionName)
    {
        Description = CommandDefinitionStrings.TemplateCommand_Option_NoUpdateCheck,
        Arity = new ArgumentArity(0, 1)
    };

    public const string ProjectOptionName = "--project";

    public static Option<FileInfo> CreateProjectOption() => new Option<FileInfo>(ProjectOptionName)
    {
        Description = CommandDefinitionStrings.Option_ProjectPath
    }.AcceptExistingOnly();
}
