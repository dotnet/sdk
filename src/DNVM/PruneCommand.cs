
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Semver;

namespace Dnvm;

public sealed class PruneCommand
{
    public sealed record Options
    {
        public bool Verbose { get; init; } = false;
        public bool DryRun { get; init; } = false;
    }

    public static Task<int> Run(DnvmEnv env, Logger logger, DnvmSubCommand.PruneArgs args)
    {
        return Run(env, logger, new Options
        {
            Verbose = args.Verbose ?? false,
            DryRun = args.DryRun ?? false
        });
    }

    public static async Task<int> Run(DnvmEnv env, Logger logger, Options options)
    {
        Manifest manifest;
        try
        {
            manifest = await env.ReadManifest();
        }
        catch (Exception e)
        {
            Environment.FailFast("Error reading manifest: ", e);
            // unreachable
            return 1;
        }

        var sdksToRemove = GetOutOfDateSdks(manifest);
        foreach (var sdk in sdksToRemove)
        {
            if (options.DryRun)
            {
                Console.WriteLine($"Would remove {sdk}");
            }
            else
            {
                Console.WriteLine($"Removing {sdk}");
                int result = await UninstallCommand.Run(env, logger, sdk.Version, sdk.Dir);
                if (result != 0)
                {
                    return result;
                }
            }
        }
        return 0;
    }

    public static List<(SemVersion Version, SdkDirName Dir)> GetOutOfDateSdks(Manifest manifest)
    {
        var latestMajorMinorInDirs = new Dictionary<(SdkDirName Dir, string MajorMinor), SemVersion>();
        var sdksToRemove = new List<(SemVersion, SdkDirName)>();
        foreach (var sdk in manifest.InstalledSdks)
        {
            var majorMinor = sdk.SdkVersion.ToMajorMinor();
            var dir = sdk.SdkDirName;
            if (latestMajorMinorInDirs.TryGetValue((sdk.SdkDirName, majorMinor), out var latest))
            {
                int order = sdk.SdkVersion.ComparePrecedenceTo(latest);
                if (order < 0)
                {
                    // This sdk is older than the latest in the same dir
                    sdksToRemove.Add((sdk.SdkVersion, dir));
                }
                else if (order > 0)
                {
                    // This sdk is newer than the latest in the same dir
                    sdksToRemove.Add((latest, dir));
                    latestMajorMinorInDirs[(sdk.SdkDirName, majorMinor)] = sdk.SdkVersion;
                }
                else
                {
                    // same version, do nothing
                }
            }
            else
            {
                latestMajorMinorInDirs[(sdk.SdkDirName, majorMinor)] = sdk.SdkVersion;
            }
        }
        return sdksToRemove;
    }
}