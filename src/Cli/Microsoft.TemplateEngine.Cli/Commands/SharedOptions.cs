// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptions
    {
        public static CliOption<FileInfo> OutputOption { get; } = new("--output", "-o")
        {
            Description = SymbolStrings.Option_Output,
            Required = false,
            Arity = new ArgumentArity(1, 1)
        };

        public static CliOption<FileInfo> ProjectPathOption { get; } = new CliOption<FileInfo>("--project")
        {
            Description = SymbolStrings.Option_ProjectPath
        }.AcceptExistingOnly();

        public static CliOption<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal static CliOption<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption();

        internal static CliOption<string> NameOption { get; } = new("--name", "-n")
        {
            Description = SymbolStrings.TemplateCommand_Option_Name,
            Arity = new ArgumentArity(1, 1)
        };

        internal static CliOption<bool> DryRunOption { get; } = new("--dry-run")
        {
            Description = SymbolStrings.TemplateCommand_Option_DryRun,
            Arity = new ArgumentArity(0, 1)
        };

        internal static CliOption<bool> NoUpdateCheckOption { get; } = new("--no-update-check")
        {
            Description = SymbolStrings.TemplateCommand_Option_NoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };
    }
}
