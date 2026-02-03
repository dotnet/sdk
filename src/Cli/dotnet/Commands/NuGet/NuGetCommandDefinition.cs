// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using NuGetWhyCommand = NuGet.CommandLine.XPlat.Commands.Why.WhyCommand;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

// This parser is used for completion and telemetry.
// See https://github.com/NuGet/NuGet.Client for the actual implementation.
internal static class NuGetCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-nuget";

    public static Command Create()
    {
        var command = new Command("nuget")
        {
            // some subcommands are not defined here and just forwarded to NuGet app
            TreatUnmatchedTokensAsErrors = false,
            DocsLink = DocsLink
        };

        command.Options.Add(new Option<bool>("--version")
        {
            Arity = ArgumentArity.Zero
        });
        command.Options.Add(new Option<string>("--verbosity", "-v"));

        command.Subcommands.Add(CreateDeleteCommand());
        command.Subcommands.Add(CreateLocalsCommand());
        command.Subcommands.Add(CreatePushCommand());
        command.Subcommands.Add(CreateVerifyCommand());
        command.Subcommands.Add(CreateTrustCommand());
        command.Subcommands.Add(CreateSignCommand());
        NuGetWhyCommand.GetWhyCommand(command);

        return command;
    }

    private static Command CreateDeleteCommand()
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
        deleteCommand.Options.Add(CommonOptions.CreateInteractiveOption());

        return deleteCommand;
    }

    private static Command CreateLocalsCommand()
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

        return localsCommand;
    }

    private static Command CreatePushCommand()
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
        pushCommand.Options.Add(CommonOptions.CreateInteractiveOption());
        pushCommand.Options.Add(new Option<bool>("--skip-duplicate")
        {
            Arity = ArgumentArity.Zero
        });
        pushCommand.Options.Add(new Option<string>("--configfile"));

        return pushCommand;
    }

    private static Command CreateVerifyCommand()
    {
        const string fingerprint = "--certificate-fingerprint";
        Command verifyCommand = new("verify");

        verifyCommand.Arguments.Add(new Argument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

        verifyCommand.Options.Add(new Option<bool>("--all")
        {
            Arity = ArgumentArity.Zero
        });
        verifyCommand.Options.Add(new Option<IEnumerable<string>>(fingerprint)
            .ForwardAsManyArgumentsEachPrefixedByOption(fingerprint)
            .AllowSingleArgPerToken());
        verifyCommand.Options.Add(CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal));

        return verifyCommand;
    }

    private static Command CreateTrustCommand()
    {
        Command trustCommand = new("trust");

        Option<bool> allowUntrustedRoot = new("--allow-untrusted-root")
        {
            Arity = ArgumentArity.Zero
        };
        Option<string> owners = new("--owners");

        trustCommand.Subcommands.Add(new Command("list"));
        trustCommand.Subcommands.Add(CreateAuthorCommand());
        trustCommand.Subcommands.Add(CreateRepositoryCommand());
        trustCommand.Subcommands.Add(CreateSourceCommand());
        trustCommand.Subcommands.Add(CreateCertificateCommand());
        trustCommand.Subcommands.Add(CreateRemoveCommand());
        trustCommand.Subcommands.Add(CreateSyncCommand());

        Option<string> configFile = new("--configfile");

        // now set global options for all nuget commands: configfile, verbosity
        // as well as the standard NugetCommand.Run handler

        trustCommand.Options.Add(configFile);
        trustCommand.Options.Add(CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal));

        foreach (var command in trustCommand.Subcommands)
        {
            command.Options.Add(configFile);
            command.Options.Add(CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal));
        }

        Command CreateAuthorCommand() => new("author") {
            new Argument<string>("NAME"),
            new Argument<string>("PACKAGE"),
            allowUntrustedRoot,
        };

        Command CreateRepositoryCommand() => new("repository") {
            new Argument<string>("NAME"),
            new Argument<string>("PACKAGE"),
            allowUntrustedRoot,
            owners
        };

        Command CreateSourceCommand() => new("source") {
            new Argument<string>("NAME"),
            owners,
            new Option<string>("--source-url"),
        };

        Command CreateCertificateCommand()
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

        Command CreateRemoveCommand() => new("remove") {
            new Argument<string>("NAME"),
        };

        Command CreateSyncCommand() => new("sync") {
            new Argument<string>("NAME"),
        };

        return trustCommand;
    }

    private static Command CreateSignCommand()
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
        signCommand.Options.Add(CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal));

        return signCommand;
    }
}
