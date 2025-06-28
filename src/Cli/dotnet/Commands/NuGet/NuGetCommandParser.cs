// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using NuGetWhyCommand = NuGet.CommandLine.XPlat.Commands.Why.WhyCommand;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

// This parser is used for completion and _telemetry.
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
        var command = new DocumentedCommand("nuget", DocsLink)
        {
            // some subcommands are not defined here and just forwarded to NuGet app
            TreatUnmatchedTokensAsErrors = false
        };

        command.Options.Add(new Option<bool>("--version")
        {
            Arity = ArgumentArity.Zero
        });
        command.Options.Add(new Option<string>("--verbosity", "-v"));

        command.Subcommands.Add(GetDeleteCommand());
        command.Subcommands.Add(GetLocalsCommand());
        command.Subcommands.Add(GetPushCommand());
        command.Subcommands.Add(GetVerifyCommand());
        command.Subcommands.Add(GetTrustCommand());
        command.Subcommands.Add(GetSignCommand());
        NuGetWhyCommand.GetWhyCommand(command);

        command.SetAction(NuGetCommand.Run);

        return command;
    }

    private static Command GetDeleteCommand()
    {
        Command deleteCommand = new("delete");
        deleteCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });
        deleteCommand.Options.Add(new Option<bool>("--force-english-output")
        {
            Arity = ArgumentArity.Zero
        });
        deleteCommand.Options.Add(new Option<string>("--source", "-s"));
        deleteCommand.Options.Add(new Option<bool>("--non-interactive")
        {
            Arity = ArgumentArity.Zero
        });
        deleteCommand.Options.Add(new Option<string>("--api-key", "-k"));
        deleteCommand.Options.Add(new Option<bool>("--no-service-endpoint")
        {
            Arity = ArgumentArity.Zero
        });
        deleteCommand.Options.Add(CommonOptions.InteractiveOption());

        deleteCommand.SetAction(NuGetCommand.Run);

        return deleteCommand;
    }

    private static Command GetLocalsCommand()
    {
        Command localsCommand = new("locals");

        Argument<string> foldersArgument = new("folders");
        foldersArgument.AcceptOnlyFromAmong(["all", "http-cache", "global-packages", "plugins-cache", "temp"]);

        localsCommand.Arguments.Add(foldersArgument);

        localsCommand.Options.Add(new Option<bool>("--force-english-output")
        {
            Arity = ArgumentArity.Zero
        });
        localsCommand.Options.Add(new Option<bool>("--clear", "-c")
        {
            Arity = ArgumentArity.Zero
        });
        localsCommand.Options.Add(new Option<bool>("--list", "-l")
        {
            Arity = ArgumentArity.Zero
        });

        localsCommand.SetAction(NuGetCommand.Run);

        return localsCommand;
    }

    private static Command GetPushCommand()
    {
        Command pushCommand = new("push");

        pushCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

        pushCommand.Options.Add(new Option<bool>("--force-english-output")
        {
            Arity = ArgumentArity.Zero
        });
        pushCommand.Options.Add(new Option<string>("--source", "-s"));
        pushCommand.Options.Add(new Option<string>("--symbol-source", "-ss"));
        pushCommand.Options.Add(new Option<string>("--timeout", "-t"));
        pushCommand.Options.Add(new Option<string>("--api-key", "-k"));
        pushCommand.Options.Add(new Option<string>("--symbol-api-key", "-sk"));
        pushCommand.Options.Add(new Option<bool>("--disable-buffering", "-d")
        {
            Arity = ArgumentArity.Zero
        });
        pushCommand.Options.Add(new Option<bool>("--no-symbols", "-n")
        {
            Arity = ArgumentArity.Zero
        });
        pushCommand.Options.Add(new Option<bool>("--no-service-endpoint")
        {
            Arity = ArgumentArity.Zero
        });
        pushCommand.Options.Add(CommonOptions.InteractiveOption());
        pushCommand.Options.Add(new Option<bool>("--skip-duplicate")
        {
            Arity = ArgumentArity.Zero
        });
        pushCommand.Options.Add(new Option<string>("--configfile"));

        pushCommand.SetAction(NuGetCommand.Run);

        return pushCommand;
    }

    private static Command GetVerifyCommand()
    {
        const string fingerprint = "--certificate-fingerprint";
        Command verifyCommand = new("verify");

        verifyCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

        verifyCommand.Options.Add(new Option<bool>("--all")
        {
            Arity = ArgumentArity.Zero
        });
        verifyCommand.Options.Add(new ForwardedOption<IEnumerable<string>>(fingerprint)
            .ForwardAsManyArgumentsEachPrefixedByOption(fingerprint)
            .AllowSingleArgPerToken());
        verifyCommand.Options.Add(CommonOptions.VerbosityOption);

        verifyCommand.SetAction(NuGetCommand.Run);

        return verifyCommand;
    }

    private static Command GetTrustCommand()
    {
        Command trustCommand = new("trust");

        Option<bool> allowUntrustedRoot = new("--allow-untrusted-root")
        {
            Arity = ArgumentArity.Zero
        };
        Option<string> owners = new("--owners");

        trustCommand.Subcommands.Add(new Command("list"));
        trustCommand.Subcommands.Add(AuthorCommand());
        trustCommand.Subcommands.Add(RepositoryCommand());
        trustCommand.Subcommands.Add(SourceCommand());
        trustCommand.Subcommands.Add(CertificateCommand());
        trustCommand.Subcommands.Add(RemoveCommand());
        trustCommand.Subcommands.Add(SyncCommand());

        Option<string> configFile = new("--configfile");

        // now set global options for all nuget commands: configfile, verbosity
        // as well as the standard NugetCommand.Run handler

        trustCommand.Options.Add(configFile);
        trustCommand.Options.Add(CommonOptions.VerbosityOption);
        trustCommand.SetAction(NuGetCommand.Run);

        foreach (var command in trustCommand.Subcommands)
        {
            command.Options.Add(configFile);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.SetAction(NuGetCommand.Run);
        }

        Command AuthorCommand() => new("author") {
            new Argument<string>("NAME"),
            new Argument<string>("PACKAGE"),
            allowUntrustedRoot,
        };

        Command RepositoryCommand() => new("repository") {
            new Argument<string>("NAME"),
            new Argument<string>("PACKAGE"),
            allowUntrustedRoot,
            owners
        };

        Command SourceCommand() => new("source") {
            new Argument<string>("NAME"),
            owners,
            new Option<string>("--source-url"),
        };

        Command CertificateCommand()
        {
            Option<string> algorithm = new("--algorithm")
            {
                DefaultValueFactory = (_argResult) => "SHA256"
            };
            algorithm.AcceptOnlyFromAmong("SHA256", "SHA384", "SHA512");

            return new Command("certificate") {
                new Argument<string>("NAME"),
                new Argument<string>("FINGERPRINT"),
                allowUntrustedRoot,
                algorithm
            };
        }
        ;

        Command RemoveCommand() => new("remove") {
            new Argument<string>("NAME"),
        };

        Command SyncCommand() => new("sync") {
            new Argument<string>("NAME"),
        };

        return trustCommand;
    }

    private static Command GetSignCommand()
    {
        Command signCommand = new("sign");

        signCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

        signCommand.Options.Add(new Option<string>("--output", "-o"));
        signCommand.Options.Add(new Option<string>("--certificate-path"));
        signCommand.Options.Add(new Option<string>("--certificate-store-name"));
        signCommand.Options.Add(new Option<string>("--certificate-store-location"));
        signCommand.Options.Add(new Option<string>("--certificate-subject-name"));
        signCommand.Options.Add(new Option<string>("--certificate-fingerprint"));
        signCommand.Options.Add(new Option<string>("--certificate-password"));
        signCommand.Options.Add(new Option<string>("--hash-algorithm"));
        signCommand.Options.Add(new Option<string>("--timestamper"));
        signCommand.Options.Add(new Option<string>("--timestamp-hash-algorithm"));
        signCommand.Options.Add(new Option<bool>("--overwrite")
        {
            Arity = ArgumentArity.Zero
        });
        signCommand.Options.Add(CommonOptions.VerbosityOption);

        signCommand.SetAction(NuGetCommand.Run);

        return signCommand;
    }
}
