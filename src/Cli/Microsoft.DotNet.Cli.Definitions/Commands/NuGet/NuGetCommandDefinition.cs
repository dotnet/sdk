// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

internal sealed class NuGetCommandDefinition : Command
{
    private readonly string Link = "https://aka.ms/dotnet-nuget";

    public readonly Option<bool> VersionOption = new("--version")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> VerbosityOption = new("--verbosity", "-v");

    public readonly NuGetDeleteCommandDefinition DeleteCommand = new();
    public readonly NuGetLocalsCommandDefinition LocalsCommand = new();
    public readonly NuGetPushCommandDefinition PushCommand = new();
    public readonly NuGetVerifyCommandDefinition VerifyCommand = new();
    public readonly NuGetTrustCommandDefinition TrustCommand = new();
    public readonly NuGetSignCommandDefinition SignCommand = new();

    public NuGetCommandDefinition()
        : base("nuget")
    {
        // some subcommands are not defined here and just forwarded to NuGet app
        TreatUnmatchedTokensAsErrors = false;
        this.DocsLink = Link;

        Options.Add(VersionOption);
        Options.Add(VerbosityOption);

        Subcommands.Add(DeleteCommand);
        Subcommands.Add(LocalsCommand);
        Subcommands.Add(PushCommand);
        Subcommands.Add(VerifyCommand);
        Subcommands.Add(TrustCommand);
        Subcommands.Add(SignCommand);

        // TODO: https://github.com/dotnet/sdk/issues/52661
        // Add `why` and `package` command definitions. They are currently only added to the implementation.
    }
}

public sealed class NuGetDeleteCommandDefinition : Command
{
    public readonly Argument<IEnumerable<string>> PackagePathsArgument = new("package-paths")
    {
        Arity = ArgumentArity.OneOrMore
    };

    public readonly Option<bool> ForceEnglishOption = new("--force-english-output")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> SourceOption = new("--source", "-s");

    public readonly Option<bool> NonInteractiveOption = new("--non-interactive")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> ApiKeyOption = new("--api-key", "-k");

    public readonly Option<bool> NoServiceEndpointOption = new("--no-service-endpoint")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public NuGetDeleteCommandDefinition()
        : base("delete")
    {
        Arguments.Add(PackagePathsArgument);

        Options.Add(ForceEnglishOption);
        Options.Add(SourceOption);
        Options.Add(NonInteractiveOption);
        Options.Add(ApiKeyOption);
        Options.Add(NoServiceEndpointOption);
        Options.Add(InteractiveOption);
    }
}

public sealed class NuGetLocalsCommandDefinition : Command
{
    public readonly Argument<string> FoldersArgument;

    public readonly Option<bool> ForceEnglishOption = new("--force-english-output")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> ClearOption = new("--clear", "-c")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> ListOption = new("--list", "-l")
    {
        Arity = ArgumentArity.Zero
    };

    public NuGetLocalsCommandDefinition()
        : base("locals")
    {
        FoldersArgument = new Argument<string>("folders");
        FoldersArgument.AcceptOnlyFromAmong(["all", "http-cache", "global-packages", "plugins-cache", "temp"]);

        Arguments.Add(FoldersArgument);

        Options.Add(ForceEnglishOption);
        Options.Add(ClearOption);
        Options.Add(ListOption);
    }
}

public sealed class NuGetPushCommandDefinition : Command
{
    public readonly Argument<IEnumerable<string>> PackagePathsArgument = new("package-paths")
    {
        Arity = ArgumentArity.OneOrMore
    };

    public readonly Option<bool> ForceEnglishOption = new("--force-english-output")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> SourceOption = new("--source", "-s");

    public readonly Option<string> SymbolSourceOption = new("--symbol-source", "-ss");

    public readonly Option<string> TimeoutOption = new("--timeout", "-t");

    public readonly Option<string> ApiKeyOption = new("--api-key", "-k");

    public readonly Option<string> SymbolApiKeyOption = new("--symbol-api-key", "-sk");

    public readonly Option<bool> DisableBufferingOption = new("--disable-buffering", "-d")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> NoSymbolsOption = new("--no-symbols", "-n")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> NoServiceEndpointOption = new("--no-service-endpoint")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public readonly Option<bool> SkipDuplicateOption = new("--skip-duplicate")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> ConfigFileOption = new("--configfile");

