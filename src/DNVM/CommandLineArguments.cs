
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using Semver;
using Serde;
using Serde.CmdLine;
using Spectre.Console;
using StaticCs;

namespace Dnvm;

[GenerateDeserialize]
[Command("dnvm", Summary = "Install and manage .NET SDKs.")]
public partial record DnvmArgs
{
    [CommandOption("--enable-dnvm-previews", Description = "Enable dnvm previews.")]
    public bool? EnableDnvmPreviews { get; init; }

    [CommandGroup("command")]
    public DnvmSubCommand? SubCommand { get; init; }
}

[Closed]
[GenerateDeserialize]
public abstract partial record DnvmSubCommand
{
    private DnvmSubCommand() { }

    [Command("install", Summary = "Install an SDK.")]
    public sealed partial record InstallArgs : DnvmSubCommand
    {
        [CommandParameter(0, "version", Description = "The version of the SDK to install.")]
        [SerdeMemberOptions(DeserializeProxy = typeof(SemVersionProxy))]
        public required SemVersion SdkVersion { get; init; }

        [CommandOption("-f|--force", Description = "Force install the given SDK, even if already installed")]
        public bool? Force { get; init; } = null;

        [CommandOption("-s|--sdk-dir", Description = "Install the SDK into a separate directory with the given name.")]
        [SerdeMemberOptions(DeserializeProxy = typeof(NullableRefProxy.De<SdkDirName, SdkDirNameProxy>))] // Treat as string
        public SdkDirName? SdkDir { get; init; } = null;

        [CommandOption("-v|--verbose", Description = "Print debugging messages to the console.")]
        public bool? Verbose { get; init; } = null;
    }

    [Command("track", Summary = "Start tracking a new channel.")]
    public sealed partial record TrackArgs : DnvmSubCommand
    {
        [CommandParameter(0, "channel", Description = "Track the channel specified.")]
        [SerdeMemberOptions(DeserializeProxy = typeof(CaseInsensitiveChannel))]
        public required Channel Channel { get; init; }

        /// <summary>
        /// URL to the dotnet feed containing the releases index and SDKs.
        /// </summary>
        [CommandOption("--feed-url", Description = "Set the feed URL to download the SDK from.")]
        public string? FeedUrl { get; init; }

        [CommandOption("-v|--verbose", Description = "Print debugging messages to the console.")]
        public bool? Verbose { get; init; } = null;

        [CommandOption("-f|--force",  Description = "Force tracking the given channel, even if already tracked.")]
        public bool? Force { get; init; } = null;

        /// <summary>
        /// Answer yes to every question or use the defaults.
        /// </summary>
        [CommandOption("-y", Description = "Answer yes to all prompts.")]
        public bool? Yes { get; init; } = null;

        [CommandOption("--prereqs", Description = "Print prereqs for dotnet on Ubuntu.")]
        public bool? Prereqs { get; init; } = null;

        /// <summary>
        /// When specified, install the SDK into a separate directory with the given name,
        /// translated to lower-case. Preview releases are installed into a directory named 'preview'
        /// by default.
        /// </summary>
        [CommandOption("-s|--sdk-dir", Description = "Track the channel in a separate directory with the given name.")]
        public string? SdkDir { get; init; } = null;
    }

    [Command("selfinstall", Summary = "Install dnvm to the local machine.")]
    public sealed partial record SelfInstallArgs : DnvmSubCommand
    {
        [CommandOption("-v|--verbose", Description = "Print debugging messages to the console.")]
        public bool? Verbose { get; init; } = null;

        [CommandOption("-f|--force", Description = "Force install the given SDK, even if already installed")]
        public bool? Force { get; init; } = null;

        [CommandOption("--feed-url", Description = "Set the feed URL to download the SDK from.")]
        public string? FeedUrl { get; init; }

        [CommandOption("-y", Description = "Answer yes to all prompts.")]
        public bool? Yes { get; init; } = null;

        [CommandOption("--update", Description = "[internal] Update the current dnvm installation. Only intended to be called from dnvm.")]
        public bool? Update { get; init; } = null;

        [CommandOption("--dest-path", Description = "Set the destination path for the dnvm executable.")]
        public string? DestPath { get; init; } = null;
    }

    [Command("update", Summary = "Update the installed SDKs or dnvm itself.")]
    public sealed partial record UpdateArgs : DnvmSubCommand
    {
        [CommandOption("--dnvm-url", Description = "Set the URL for the dnvm releases endpoint.")]
        public string? DnvmReleasesUrl { get; init; } = null;

        [CommandOption("--feed-url", Description = "Set the feed URL to download the SDK from.")]
        public string? FeedUrl { get; init; } = null;

        [CommandOption("-v|--verbose", Description = "Print debugging messages to the console.")]
        public bool? Verbose { get; init; } = null;

        [CommandOption("--self", Description = "Update dnvm itself in the current location.")]
        public bool? Self { get; init; } = null;

        [CommandOption("-y", Description = "Answer yes to all prompts.")]
        public bool? Yes { get; init; } = null;
    }

    [Command("list", Summary = "List installed SDKs.")]
    public sealed partial record ListArgs : DnvmSubCommand
    {
    }

