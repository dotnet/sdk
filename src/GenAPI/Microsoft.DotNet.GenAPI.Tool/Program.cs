// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.GenAPI.Tool
{
    /// <summary>
    /// CLI frontend for the Roslyn-based GenAPI.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            // Global options
            CliOption<string[]> assembliesOption = new("--assembly")
            {
                Description = "The path to one or more assemblies or directories with assemblies.",
                CustomParser = ParseAssemblyArgument,
                Arity = ArgumentArity.OneOrMore,
                Required = true,
                Recursive = true
            };

            CliOption<string[]?> assemblyReferencesOption = new("--assembly-reference")
            {
                Description = "Paths to assembly references or their underlying directories for a specific target framework in the package.",
                CustomParser = ParseAssemblyArgument,
                Arity = ArgumentArity.ZeroOrMore,
                Recursive = true
            };

            CliOption<string[]?> excludeAttributesFilesOption = new("--exclude-attributes-file")
            {
                Description = "The path to one or more attribute exclusion files with types in DocId format.",
                CustomParser = ParseAssemblyArgument,
                Arity = ArgumentArity.ZeroOrMore,
                Recursive = true
            };

            CliOption<string?> outputPathOption = new("--output-path")
            {
                Description = @"Output path. Default is the console. Can specify an existing directory as well
            and then a file will be created for each assembly with the matching name of the assembly.",
                Recursive = true
            };

            CliOption<string?> headerFileOption = new("--header-file")
            {
                Description = "Specify a file with an alternate header content to prepend to output.",
                Recursive = true
            };

            CliOption<string?> exceptionMessageOption = new("--exception-message")
            {
                Description = "If specified - method bodies should throw PlatformNotSupportedException, else `throw null`.",
                Recursive = true
            };

            CliOption<bool> includeVisibleOutsideOfAssemblyOption = new("--include-visible-outside")
            {
                Description = "Include internal API's. Default is false.",
                Recursive = true
            };

            CliRootCommand rootCommand = new("Microsoft.DotNet.GenAPI")
            {
                TreatUnmatchedTokensAsErrors = true
            };
            rootCommand.Options.Add(assembliesOption);
            rootCommand.Options.Add(assemblyReferencesOption);
            rootCommand.Options.Add(excludeAttributesFilesOption);
            rootCommand.Options.Add(outputPathOption);
            rootCommand.Options.Add(headerFileOption);
            rootCommand.Options.Add(exceptionMessageOption);
            rootCommand.Options.Add(includeVisibleOutsideOfAssemblyOption);

            rootCommand.SetAction((ParseResult parseResult) =>
            {
                GenAPIApp.Run(new ConsoleLog(MessageImportance.Normal), new GenAPIApp.Context(
                    parseResult.GetValue(assembliesOption)!,
                    parseResult.GetValue(assemblyReferencesOption),
                    parseResult.GetValue(outputPathOption),
                    parseResult.GetValue(headerFileOption),
                    parseResult.GetValue(exceptionMessageOption),
                    parseResult.GetValue(excludeAttributesFilesOption),
                    parseResult.GetValue(includeVisibleOutsideOfAssemblyOption)
                ));
            });

            return rootCommand.Parse(args).Invoke();
        }

        /// Splits delimiter separated list of pathes represented as a string to a List of paths.
        /// </summary>
        /// <param name="pathSet">Delimiter separated list of paths.</param>
        /// <returns></returns>
        private static string[] ParseAssemblyArgument(ArgumentResult argumentResult)
        {
            List<string> args = new();
            foreach (Token token in argumentResult.Tokens)
            {
                args.AddRange(token.Value.Split(','));
            }

            return args.ToArray();
        }
    }
}

