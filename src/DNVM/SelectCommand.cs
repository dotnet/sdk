
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Serde.Json;
using Spectre.Console;
using StaticCs;
using Zio;

namespace Microsoft.DotNet.DNVM;

public static class SelectCommand
{
    public enum Result
    {
        Success,
        BadDirName,
    }

    public static async Task<Result> Run(DnvmEnv dnvmEnv, Logger logger, SdkDirName sdkDirName)
    {
        var manifest = await dnvmEnv.ReadManifest();
        switch (await RunWithManifest(dnvmEnv, sdkDirName, manifest, logger))
        {
            case Result<Manifest, Result>.Ok(var newManifest):
                dnvmEnv.WriteManifest(newManifest);
                return Result.Success;
            case Result<Manifest, Result>.Err(var error):
                return error;
            default:
                throw ExceptionUtilities.Unreachable;
        }
        ;
    }

    public static Task<Result<Manifest, Result>> RunWithManifest(DnvmEnv env, SdkDirName newDir, Manifest manifest, Logger logger)
    {
        var validDirs = manifest.RegisteredChannels.Select(c => c.SdkDirName).ToList();

        if (!validDirs.Contains(newDir))
        {
            logger.Error($"Invalid SDK directory name: {newDir.Name}");
            logger.Log("Valid SDK directory names:");
            foreach (var dir in validDirs)
            {
                logger.Log($"  {dir.Name}");
            }
            return Task.FromResult<Result<Manifest, Result>>(Result.BadDirName);
        }

        SelectDir(logger, env, manifest.CurrentSdkDir, newDir);
        Result<Manifest, Result> result = manifest with { CurrentSdkDir = newDir };
        return Task.FromResult(result);
    }

    /// <summary>
    /// Change the current SDK directory to the target SDK directory. Doesn't update the manifest.
    ///
    /// This has two implementations - one for Windows and one for Unix. The Unix implementation
    /// uses a symlink to point to the target SDK directory, while the Windows implementation adds
    /// the target SDK directory to the PATH environment variable.
    /// </summary>
    internal static void SelectDir(Logger logger, DnvmEnv dnvmEnv, SdkDirName currentDirName, SdkDirName newDirName)
    {
        if (OperatingSystem.IsWindows())
        {
            RetargetPath(dnvmEnv, currentDirName, newDirName);
        }
        else
        {
            RetargetSymlink(logger, dnvmEnv, currentDirName, newDirName);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RetargetPath(DnvmEnv dnvmEnv, SdkDirName currentDirName, SdkDirName newDirName)
    {
        // First grab the current PATH and look for the existing SDK directory in the PATH. If it
        // exists, remove it.
        var currentPath = dnvmEnv.GetUserEnvVar("PATH");
        List<string> pathDirs = new List<string>();
        if (currentPath != null)
        {
            pathDirs = currentPath.Split(';').ToList();
            var currentDirPath = dnvmEnv.RealPath(DnvmEnv.GetSdkPath(currentDirName));
            pathDirs.Remove(currentDirPath);
        }
        var newDirPath = dnvmEnv.RealPath(DnvmEnv.GetSdkPath(newDirName));
        pathDirs.Insert(0, newDirPath);
        dnvmEnv.SetUserEnvVar("PATH", string.Join(";", pathDirs));
    }

    [UnsupportedOSPlatform("windows")]
    private static void RetargetSymlink(Logger logger, DnvmEnv dnvmEnv, SdkDirName newDirName, SdkDirName sdkDirName)
    {
        var dotnetExePath = DnvmEnv.GetSdkPath(sdkDirName) / Utilities.DotnetExeName;
        var realDotnetPath = dnvmEnv.RealPath(dotnetExePath);
        logger.Info($"Retargeting symlink in {dnvmEnv.RealPath(UPath.Root)} to {realDotnetPath}");
        if (!dnvmEnv.DnvmHomeFs.FileExists(dotnetExePath))
        {
            logger.Info("SDK install not found, skipping symlink creation.");
            return;
        }

        var homeFs = dnvmEnv.DnvmHomeFs;
        // Delete if it already exists
        try
        {
            homeFs.DeleteFile(DnvmEnv.SymlinkPath);
        }
        catch { }

        homeFs.CreateSymbolicLink(DnvmEnv.SymlinkPath, dotnetExePath);
    }
}