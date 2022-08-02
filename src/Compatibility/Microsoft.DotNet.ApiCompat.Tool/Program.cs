﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompat.Tool
{
    class Program
    {
        static int Main(string[] args)
        {
            // CLI frontend for ApiCompat's ValidateAssemblies and ValidatePackage features.
            // Important: Keep parameters exposed in sync with the msbuild task frontend.

            // Global options
            Option<string?> suppressionFileOption = new("--suppression-file",
                "The path to a compatibility suppression file.")
            {
                ArgumentHelpName = "file"
            };
            Option<bool> generateSuppressionFileOption = new("--generate-suppression-file",
                "If true, generates a compatibility suppression file.");
            Option<string?> noWarnOption = new("--noWarn",
                "A NoWarn string that allows to disable specific rules.");
            Option<string?> roslynAssembliesPathOption = new("--roslyn-assemblies-path",
                "The path to the directory that contains the Microsoft.CodeAnalysis assemblies.")
            {
                ArgumentHelpName = "file"
            };
            Option<MessageImportance> verbosityOption = new(new string[] { "--verbosity", "-v" },
                "Controls the log level verbosity. Allowed values are high, normal, and low.");
            verbosityOption.SetDefaultValue(MessageImportance.Normal);

            // Root command
            Option<string[]> leftAssembliesOption = new(new string[] { "--left-assembly", "--left", "-l" },
                "The path to one or more assemblies that serve as the left side to compare.")
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                IsRequired = true
            };
            Option<string[]> rightAssembliesOption = new(new string[] { "--right-assembly", "--right", "-r" },
                "The path to one or more assemblies that serve as the right side to compare.")
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                IsRequired = true
            };
            Option<bool> strictModeOption = new("--strict-mode",
                "If true, performs api compatibility checks in strict mode");
            Option<string[][]?> leftAssembliesReferencesOption = new("--left-assembly-references",
                description: "Paths to assembly references or the underlying directories for a given left. Values must be separated by commas: ','.",
                parseArgument: ParseAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "file1,file2,..."
            };
            Option<string[][]?> rightAssembliesReferencesOption = new("--right-assembly-references",
                description: "Paths to assembly references or the underlying directories for a given right. Values must be separated by commas: ','.",
                parseArgument: ParseAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "file1,file2,..."
            };
            Option<bool> createWorkItemPerAssemblyOption = new("--create-workitem-per-assembly",
                "If true, enqueues a work item per passed in left and right assembly.");
            Option<(string, string)[]?> leftAssembliesTransformationPatternOption = new("--left-assemblies-transformation-pattern",
                description: "A transformation pattern for the left side assemblies.",
                parseArgument: ParseTransformationPattern)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore
            };
            Option<(string, string)[]?> rightAssembliesTransformationPatternOption = new("--right-assemblies-transformation-pattern",
                description: "A transformation pattern for the right side assemblies.",
                parseArgument: ParseTransformationPattern)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore
            };

            RootCommand rootCommand = new("Microsoft.DotNet.ApiCompat v" + Environment.Version.ToString(2))
            {
                TreatUnmatchedTokensAsErrors = true
            };
            rootCommand.AddGlobalOption(suppressionFileOption);
            rootCommand.AddGlobalOption(generateSuppressionFileOption);
            rootCommand.AddGlobalOption(noWarnOption);
            rootCommand.AddGlobalOption(roslynAssembliesPathOption);
            rootCommand.AddGlobalOption(verbosityOption);

            rootCommand.AddOption(leftAssembliesOption);
            rootCommand.AddOption(rightAssembliesOption);
            rootCommand.AddOption(strictModeOption);
            rootCommand.AddOption(leftAssembliesReferencesOption);
            rootCommand.AddOption(rightAssembliesReferencesOption);
            rootCommand.AddOption(createWorkItemPerAssemblyOption);
            rootCommand.AddOption(leftAssembliesTransformationPatternOption);
            rootCommand.AddOption(rightAssembliesTransformationPatternOption);

            rootCommand.SetHandler((InvocationContext context) =>
            {
                string? roslynAssembliesPath = context.ParseResult.GetValueForOption(roslynAssembliesPathOption);
                if (roslynAssembliesPath != null)
                {
                    RoslynResolver.Register(roslynAssembliesPath);
                }

                MessageImportance verbosity = context.ParseResult.GetValueForOption(verbosityOption);
                bool generateSuppressionFile = context.ParseResult.GetValueForOption(generateSuppressionFileOption);
                string? suppressionFile = context.ParseResult.GetValueForOption(suppressionFileOption);
                string? noWarn = context.ParseResult.GetValueForOption(noWarnOption);
                string[] leftAssemblies = context.ParseResult.GetValueForOption(leftAssembliesOption)!;
                string[] rightAssemblies = context.ParseResult.GetValueForOption(rightAssembliesOption)!;
                bool strictMode = context.ParseResult.GetValueForOption(strictModeOption);
                string[][]? leftAssembliesReferences = context.ParseResult.GetValueForOption(leftAssembliesReferencesOption);
                string[][]? rightAssembliesReferences = context.ParseResult.GetValueForOption(rightAssembliesReferencesOption);
                bool createWorkItemPerAssembly = context.ParseResult.GetValueForOption(createWorkItemPerAssemblyOption);
                (string, string)[]? leftAssembliesTransformationPattern = context.ParseResult.GetValueForOption(leftAssembliesTransformationPatternOption);
                (string, string)[]? rightAssembliesTransformationPattern = context.ParseResult.GetValueForOption(rightAssembliesTransformationPatternOption);

                Func<ISuppressionEngine, ConsoleCompatibilityLogger> logFactory = (suppressionEngine) => new(suppressionEngine, verbosity);
                ValidateAssemblies.Run(logFactory,
                    generateSuppressionFile,
                    suppressionFile,
                    noWarn,
                    leftAssemblies,
                    rightAssemblies,
                    strictMode,
                    leftAssembliesReferences,
                    rightAssembliesReferences,
                    createWorkItemPerAssembly,
                    leftAssembliesTransformationPattern,
                    rightAssembliesTransformationPattern);
            });

            // Package command
            Argument<string> packageArgument = new("--package",
                "The path to the package that should be validated")
            {
                Arity = ArgumentArity.ExactlyOne
            };
            Option<string?> runtimeGraphOption = new("--runtime-graph",
                "The path to the runtime graph to read from.")
            {
                ArgumentHelpName = "json"
            };
            Option<bool> runApiCompatOption = new("--run-api-compat",
                "If true, performs api compatibility checks on the package assets.");
            runApiCompatOption.SetDefaultValue(true);
            Option<bool> enableStrictModeForCompatibleTfmsOption = new("--enable-strict-mode-for-compatible-tfms",
                "Validates api compatibility in strict mode for contract and implementation assemblies for all compatible target frameworks.");
            Option<bool> enableStrictModeForCompatibleFrameworksInPackageOption = new("--enable-strict-mode-for-compatible-frameworks-in-package",
                "Validates api compatibility in strict mode for assemblies that are compatible based on their target framework.");
            Option<bool> enableStrictModeForBaselineValidationOption = new("--enable-strict-mode-for-baseline-validation",
                "Validates api compatibility in strict mode for package baseline checks.");
            Option<string?> baselinePackageOption = new("--baseline-package",
                "The path to a baseline package to validate against the current package.")
            {
                ArgumentHelpName = "nupkg"
            };
            Option<Dictionary<string, string[]>?> packageAssemblyReferencesOption = new("--package-assembly-references",
                description: "Paths to assembly references or their underlying directories for a specific target framework in the package. Values must be separated by commas: ','.",
                parseArgument: ParsePackageAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "tfm=file1,file2,..."
            };
            Option<Dictionary<string, string[]>?> baselinePackageAssemblyReferencesOption = new("--baseline-package-assembly-references",
                description: "Paths to assembly references or their underlying directories for a specific target framework in the baseline package. Values must be separated by commas: ','.",
                parseArgument: ParsePackageAssemblyReferenceArgument)
            {
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.ZeroOrMore,
                ArgumentHelpName = "tfm=file1,file2,..."
            };

            Command packageCommand = new("package", "Validates the compatibility of package assets");
            packageCommand.AddArgument(packageArgument);
            packageCommand.AddOption(runtimeGraphOption);
            packageCommand.AddOption(runApiCompatOption);
            packageCommand.AddOption(enableStrictModeForCompatibleTfmsOption);
            packageCommand.AddOption(enableStrictModeForCompatibleFrameworksInPackageOption);
            packageCommand.AddOption(enableStrictModeForBaselineValidationOption);
            packageCommand.AddOption(baselinePackageOption);
            packageCommand.AddOption(packageAssemblyReferencesOption);
            packageCommand.AddOption(baselinePackageAssemblyReferencesOption);
            packageCommand.SetHandler((InvocationContext context) =>
            {
                string? roslynAssembliesPath = context.ParseResult.GetValueForOption(roslynAssembliesPathOption);
                if (roslynAssembliesPath != null)
                {
                    RoslynResolver.Register(roslynAssembliesPath);
                }

                MessageImportance verbosity = context.ParseResult.GetValueForOption(verbosityOption);
                bool generateSuppressionFile = context.ParseResult.GetValueForOption(generateSuppressionFileOption);
                string? suppressionFile = context.ParseResult.GetValueForOption(suppressionFileOption);
                string? noWarn = context.ParseResult.GetValueForOption(noWarnOption);
                string package = context.ParseResult.GetValueForArgument(packageArgument);
                bool runApiCompat = context.ParseResult.GetValueForOption(runApiCompatOption);
                bool enableStrictModeForCompatibleTfms = context.ParseResult.GetValueForOption(enableStrictModeForCompatibleTfmsOption);
                bool enableStrictModeForCompatibleFrameworksInPackage = context.ParseResult.GetValueForOption(enableStrictModeForCompatibleFrameworksInPackageOption);
                bool enableStrictModeForBaselineValidation = context.ParseResult.GetValueForOption(enableStrictModeForBaselineValidationOption);
                string? baselinePackage = context.ParseResult.GetValueForOption(baselinePackageOption);
                string? runtimeGraph = context.ParseResult.GetValueForOption(runtimeGraphOption);
                Dictionary<string, string[]>? packageAssemblyReferences = context.ParseResult.GetValueForOption(packageAssemblyReferencesOption);
                Dictionary<string, string[]>? baselinePackageAssemblyReferences = context.ParseResult.GetValueForOption(baselinePackageAssemblyReferencesOption);

                Func<ISuppressionEngine, ConsoleCompatibilityLogger> logFactory = (suppressionEngine) => new(suppressionEngine, verbosity);
                ValidatePackage.Run(logFactory,
                    generateSuppressionFile,
                    suppressionFile,
                    noWarn,
                    package,
                    runApiCompat,
                    enableStrictModeForCompatibleTfms,
                    enableStrictModeForCompatibleFrameworksInPackage,
                    enableStrictModeForBaselineValidation,
                    baselinePackage,
                    runtimeGraph,
                    packageAssemblyReferences,
                    baselinePackageAssemblyReferences);
            });
            
            rootCommand.AddCommand(packageCommand);
            return rootCommand.Invoke(args);
        }

        private static string[][]? ParseAssemblyReferenceArgument(ArgumentResult argumentResult)
        {
            List<string[]> args = new();
            foreach (Token token in argumentResult.Tokens)
            {
                args.Add(token.Value.Split(','));
            }

            return args.ToArray();
        }

        private static (string CaptureGroupPattern, string ReplacementString)[]? ParseTransformationPattern(ArgumentResult argumentResult)
        {
            var patterns = new (string CaptureGroupPattern, string ReplacementPattern)[argumentResult.Tokens.Count];
            for (int i = 0; i < argumentResult.Tokens.Count; i++)
            {
                string[] parts = argumentResult.Tokens[i].Value.Split(';');
                if (parts.Length != 2)
                {
                    argumentResult.ErrorMessage = "Invalid assemblies transformation pattern. Usage: {regex-pattern};{replacement-string}";
                    continue;
                }

                patterns[i] = (parts[0], parts[1]);
            }

            return patterns;
        }

        private static Dictionary<string, string[]>? ParsePackageAssemblyReferenceArgument(ArgumentResult argumentResult)
        {
            Dictionary<string, string[]> args = new();
            foreach (Token token in argumentResult.Tokens)
            {
                string[] parts = token.Value.Split('=');
                if (parts.Length != 2)
                {
                    argumentResult.ErrorMessage = "Invalid package assembly reference format {tfm=assembly1,assembly2,assembly3,...}";
                    continue;
                }

                string tfm = parts[0];
                string[] assemblies = parts[1].Split(',');

                if (args.TryGetValue(tfm, out _))
                {
                    argumentResult.ErrorMessage = $"Package assembly references for tfm '{tfm}' are already provided.";
                    continue;
                }

                args.Add(tfm, assemblies);
            }

            return args;
        }
    }
}
