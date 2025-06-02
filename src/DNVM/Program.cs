using System.Net.Http;
using System.Threading.Tasks;
using Semver;
using Spectre.Console;

namespace Microsoft.DotNet.DNVM;

public static class Program
{
    public static readonly SemVersion SemVer = SemVersion.Parse(GitVersionInformation.SemVer, SemVersionStyles.Strict);

    public static async Task<int> Main(string[] args)
    {
        var console = AnsiConsole.Console;
        if (!console.Profile.Out.IsTerminal)
        {
            // Set the width to a large, but reasonable, value to avoid wrapping.
            console.Profile.Width = 255;
        }
        console.WriteLine("dnvm " + SemVer + " " + GitVersionInformation.ShortSha);
        console.WriteLine();
        var logger = new Logger(console);

        var parsedArgs = CommandLineArguments.TryParse(console, args);
        if (parsedArgs is null)
        {
            // Help was requested, exit with success.
            return 0;
        }

        // Self-install is special, since we don't know the DNVM_HOME yet.
        if (parsedArgs.SubCommand is DnvmSubCommand.SelfInstallArgs selfInstallArgs)
        {
            return (int)await SelfInstallCommand.Run(logger, selfInstallArgs);
        }

        using var env = DnvmEnv.CreateDefault();
        if (parsedArgs.SubCommand is null)
        {
            if (parsedArgs.EnableDnvmPreviews == true)
            {
                return await EnableDnvmPreviews(env);
            }
            else
            {
                // Help was requested, exit with success.
                return 0;
            }
        }

        return await Dnvm(env, logger, parsedArgs);
    }

    public static async Task<int> EnableDnvmPreviews(DnvmEnv env)
    {
        var manifest = await ManifestUtils.ReadOrCreateManifest(env);
        manifest = manifest with { PreviewsEnabled = true };
        env.WriteManifest(manifest);
        return 0;
    }

    internal static async Task<int> Dnvm(DnvmEnv env, Logger logger, DnvmArgs args)
    {
        return args.SubCommand switch
        {
            DnvmSubCommand.TrackArgs a => (int)await TrackCommand.Run(env, logger, a),
            DnvmSubCommand.InstallArgs a => (int)await InstallCommand.Run(env, logger, a),
            DnvmSubCommand.UpdateArgs a => (int)await UpdateCommand.Run(env, logger, a),
            DnvmSubCommand.ListArgs => (int)await ListCommand.Run(logger, env),
            DnvmSubCommand.SelectArgs a => (int)await SelectCommand.Run(env, logger, new(a.SdkDirName)),
            DnvmSubCommand.UntrackArgs a => await UntrackCommand.Run(env, logger, a.Channel),
            DnvmSubCommand.UninstallArgs a => await UninstallCommand.Run(env, logger, a.SdkVersion, a.SdkDir),
            DnvmSubCommand.PruneArgs a => await PruneCommand.Run(env, logger, a),
            DnvmSubCommand.RestoreArgs a => await RestoreCommand.Run(env, logger, a) switch {
                Result<SemVersion, RestoreCommand.Error>.Ok => 0,
                Result<SemVersion, RestoreCommand.Error>.Err x => (int)x.Value,
            },

            DnvmSubCommand.SelfInstallArgs => throw ExceptionUtilities.Unreachable,
        };
    }
}
