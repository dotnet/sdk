// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class ShellShimRepository(
    DirectoryPath shimsDirectory,
    string appHostSourceDirectory,
    IFileSystem fileSystem = null,
    IAppHostShellShimMaker appHostShellShimMaker = null,
    IFilePermissionSetter filePermissionSetter = null) : IShellShimRepository
{
    private readonly DirectoryPath _shimsDirectory = shimsDirectory;
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystemWrapper();
    private readonly IAppHostShellShimMaker _appHostShellShimMaker = appHostShellShimMaker ?? new AppHostShellShimMaker(appHostSourceDirectory: appHostSourceDirectory);
    private readonly IFilePermissionSetter _filePermissionSetter = filePermissionSetter ?? new FilePermissionSetter();

    public void CreateShim(ToolCommand toolCommand, IReadOnlyList<FilePath> packagedShims = null)
    {
        if (string.IsNullOrEmpty(toolCommand.Executable.Value))
        {
            throw new ShellShimException(CliStrings.CannotCreateShimForEmptyExecutablePath);
        }

        if (ShimExists(toolCommand))
        {
            throw new ShellShimException(
                string.Format(
                    CliStrings.ShellShimConflict,
                    toolCommand.Name));
        }

        TransactionalAction.Run(
            action: () =>
            {
                try
                {
                    if (!_fileSystem.Directory.Exists(_shimsDirectory.Value))
                    {
                        _fileSystem.Directory.CreateDirectory(_shimsDirectory.Value);
                    }

                    if (TryGetPackagedShim(packagedShims, toolCommand.Name, out FilePath? packagedShim))
                    {
                        _fileSystem.File.Copy(packagedShim.Value.Value, GetShimPath(toolCommand.Name).Value);
                        _filePermissionSetter.SetUserExecutionPermission(GetShimPath(toolCommand.Name).Value);
                    }
                    else
                    {
                        _appHostShellShimMaker.CreateApphostShellShim(
                            toolCommand.Executable,
                            GetShimPath(toolCommand.Name));
                    }
                }
                catch (FilePermissionSettingException ex)
                {
                    throw new ShellShimException(
                            string.Format(CliStrings.FailedSettingShimPermissions, ex.Message));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    throw new ShellShimException(
                        string.Format(
                            CliStrings.FailedToCreateShellShim,
                            toolCommand.Name,
                            ex.Message
                        ),
                        ex);
                }
            },
            rollback: () =>
            {
                foreach (var file in GetShimFiles(toolCommand).Where(f => _fileSystem.File.Exists(f.Value)))
                {
                    File.Delete(file.Value);
                }
            });
    }

    public void RemoveShim(ToolCommand toolCommand)
    {
        var files = new Dictionary<string, string>();
        TransactionalAction.Run(
            action: () =>
            {
                try
                {
                    foreach (var file in GetShimFiles(toolCommand).Where(f => _fileSystem.File.Exists(f.Value)))
                    {
                        var tempPath = Path.Combine(_fileSystem.Directory.CreateTemporarySubdirectory(), Path.GetRandomFileName());
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => _fileSystem.File.Move(file.Value, tempPath));
                        files[file.Value] = tempPath;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    throw new ShellShimException(
                        string.Format(
                            CliStrings.FailedToRemoveShellShim,
                            toolCommand.Name.ToString(),
                            ex.Message
                        ),
                        ex);
                }
            },
            commit: () =>
            {
                foreach (var value in files.Values)
                {
                    _fileSystem.File.Delete(value);
                }
            },
            rollback: () =>
            {
                foreach (var kvp in files)
                {
                    FileAccessRetrier.RetryOnMoveAccessFailure(() => _fileSystem.File.Move(kvp.Value, kvp.Key));
                }
            });
    }

    private class StartupOptions
    {
        public string appRoot { get; set; }
    }

    private class RootObject
    {
        public StartupOptions startupOptions { get; set; }
    }

    private bool ShimExists(ToolCommand toolCommand)
    {
        return GetShimFiles(toolCommand).Any(p => _fileSystem.File.Exists(p.Value));
    }

    private IEnumerable<FilePath> GetShimFiles(ToolCommand toolCommand)
    {
        yield return GetShimPath(toolCommand);
    }

    private FilePath GetShimPath(ToolCommand toolCommand)
    {
        if (OperatingSystem.IsWindows())
        {
            return _shimsDirectory.WithFile(toolCommand.Name.Value + ".exe");
        }
        else
        {
            return _shimsDirectory.WithFile(toolCommand.Name.Value);
        }
    }

    private bool TryGetPackagedShim(
        IReadOnlyList<FilePath> packagedShims,
        ToolCommand toolCommand,
        out FilePath? packagedShim)
    {
        packagedShim = null;

        if (packagedShims != null && packagedShims.Count > 0)
        {
            FilePath[] candidatepackagedShim = [.. packagedShims.Where(s => string.Equals(Path.GetFileName(s.Value), Path.GetFileName(GetShimPath(toolCommand).Value)))];

            if (candidatepackagedShim.Length > 1)
            {
                throw new ShellShimException(
                    string.Format(
                        CliStrings.MoreThanOnePackagedShimAvailable,
                        string.Join(';', candidatepackagedShim)));
            }

            if (candidatepackagedShim.Length == 1)
            {
                packagedShim = candidatepackagedShim.Single();
                return true;
            }
        }

        return false;
    }
}
