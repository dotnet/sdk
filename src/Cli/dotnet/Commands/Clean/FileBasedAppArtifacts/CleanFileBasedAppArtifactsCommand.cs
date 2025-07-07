// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

internal sealed class CleanFileBasedAppArtifactsCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    public override int Execute()
    {
        bool dryRun = _parseResult.GetValue(CleanFileBasedAppArtifactsCommandParser.DryRunOption);

        bool anyErrors = false;
        int count = 0;

        foreach (var folder in GetFoldersToRemove())
        {
            if (dryRun)
            {
                Reporter.Verbose.WriteLine($"Would remove folder: {folder.FullName}");
                count++;
            }
            else
            {
                try
                {
                    folder.Delete(recursive: true);
                    Reporter.Verbose.WriteLine($"Removed folder: {folder.FullName}");
                    count++;
                }
                catch (Exception ex)
                {
                    Reporter.Error.WriteLine(string.Join(CliCommandStrings.CleanFileBasedAppArtifactsErrorRemovingFolder, folder, ex.Message).Red());
                    anyErrors = true;
                }
            }
        }

        Reporter.Output.WriteLine(
            dryRun
                ? CliCommandStrings.CleanFileBasedAppArtifactsWouldRemoveFolders
                : CliCommandStrings.CleanFileBasedAppArtifactsTotalFoldersRemoved,
            count);

        return anyErrors ? 1 : 0;
    }

    private IEnumerable<DirectoryInfo> GetFoldersToRemove()
    {
        var directory = new DirectoryInfo(VirtualProjectBuildingCommand.GetTempSubdirectory());

        if (!directory.Exists)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CleanFileBasedAppArtifactsDirectoryNotFound, directory.FullName).Yellow());
            yield break;
        }

        Reporter.Output.WriteLine(CliCommandStrings.CleanFileBasedAppArtifactsScanning, directory.FullName);

        var days = _parseResult.GetValue(CleanFileBasedAppArtifactsCommandParser.DaysOption);
        var cutoff = DateTime.UtcNow.AddDays(-days);

        foreach (var subdir in directory.GetDirectories())
        {
            if (subdir.LastWriteTimeUtc < cutoff)
            {
                yield return subdir;
            }
        }
    }
}
