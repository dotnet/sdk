// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

internal sealed class CleanFileBasedAppArtifactsCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    public override int Execute()
    {
        bool dryRun = _parseResult.GetValue(CleanFileBasedAppArtifactsCommandParser.DryRunOption);

        using var metadataFileStream = OpenMetadataFile();

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
                    Reporter.Error.WriteLine(string.Format(CliCommandStrings.CleanFileBasedAppArtifactsErrorRemovingFolder, folder, ex.Message).Red());
                    anyErrors = true;
                }
            }
        }

        Reporter.Output.WriteLine(
            dryRun
                ? CliCommandStrings.CleanFileBasedAppArtifactsWouldRemoveFolders
                : CliCommandStrings.CleanFileBasedAppArtifactsTotalFoldersRemoved,
            count);

        if (!dryRun)
        {
            UpdateMetadata(metadataFileStream);
        }

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

    private static FileInfo GetMetadataFile()
    {
        return new FileInfo(VirtualProjectBuildingCommand.GetTempSubpath(RunFileArtifactsMetadata.FilePath));
    }

    private FileStream? OpenMetadataFile()
    {
        if (!_parseResult.GetValue(CleanFileBasedAppArtifactsCommandParser.AutomaticOption))
        {
            return null;
        }

        // Open and lock the metadata file to ensure we are the only automatic cleanup process.
        return GetMetadataFile().Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    }

    private static void UpdateMetadata(FileStream? metadataFileStream)
    {
        if (metadataFileStream is null)
        {
            return;
        }

        var metadata = new RunFileArtifactsMetadata
        {
            LastAutomaticCleanupUtc = DateTime.UtcNow,
        };
        JsonSerializer.Serialize(metadataFileStream, metadata, RunFileJsonSerializerContext.Default.RunFileArtifactsMetadata);
    }

    /// <summary>
    /// Starts a background process to clean up file-based app artifacts if needed.
    /// </summary>
    public static void StartAutomaticCleanupIfNeeded()
    {
        if (ShouldStartAutomaticCleanup())
        {
            Reporter.Verbose.WriteLine("Starting automatic cleanup of file-based app artifacts.");

            var startInfo = new ProcessStartInfo
            {
                FileName = new Muxer().MuxerPath,
                Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                [
                    CleanCommandParser.GetCommand().Name,
                    CleanFileBasedAppArtifactsCommandParser.Command.Name,
                    CleanFileBasedAppArtifactsCommandParser.AutomaticOption.Name,
                ]),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Process.Start(startInfo);
        }
    }

    private static bool ShouldStartAutomaticCleanup()
    {
        if (Env.GetEnvironmentVariableAsBool("DOTNET_CLI_DISABLE_FILE_BASED_APP_ARTIFACTS_AUTOMATIC_CLEANUP", defaultValue: false))
        {
            return false;
        }

        FileInfo? metadataFile = null;
        try
        {
            metadataFile = GetMetadataFile();

            if (!metadataFile.Exists)
            {
                return true;
            }

            using var stream = metadataFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var metadata = JsonSerializer.Deserialize(stream, RunFileJsonSerializerContext.Default.RunFileArtifactsMetadata);

            if (metadata?.LastAutomaticCleanupUtc is not { } timestamp)
            {
                return true;
            }

            // Start automatic cleanup every two days.
            return timestamp.AddDays(2) < DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Reporter.Verbose.WriteLine($"Cannot access artifacts metadata file '{metadataFile?.FullName}': {ex}");

            // If the file cannot be accessed, automatic cleanup might already be running.
            return false;
        }
    }
}

/// <summary>
/// Metadata stored at the root level of the file-based app artifacts directory.
/// </summary>
internal sealed class RunFileArtifactsMetadata
{
    public const string FilePath = "dotnet-run-file-artifacts-metadata.json";

    public DateTime? LastAutomaticCleanupUtc { get; init; }
}