    public NuGetPushCommandDefinition()
        : base("push")
    {
        Arguments.Add(PackagePathsArgument);

        Options.Add(ForceEnglishOption);
        Options.Add(SourceOption);
        Options.Add(SymbolSourceOption);
        Options.Add(TimeoutOption);
        Options.Add(ApiKeyOption);
        Options.Add(SymbolApiKeyOption);
        Options.Add(DisableBufferingOption);
        Options.Add(NoSymbolsOption);
        Options.Add(NoServiceEndpointOption);
        Options.Add(InteractiveOption);
        Options.Add(SkipDuplicateOption);
        Options.Add(ConfigFileOption);
    }
}

public sealed class NuGetVerifyCommandDefinition : Command
{
    private const string CertificateFingerprintOptionName = "--certificate-fingerprint";

    public readonly Argument<IEnumerable<string>> PackagePathsArgument = new("package-paths")
    {
        Arity = ArgumentArity.OneOrMore
    };

    public readonly Option<bool> AllOption = new("--all")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<IEnumerable<string>> CertificateFingerprintOption = new Option<IEnumerable<string>>(CertificateFingerprintOptionName)
        .ForwardAsManyArgumentsEachPrefixedByOption(CertificateFingerprintOptionName)
        .AllowSingleArgPerToken();

    public readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public NuGetVerifyCommandDefinition()
        : base("verify")
    {
        Arguments.Add(PackagePathsArgument);

        Options.Add(AllOption);
        Options.Add(CertificateFingerprintOption);
        Options.Add(VerbosityOption);
    }
}

public sealed class NuGetTrustCommandDefinition : Command
{
    public readonly Option<bool> AllowUntrustedRootOption = new("--allow-untrusted-root")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> OwnersOption = new("--owners");

    public readonly Option<string> ConfigFileOption = new("--configfile");

    public readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(VerbosityOptions.normal);

    public readonly NuGetTrustListCommandDefinition ListCommand;
    public readonly NuGetTrustAuthorCommandDefinition AuthorCommand;
    public readonly NuGetTrustRepositoryCommandDefinition RepositoryCommand;
    public readonly NuGetTrustSourceCommandDefinition SourceCommand;
    public readonly NuGetTrustCertificateCommandDefinition CertificateCommand;
    public readonly NuGetTrustRemoveCommandDefinition RemoveCommand;
    public readonly NuGetTrustSyncCommandDefinition SyncCommand;

    public NuGetTrustCommandDefinition()
        : base("trust")
    {
        Options.Add(ConfigFileOption);
        Options.Add(VerbosityOption);

        Subcommands.Add(ListCommand = new(this));
        Subcommands.Add(AuthorCommand = new(this));
        Subcommands.Add(RepositoryCommand = new(this));
        Subcommands.Add(SourceCommand = new(this));
        Subcommands.Add(CertificateCommand = new(this));
        Subcommands.Add(RemoveCommand = new(this));
        Subcommands.Add(SyncCommand = new(this));
    }
}

public abstract class NuGetTrustSubcommandDefinition : Command
{
    protected NuGetTrustSubcommandDefinition(string name, NuGetTrustCommandDefinition parent)
        : base(name)
    {
        Options.Add(parent.ConfigFileOption);
        Options.Add(parent.VerbosityOption);
    }
}

public sealed class NuGetTrustListCommandDefinition(NuGetTrustCommandDefinition parent) : NuGetTrustSubcommandDefinition("list", parent);

public sealed class NuGetTrustAuthorCommandDefinition : NuGetTrustSubcommandDefinition
{
    public readonly Argument<string> NameArgument = new("NAME");
    public readonly Argument<string> PackageArgument = new("PACKAGE");

    public NuGetTrustAuthorCommandDefinition(NuGetTrustCommandDefinition parent)
        : base("author", parent)
    {
        Arguments.Add(NameArgument);
        Arguments.Add(PackageArgument);
        Options.Add(parent.AllowUntrustedRootOption);
    }
}

public sealed class NuGetTrustRepositoryCommandDefinition : NuGetTrustSubcommandDefinition
{
    public readonly Argument<string> NameArgument = new("NAME");
    public readonly Argument<string> PackageArgument = new("PACKAGE");

