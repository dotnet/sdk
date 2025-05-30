
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Semver;
using Zio;

namespace Microsoft.DotNet.DNVM;

public sealed class UninstallCommand
{
    public static async Task<int> Run(DnvmEnv env, Logger logger, SemVersion sdkVersion, SdkDirName? dir = null)
    {
        Manifest manifest;
        try
        {
            manifest = await env.ReadManifest();
        }
        catch (Exception e)
        {
            logger.Error($"Error reading manifest: {e.Message}");
            throw;
        }

        var runtimesToKeep = new HashSet<(SemVersion, SdkDirName)>();
        var runtimesToRemove = new HashSet<(SemVersion, SdkDirName)>();
        var sdksToRemove = new HashSet<(SemVersion, SdkDirName)>();
        var aspnetToKeep = new HashSet<(SemVersion, SdkDirName)>();
        var aspnetToRemove = new HashSet<(SemVersion, SdkDirName)>();
        var winToKeep = new HashSet<(SemVersion, SdkDirName)>();
        var winToRemove = new HashSet<(SemVersion, SdkDirName)>();

        foreach (var installed in manifest.InstalledSdks)
        {
            if (installed.SdkVersion == sdkVersion && (dir is null || installed.SdkDirName == dir))
            {
                sdksToRemove.Add((installed.SdkVersion, installed.SdkDirName));
                runtimesToRemove.Add((installed.RuntimeVersion, installed.SdkDirName));
                aspnetToRemove.Add((installed.AspNetVersion, installed.SdkDirName));
                winToRemove.Add((installed.ReleaseVersion, installed.SdkDirName));
            }
            else
            {
                runtimesToKeep.Add((installed.RuntimeVersion, installed.SdkDirName));
                aspnetToKeep.Add((installed.AspNetVersion, installed.SdkDirName));
                winToKeep.Add((installed.ReleaseVersion, installed.SdkDirName));
            }
        }

        if (sdksToRemove.Count == 0)
        {
            logger.Error($"SDK version {sdkVersion} is not installed.");
            return 1;
        }

        runtimesToRemove.ExceptWith(runtimesToKeep);
        aspnetToRemove.ExceptWith(aspnetToKeep);
        winToRemove.ExceptWith(winToKeep);

        DeleteSdks(env, sdksToRemove, logger);
        DeleteRuntimes(env, runtimesToRemove, logger);
        DeleteAspnets(env, aspnetToRemove, logger);
        DeleteWins(env, winToRemove, logger);

        manifest = UninstallSdk(manifest, sdkVersion);
        env.WriteManifest(manifest);

        return 0;
    }

    private static void DeleteSdks(DnvmEnv env, IEnumerable<(SemVersion, SdkDirName)> sdks, Logger logger)
    {
        foreach (var (version, dir) in sdks)
        {
            var verString = version.ToString();
            var sdkDir = DnvmEnv.GetSdkPath(dir) / "sdk" / verString;

            logger.Log($"Deleting SDK {verString} from {dir.Name}");

            env.DnvmHomeFs.DeleteDirectory(sdkDir, isRecursive: true);
        }
    }

    private static void DeleteRuntimes(DnvmEnv env, IEnumerable<(SemVersion, SdkDirName)> runtimes, Logger logger)
    {
        foreach (var (version, dir) in runtimes)
        {
            var verString = version.ToString();
            var netcoreappDir = DnvmEnv.GetSdkPath(dir) / "shared" / "Microsoft.NETCore.App" / verString;
            var hostfxrDir = DnvmEnv.GetSdkPath(dir) / "host" / "fxr" / verString;
            var packsHostDir = DnvmEnv.GetSdkPath(dir) / "packs" / $"Microsoft.NETCore.App.Host.{Utilities.CurrentRID}" / verString;

            logger.Log($"Deleting Runtime {verString} from {dir.Name}");

            env.DnvmHomeFs.DeleteDirectory(netcoreappDir, isRecursive: true);
            env.DnvmHomeFs.DeleteDirectory(hostfxrDir, isRecursive: true);
            env.DnvmHomeFs.DeleteDirectory(packsHostDir, isRecursive: true);
        }
    }

    private static void DeleteAspnets(DnvmEnv env, IEnumerable<(SemVersion, SdkDirName)> aspnets, Logger logger)
    {
        foreach (var (version, dir) in aspnets)
        {
            var verString = version.ToString();
            var aspnetDir = DnvmEnv.GetSdkPath(dir) / "shared" / "Microsoft.AspNetCore.App" / verString;
            var templatesDir = DnvmEnv.GetSdkPath(dir) / "templates" / verString;

            logger.Log($"Deleting ASP.NET pack {verString} from {dir.Name}");

            env.DnvmHomeFs.DeleteDirectory(aspnetDir, isRecursive: true);
            env.DnvmHomeFs.DeleteDirectory(templatesDir, isRecursive: true);
        }
    }

    private static void DeleteWins(DnvmEnv env, IEnumerable<(SemVersion, SdkDirName)> wins, Logger logger)
    {
        foreach (var (version, dir) in wins)
        {
            var verString = version.ToString();
            var winDir = DnvmEnv.GetSdkPath(dir) / "shared" / "Microsoft.WindowsDesktop.App" / verString;

            if (env.DnvmHomeFs.DirectoryExists(winDir))
            {
                logger.Log($"Deleting Windows Desktop pack {verString} from {dir.Name}");

                env.DnvmHomeFs.DeleteDirectory(winDir, isRecursive: true);
            }
        }
    }

    private static Manifest UninstallSdk(Manifest manifest, SemVersion sdkVersion)
    {
        // Delete SDK version from all directories
        var newVersions = manifest.InstalledSdks
            .Where(sdk => sdk.SdkVersion != sdkVersion)
            .ToEq();
        return manifest with {
            InstalledSdks = newVersions,
        };
    }
}
