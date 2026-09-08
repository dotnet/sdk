// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using VerifyTests;

namespace Microsoft.TemplateEngine.Authoring.CLI
{
    internal sealed class Program
    {
        internal static Task<int> Main(string[] args)
        {
            // TemplateVerifier no longer ships with a built-in verifier, so the hosting application must
            // supply one. The CLI runs outside a test framework, so it supplies the Verify context directly.
            VerificationEngine.DirectoryVerifier ??= VerifyDirectory;

            RootCommand rootCommand = new("dotnet-template-authoring");
            rootCommand.Subcommands.Add(new LocalizeCommand());
            rootCommand.Subcommands.Add(new VerifyCommand());
            rootCommand.Subcommands.Add(new ValidateCommand());

            return rootCommand.Parse(args, new() { EnablePosixBundling = false }).InvokeAsync();
        }

        private static SettingsTask VerifyDirectory(
            string path,
            Func<string, bool>? include,
            string? pattern,
            EnumerationOptions? options,
            VerifySettings? settings,
            object? info,
            FileScrubber? fileScrubber,
            string sourceFile)
        {
            return new(
                settings,
                async configuredSettings =>
                {
                    configuredSettings.UseUniqueDirectory();
                    VerifierSettings.AssignTargetAssembly(typeof(Program).Assembly);

                    string sourceDirectory = Path.GetDirectoryName(sourceFile)!;
                    PathInfo pathInfo = new(sourceDirectory, nameof(Program), nameof(Main));
                    using InnerVerifier verifier = new(
                        sourceFile,
                        configuredSettings,
                        nameof(Program),
                        nameof(Main),
                        methodParameters: null,
                        pathInfo);

                    return await verifier.VerifyDirectory(path, include, pattern, options, info, fileScrubber).ConfigureAwait(false);
                });
        }
    }
}
