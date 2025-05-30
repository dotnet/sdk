
using System;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using Zio;

namespace Dnvm;

public static class ListCommand
{
    /// <summary>
    /// Prints a list of installed SDK versions and their locations.
    public static async Task<int> Run(Logger logger, DnvmEnv home)
    {
        Manifest manifest;
        try
        {
            manifest = await home.ReadManifest();
        }
        catch (Exception e)
        {
            Environment.FailFast("Error reading manifest: ", e);
            // unreachable
            return 1;
        }

        PrintSdks(logger, manifest, home.RealPath(UPath.Root));

        return 0;
    }

    public static void PrintSdks(Logger logger, Manifest manifest, string homePath)
    {
        logger.Log($"DNVM_HOME: {homePath}");
        logger.Log();
        logger.Log("Installed SDKs:");
        logger.Log();
        var table = new Table();
        table.AddColumn(new TableColumn(" "));
        table.AddColumn("Version");
        table.AddColumn("Channel");
        table.AddColumn("Location");
        foreach (var sdk in manifest.InstalledSdks)
        {
            string selected = manifest.CurrentSdkDir == sdk.SdkDirName ? "*" : " ";
            var channels = manifest.RegisteredChannels
                .Where(c => c.InstalledSdkVersions.Contains(sdk.SdkVersion))
                .Select(c => c.ChannelName.GetLowerName());
            table.AddRow(selected, sdk.SdkVersion.ToString(), string.Join(", ", channels), sdk.SdkDirName.Name);
        }
        logger.Console.Write(table);

        logger.Log();
        logger.Log("Tracked channels:");
        logger.Log();
        foreach (var c in manifest.TrackedChannels())
        {
            logger.Log($" • {c.ChannelName.GetLowerName()}");
        }
    }
}