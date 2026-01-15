// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptions
    {
        // fitler options:
        public static Option<string> AuthorOption { get; } = SharedOptionsFactory.CreateAuthorOption();
        public static Option<string> BaselineOption { get; } = SharedOptionsFactory.CreateBaselineOption();
        public static Option<string> LanguageOption { get; } = SharedOptionsFactory.CreateLanguageOption();
        public static Option<string> TypeOption { get; } = SharedOptionsFactory.CreateTypeOption();
        public static Option<string> TagOption { get; } = SharedOptionsFactory.CreateTagOption();
        public static Option<string> PackageOption { get; } = SharedOptionsFactory.CreatePackageOption();

        public static Option<FileInfo> OutputOption { get; } = SharedOptionsFactory.CreateOutputOption();

        public static Option<FileInfo> ProjectPathOption { get; } = new Option<FileInfo>("--project")
        {
            Description = CommandDefinitionStrings.Option_ProjectPath
        }.AcceptExistingOnly();

        public static Option<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        public static Option<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        public static Option<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption();

        public static Option<string[]> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption();

        public static Option<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption();

        public static Option<string> NameOption { get; } = new("--name", "-n")
        {
            Description = CommandDefinitionStrings.TemplateCommand_Option_Name,
            Arity = new ArgumentArity(1, 1)
        };

        public static Option<bool> DryRunOption { get; } = new("--dry-run")
        {
            Description = CommandDefinitionStrings.TemplateCommand_Option_DryRun,
            Arity = new ArgumentArity(0, 1)
        };

        public static Option<bool> NoUpdateCheckOption { get; } = new("--no-update-check")
        {
            Description = CommandDefinitionStrings.TemplateCommand_Option_NoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };
    }
}
