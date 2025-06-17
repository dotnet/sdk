// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptions
    {
        public static Option<FileInfo> OutputOption { get; } = new("--output", "-o")
        {
            Description = SymbolStrings.Option_Output,
            HelpName = SymbolStrings.Option_Output_HelpName,
            Required = false,
            Arity = new ArgumentArity(1, 1)
        };

        public static Option<FileInfo> ProjectPathOption { get; } = new Option<FileInfo>("--project")
        {
            Description = SymbolStrings.Option_ProjectPath,
            HelpName = SymbolStrings.Option_ProjectPath_HelpName
        }.AcceptExistingOnly();

        public static Option<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal static Option<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption();

        internal static Option<string> NameOption { get; } = new("--name", "-n")
        {
            Description = SymbolStrings.TemplateCommand_Option_Name,
            HelpName = SymbolStrings.TemplateCommand_Option_Name_HelpName,
            Arity = new ArgumentArity(1, 1)
        };

        internal static Option<bool> DryRunOption { get; } = new("--dry-run")
        {
            Description = SymbolStrings.TemplateCommand_Option_DryRun,
            Arity = new ArgumentArity(0, 1)
        };

        internal static Option<bool> NoUpdateCheckOption { get; } = new("--no-update-check")
        {
            Description = SymbolStrings.TemplateCommand_Option_NoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };
    }
}
