// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.TemplateEngine.Authoring.CLI
{
    internal sealed class Program
    {
        internal static Task<int> Main(string[] args)
        {
            // TemplateVerifier no longer ships with a built-in verifier, so the hosting application must
            // supply one. Route snapshot directory verification through the xUnit (v3) Verify integration,
            // which is the verifier this tool has always used.
            VerificationEngine.DirectoryVerifier ??=
                (path, include, pattern, options, settings, info, fileScrubber, sourceFile)
                    => VerifyXunit.Verifier.VerifyDirectory(path, include, pattern, options, settings, info, fileScrubber, sourceFile);

            RootCommand rootCommand = new("dotnet-template-authoring");
            rootCommand.Subcommands.Add(new LocalizeCommand());
            rootCommand.Subcommands.Add(new VerifyCommand());
            rootCommand.Subcommands.Add(new ValidateCommand());

            return rootCommand.Parse(args, new() { EnablePosixBundling = false }).InvokeAsync();
        }
    }
}
