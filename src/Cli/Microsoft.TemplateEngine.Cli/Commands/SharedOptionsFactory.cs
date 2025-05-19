﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.TabularOutput;

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

        internal static Option<bool> CreateInteractiveOption()
        {
            return new Option<bool>("--interactive")
            {
                Arity = new ArgumentArity(0, 1),
                Description = SymbolStrings.Option_Interactive
            };
        }

        internal static Option<string[]> CreateAddSourceOption()
        {
            return new("--add-source", "--nuget-source")
            {
                Arity = new ArgumentArity(1, 99),
                Description = SymbolStrings.Option_AddSource,
                AllowMultipleArgumentsPerToken = true,
                HelpName = "nuget-source"
            };
        }

        internal static Option<bool> CreateForceOption()
        {
            return new("--force")
            {
                Arity = new ArgumentArity(0, 1),
                Description = SymbolStrings.TemplateCommand_Option_Force,
            };
        }

        internal static Option<string> CreateAuthorOption()
        {
            return new("--author")
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_AuthorFilter
            };
        }

        internal static Option<string> CreateBaselineOption()
        {
            return new("--baseline")
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_BaselineFilter,
                Hidden = true
            };
        }

        internal static Option<string> CreateLanguageOption()
        {
            return new("--language", "-lang")
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_LanguageFilter
            };
        }

        internal static Option<string> CreateTypeOption()
        {
            return new("--type")
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_TypeFilter
            };
        }

        internal static Option<string> CreateTagOption()
        {
            return new("--tag")
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_TagFilter
            };
        }

        internal static Option<string> CreatePackageOption()
        {
            return new("--package")
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_PackageFilter
            };
        }

        internal static Option<bool> CreateColumnsAllOption()
        {
            return new("--columns-all")
            {
                Arity = ArgumentArity.Zero,
                Description = SymbolStrings.Option_ColumnsAll
            };
        }

        internal static Option<string[]> CreateColumnsOption()
        {
            Option<string[]> option = new("--columns")
            {
                Arity = new ArgumentArity(1, 4),
                Description = SymbolStrings.Option_Columns,
                AllowMultipleArgumentsPerToken = true,
                CustomParser = ParseCommaSeparatedValues
            };
            option.AcceptOnlyFromAmong(
                TabularOutputSettings.ColumnNames.Author,
                TabularOutputSettings.ColumnNames.Language,
                TabularOutputSettings.ColumnNames.Type,
                TabularOutputSettings.ColumnNames.Tags);
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