    public NuGetTrustRepositoryCommandDefinition(NuGetTrustCommandDefinition parent)
        : base("repository", parent)
    {
        Arguments.Add(NameArgument);
        Arguments.Add(PackageArgument);
        Options.Add(parent.AllowUntrustedRootOption);
        Options.Add(parent.OwnersOption);
    }
}

public sealed class NuGetTrustSourceCommandDefinition : NuGetTrustSubcommandDefinition
{
    public readonly Argument<string> NameArgument = new("NAME");
    public readonly Option<string> SourceUrlOption = new("--source-url");

    public NuGetTrustSourceCommandDefinition(NuGetTrustCommandDefinition parent)
        : base("source", parent)
    {
        Arguments.Add(NameArgument);
        Options.Add(parent.OwnersOption);
        Options.Add(SourceUrlOption);
    }
}

public sealed class NuGetTrustCertificateCommandDefinition : NuGetTrustSubcommandDefinition
{
    public readonly Argument<string> NameArgument = new("NAME");
    public readonly Argument<string> FingerprintArgument = new("FINGERPRINT");

    public readonly Option<string> AlgorithmOption = new Option<string>("--algorithm")
    {
        DefaultValueFactory = _ => "SHA256"
    }.AcceptOnlyFromAmong("SHA256", "SHA384", "SHA512");

    public NuGetTrustCertificateCommandDefinition(NuGetTrustCommandDefinition parent)
        : base("certificate", parent)
    {
        Arguments.Add(NameArgument);
        Arguments.Add(FingerprintArgument);
        Options.Add(parent.AllowUntrustedRootOption);
        Options.Add(AlgorithmOption);
    }
}

public sealed class NuGetTrustRemoveCommandDefinition : NuGetTrustSubcommandDefinition
{
    public readonly Argument<string> NameArgument = new("NAME");

    public NuGetTrustRemoveCommandDefinition(NuGetTrustCommandDefinition parent)
        : base("remove", parent)
    {
        Arguments.Add(NameArgument);
    }
}

public sealed class NuGetTrustSyncCommandDefinition : NuGetTrustSubcommandDefinition
{
    public readonly Argument<string> NameArgument = new("NAME");

    public NuGetTrustSyncCommandDefinition(NuGetTrustCommandDefinition parent)
        : base("sync", parent)
    {
        Arguments.Add(NameArgument);
    }
}

public sealed class NuGetSignCommandDefinition : Command
{
    public readonly Argument<IEnumerable<string>> PackagePathsArgument = new("package-paths")
    {
        Arity = ArgumentArity.OneOrMore
    };

    public readonly Option<string> OutputOption = new("--output", "-o");
    public readonly Option<string> CertificatePathOption = new("--certificate-path");
    public readonly Option<string> CertificateStoreNameOption = new("--certificate-store-name");
    public readonly Option<string> CertificateStoreLocationOption = new("--certificate-store-location");
    public readonly Option<string> CertificateSubjectNameOption = new("--certificate-subject-name");
    public readonly Option<string> CertificateFingerprintOption = new("--certificate-fingerprint");
    public readonly Option<string> CertificatePasswordOption = new("--certificate-password");
    public readonly Option<string> HashAlgorithmOption = new("--hash-algorithm");
    public readonly Option<string> TimestamperOption = new("--timestamper");
    public readonly Option<string> TimestampHashAlgorithmOption = new("--timestamp-hash-algorithm");

    public readonly Option<bool> OverwriteOption = new("--overwrite")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public NuGetSignCommandDefinition()
        : base("sign")
    {
        Arguments.Add(PackagePathsArgument);

        Options.Add(OutputOption);
        Options.Add(CertificatePathOption);
        Options.Add(CertificateStoreNameOption);
        Options.Add(CertificateStoreLocationOption);
        Options.Add(CertificateSubjectNameOption);
        Options.Add(CertificateFingerprintOption);
        Options.Add(CertificatePasswordOption);
        Options.Add(HashAlgorithmOption);
        Options.Add(TimestamperOption);
        Options.Add(TimestampHashAlgorithmOption);
        Options.Add(OverwriteOption);
        Options.Add(VerbosityOption);
    }
}
