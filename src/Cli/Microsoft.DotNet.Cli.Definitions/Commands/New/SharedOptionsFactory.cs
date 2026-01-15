// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptionsFactory
    {
        public static Option AsHidden(this Option o)
        {
            o.Hidden = true;
            return o;
        }

        public static Option<T> AsHidden<T>(this Option<T> o)
        {
            o.Hidden = true;
            return o;
        }

        public static Option<T> WithDescription<T>(this Option<T> o, string description)
        {
            o.Description = description;
            return o;
        }

        public static Option<T> DisableAllowMultipleArgumentsPerToken<T>(this Option<T> o)
        {
            o.AllowMultipleArgumentsPerToken = false;
            return o;
        }

        public static Option<bool> CreateInteractiveOption()
        {
            return new Option<bool>("--interactive")
            {
                Arity = new ArgumentArity(0, 1),
                Description = CommandDefinitionStrings.Option_Interactive
            };
        }

        public static Option<string[]> CreateAddSourceOption()
        {
            return new("--add-source", "--nuget-source")
            {
                Arity = new ArgumentArity(1, 99),
                Description = CommandDefinitionStrings.Option_AddSource,
                AllowMultipleArgumentsPerToken = true,
                HelpName = "nuget-source"
            };
        }

        public static Option<bool> CreateForceOption()
        {
            return new("--force")
            {
                Arity = new ArgumentArity(0, 1),
                Description = CommandDefinitionStrings.TemplateCommand_Option_Force,
            };
        }

        public static Option<string> CreateAuthorOption()
        {
            return new("--author")
            {
                Arity = new ArgumentArity(1, 1),
                Description = CommandDefinitionStrings.Option_AuthorFilter
            };
        }

        public static Option<string> CreateBaselineOption()
        {
            return new("--baseline")
            {
                Arity = new ArgumentArity(1, 1),
                Description = CommandDefinitionStrings.Option_BaselineFilter,
                Hidden = true
            };
        }

        public static Option<string> CreateLanguageOption()
        {
            return new("--language", "-lang")
            {
                Arity = new ArgumentArity(1, 1),
                Description = CommandDefinitionStrings.Option_LanguageFilter
            };
        }

        public static Option<string> CreateTypeOption()
        {
            return new("--type")
            {
                Arity = new ArgumentArity(1, 1),
                Description = CommandDefinitionStrings.Option_TypeFilter
            };
        }

        public static Option<string> CreateTagOption()
        {
            return new("--tag")
            {
                Arity = new ArgumentArity(1, 1),
                Description = CommandDefinitionStrings.Option_TagFilter
            };
        }

        public static Option<string> CreatePackageOption()
        {
            return new("--package")
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

        internal static Option<bool> CreateColumnsAllOption()
        {
            return new("--columns-all")
            {
                Arity = ArgumentArity.Zero,
                Description = CommandDefinitionStrings.Option_ColumnsAll
            };
        }

        public static Option<string[]> CreateColumnsOption()
        {
            Option<string[]> option = new("--columns")
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
    }
}