    [Command("select", Summary = "Select the active SDK directory.", Description =
"Select the active SDK directory, meaning the directory that will be used when running `dotnet` " +
"commands. This is the same directory passed to the `-s` option for `dnvm install`.\n" +
"\n" +
"Note: This command does not change between SDK versions installed in the same directory. For " +
"that, use the built-in dotnet global.json file. Information about global.json can be found at " +
"https://learn.microsoft.com/en-us/dotnet/core/tools/global-json.")]
    public sealed partial record SelectArgs : DnvmSubCommand
    {
        [CommandParameter(0, "sdkDirName", Description = "The name of the SDK directory to select.")]
        public required string SdkDirName { get; init; }
    }

    [Command("untrack", Summary = "Remove a channel from the list of tracked channels.")]
    public sealed partial record UntrackArgs : DnvmSubCommand
    {
        [CommandParameter(0, "channel", Description = "The channel to untrack.")]
        [SerdeMemberOptions(DeserializeProxy = typeof(CaseInsensitiveChannel))]
        public required Channel Channel { get; init; }
    }

    [Command("uninstall", Summary = "Uninstall an SDK.")]
    public sealed partial record UninstallArgs : DnvmSubCommand
    {
        [CommandParameter(0, "sdkVersion", Description = "The version of the SDK to uninstall.")]
        [SerdeMemberOptions(DeserializeProxy = typeof(SemVersionProxy))]
        public required SemVersion SdkVersion { get; init; }

        [CommandOption("-s|--sdk-dir", Description = "Uninstall the SDK from the given directory.")]
        [SerdeMemberOptions(DeserializeProxy = typeof(NullableRefProxy.De<SdkDirName, SdkDirNameProxy>))] // Treat as string
        public SdkDirName? SdkDir { get; init; } = null;
    }

    [Command("prune", Summary = "Remove all SDKs with older patch versions.")]
    public sealed partial record PruneArgs : DnvmSubCommand
    {
        [CommandOption("-v|--verbose", Description = "Print extra debugging info to the console.")]
        public bool? Verbose { get; init; } = null;

        [CommandOption("--dry-run", Description = "Print the list of the SDKs to be uninstalled, but don't uninstall.")]
        public bool? DryRun { get; init; } = null;
    }

    [Command("restore", Summary = "Restore the SDK listed in the global.json file.",
        Description = "Downloads the SDK in the global.json in or above the current directory.")]
    public sealed partial record RestoreArgs : DnvmSubCommand
    {
        [CommandOption("-l|--local", Description = "Install the sdk into the .dotnet folder in the same directory as global.json.")]
        public bool? Local { get; init; } = null;

        [CommandOption("-f|--force", Description = "Force install the SDK, even if already installed.")]
        public bool? Force { get; init; } = null;

        [CommandOption("-v|--verbose", Description = "Print extra debugging info to the console.")]
        public bool? Verbose { get; init; } = null;
    }

    /// <summary>
    /// Deserialize a named channel case-insensitively. Produces a user-friendly error message if the
    /// channel is not recognized.
    /// </summary>
    private sealed class CaseInsensitiveChannel : IDeserializeProvider<Channel>, IDeserialize<Channel>
    {
        public ISerdeInfo SerdeInfo => StringProxy.SerdeInfo;
        static IDeserialize<Channel> IDeserializeProvider<Channel>.Instance { get; } = new CaseInsensitiveChannel();
        private CaseInsensitiveChannel() { }

        public Channel Deserialize(IDeserializer deserializer)
        {
            try
            {
                return Channel.FromString(StringProxy.Instance.Deserialize(deserializer).ToLowerInvariant());
            }
            catch (DeserializeException)
            {
                var sep = Environment.NewLine + "\t- ";
                IEnumerable<Channel> channels = [new Channel.Latest(), new Channel.Preview(), new Channel.Lts(), new Channel.Sts()];
                throw new DeserializeException(
                    "Channel must be one of:"
                    + sep + string.Join(sep, channels));
            }
        }
    }
}

public static class CommandLineArguments
{
    /// <summary>
    /// Returns null if an error was produced or help was requested.
    /// </summary>
    public static DnvmArgs? TryParse(IAnsiConsole console, string[] args)
    {
        try
        {
            return ParseRaw(console, args);
        }
        catch (ArgumentSyntaxException ex)
        {
            console.WriteLine("error: " + ex.Message);
            console.WriteLine(CmdLine.GetHelpText<DnvmArgs>(includeHelp: true));
            return null;
        }
    }

    /// <summary>
    /// Throws an exception if the command was not recognized. Returns null if help was requested.
    /// </summary>
    public static DnvmArgs? ParseRaw(IAnsiConsole console, string[] args)
    {
        var result = CmdLine.ParseRawWithHelp<DnvmArgs>(args);
        DnvmArgs dnvmCmd;
        switch (result)
        {
            case CmdLine.ParsedArgsOrHelpInfos<DnvmArgs>.Parsed(var value):
                dnvmCmd = value;
                if (dnvmCmd.EnableDnvmPreviews is null && dnvmCmd.SubCommand is null)
                {
                    // Empty command is a help request.
                    console.WriteLine(CmdLine.GetHelpText<DnvmArgs>(includeHelp: true));
                }
                return dnvmCmd;
            case CmdLine.ParsedArgsOrHelpInfos<DnvmArgs>.Help(var helpInfos):
                var rootInfo = SerdeInfoProvider.GetDeserializeInfo<DnvmArgs>();
                var lastInfo = helpInfos.Last();
                console.WriteLine(CmdLine.GetHelpText(rootInfo, lastInfo, includeHelp: true));
                return null;
            default:
                throw new InvalidOperationException();
        }
    }
}