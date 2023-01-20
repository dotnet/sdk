// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Cli
{
    // This parser is used for completion and telemetry.
    // See https://github.com/NuGet/NuGet.Client for the actual implementation.
    internal static class NuGetCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-nuget";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("nuget", DocsLink);

            command.Options.Add(new Option<bool>("--version"));
            command.Options.Add(new Option<string>(new string[] { "-v", "--verbosity" }));

            command.Subcommands.Add(GetDeleteCommand());
            command.Subcommands.Add(GetLocalsCommand());
            command.Subcommands.Add(GetPushCommand());
            command.Subcommands.Add(GetVerifyCommand());
            command.Subcommands.Add(GetTrustCommand());
            command.Subcommands.Add(GetSignCommand());

            command.SetHandler(NuGetCommand.Run);

            return command;
        }

        private static Command GetDeleteCommand()
        {
            var deleteCommand = new Command("delete");
            deleteCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });
            deleteCommand.Options.Add(new Option<bool>("--force-english-output"));
            deleteCommand.Options.Add(new Option<string>(new string[] { "-s", "--source" }));
            deleteCommand.Options.Add(new Option<bool>("--non-interactive"));
            deleteCommand.Options.Add(new Option<string>(new string[] { "-k", "--api-key" }));
            deleteCommand.Options.Add(new Option<bool>("--no-service-endpoint"));
            deleteCommand.Options.Add(new Option<bool>("--interactive"));

            deleteCommand.SetHandler(NuGetCommand.Run);

            return deleteCommand;
        }

        private static Command GetLocalsCommand()
        {
            var localsCommand = new Command("locals");

            Argument<string> foldersArgument = new Argument<string>("folders");
            foldersArgument.AcceptOnlyFromAmong(new string[] { "all", "http-cache", "global-packages", "plugins-cache", "temp" });

            localsCommand.Arguments.Add(foldersArgument);

            localsCommand.Options.Add(new Option<bool>("--force-english-output"));
            localsCommand.Options.Add(new Option<bool>(new string[] { "-c", "--clear" }));
            localsCommand.Options.Add(new Option<bool>(new string[] { "-l", "--list" }));

            localsCommand.SetHandler(NuGetCommand.Run);

            return localsCommand;
        }

        private static Command GetPushCommand()
        {
            var pushCommand = new Command("push");

            pushCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

            pushCommand.Options.Add(new Option<bool>("--force-english-output"));
            pushCommand.Options.Add(new Option<string>(new string[] { "-s", "--source" }));
            pushCommand.Options.Add(new Option<string>(new string[] { "-ss", "--symbol-source" }));
            pushCommand.Options.Add(new Option<string>(new string[] { "-t", "--timeout" }));
            pushCommand.Options.Add(new Option<string>(new string[] { "-k", "--api-key" }));
            pushCommand.Options.Add(new Option<string>(new string[] { "-sk", "--symbol-api-key" }));
            pushCommand.Options.Add(new Option<bool>(new string[] { "-d", "--disable-buffering" }));
            pushCommand.Options.Add(new Option<bool>(new string[] { "-n", "--no-symbols" }));
            pushCommand.Options.Add(new Option<bool>("--no-service-endpoint"));
            pushCommand.Options.Add(new Option<bool>("--interactive"));
            pushCommand.Options.Add(new Option<bool>("--skip-duplicate"));

            pushCommand.SetHandler(NuGetCommand.Run);

            return pushCommand;
        }

        private static Command GetVerifyCommand()
        {
            const string fingerprint = "--certificate-fingerprint";
            var verifyCommand = new Command("verify");

            verifyCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

            verifyCommand.Options.Add(new Option<bool>("--all"));
            verifyCommand.Options.Add(new ForwardedOption<IEnumerable<string>>(fingerprint)
                .ForwardAsManyArgumentsEachPrefixedByOption(fingerprint)
                .AllowSingleArgPerToken());
            verifyCommand.Options.Add(CommonOptions.VerbosityOption);

            verifyCommand.SetHandler(NuGetCommand.Run);

            return verifyCommand;
        }

        private static Command GetTrustCommand()
        {
            var trustCommand = new Command("trust");

            Argument<string> commandArgument = new Argument<string>("command") { Arity = ArgumentArity.ZeroOrOne };
            commandArgument.AcceptOnlyFromAmong(new string[] { "list", "author", "repository", "source", "certificate", "remove", "sync" });

            trustCommand.Arguments.Add(commandArgument);

            trustCommand.Options.Add(new Option<string>("--algorithm"));
            trustCommand.Options.Add(new Option<bool>("--allow-untrusted-root"));
            trustCommand.Options.Add(new Option<string>("--owners"));
            trustCommand.Options.Add(new Option<string>("--configfile"));
            trustCommand.Options.Add(CommonOptions.VerbosityOption);

            trustCommand.SetHandler(NuGetCommand.Run);

            return trustCommand;
        }
        
        private static Command GetSignCommand()
        {
            var signCommand = new Command("sign");

            signCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

            signCommand.Options.Add(new Option<string>(new string[] { "-o", "--output" }));
            signCommand.Options.Add(new Option<string>("--certificate-path"));
            signCommand.Options.Add(new Option<string>("--certificate-store-name"));
            signCommand.Options.Add(new Option<string>("--certificate-store-location"));
            signCommand.Options.Add(new Option<string>("--certificate-subject-name"));
            signCommand.Options.Add(new Option<string>("--certificate-fingerprint"));
            signCommand.Options.Add(new Option<string>("--certificate-password"));
            signCommand.Options.Add(new Option<string>("--hash-algorithm"));
            signCommand.Options.Add(new Option<string>("--timestamper"));
            signCommand.Options.Add(new Option<string>("--timestamp-hash-algorithm"));
            signCommand.Options.Add(new Option<bool>("--overwrite"));
            signCommand.Options.Add(CommonOptions.VerbosityOption);

            signCommand.SetHandler(NuGetCommand.Run);

            return signCommand;
        }
    }
}
