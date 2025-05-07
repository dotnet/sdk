// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class PackagedCommandSpecFactory : IPackagedCommandSpecFactory
{
    private const string PackagedCommandSpecFactoryName = "packagedcommandspecfactory";

    private readonly Action<string, IList<string>> _addAdditionalArguments;

    internal PackagedCommandSpecFactory(Action<string, IList<string>> addAdditionalArguments = null)
    {
        _addAdditionalArguments = addAdditionalArguments;
    }

    public CommandSpec CreateCommandSpecFromLibrary(
        LockFileTargetLibrary toolLibrary,
        string commandName,
        IEnumerable<string> commandArguments,
        IEnumerable<string> allowedExtensions,
        LockFile lockFile,
        string depsFilePath,
        string runtimeConfigPath)
    {
        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.AttemptingToFindCommand,
            PackagedCommandSpecFactoryName,
            commandName,
            toolLibrary.Name));

        var toolAssembly = toolLibrary?.RuntimeAssemblies
                .FirstOrDefault(r => Path.GetFileNameWithoutExtension(r.Path) == commandName);

        if (toolAssembly == null)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.FailedToFindToolAssembly,
                PackagedCommandSpecFactoryName,
                commandName));

            return null;
        }

        var commandPath = GetCommandFilePath(lockFile, toolLibrary, toolAssembly);

        if (!File.Exists(commandPath))
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.FailedToFindCommandPath,
                PackagedCommandSpecFactoryName,
                commandPath));

            return null;
        }

        return CreateCommandSpecWrappingWithMuxerIfDll(
            commandPath,
            commandArguments,
            depsFilePath,
            lockFile.GetNormalizedPackageFolders(),
            runtimeConfigPath);
    }

    private static string GetCommandFilePath(
        LockFile lockFile,
        LockFileTargetLibrary toolLibrary,
        LockFileItem runtimeAssembly)
    {
        var packageDirectory = lockFile.GetPackageDirectory(toolLibrary);

        if (packageDirectory == null)
        {
            throw new GracefulException(string.Format(
                CliStrings.CommandAssembliesNotFound,
                toolLibrary.Name));
        }

        var filePath = Path.Combine(
            packageDirectory,
            PathUtility.GetPathWithDirectorySeparator(runtimeAssembly.Path));

        return filePath;
    }

    private CommandSpec CreateCommandSpecWrappingWithMuxerIfDll(
        string commandPath,
        IEnumerable<string> commandArguments,
        string depsFilePath,
        IEnumerable<string> packageFolders,
        string runtimeConfigPath)
    {
        var commandExtension = Path.GetExtension(commandPath);

        if (commandExtension == FileNameSuffixes.DotNet.DynamicLib)
        {
            return CreatePackageCommandSpecUsingMuxer(
                commandPath,
                commandArguments,
                depsFilePath,
                packageFolders,
                runtimeConfigPath);
        }

        return CreateCommandSpec(commandPath, commandArguments);
    }

    private CommandSpec CreatePackageCommandSpecUsingMuxer(
        string commandPath,
        IEnumerable<string> commandArguments,
        string depsFilePath,
        IEnumerable<string> packageFolders,
        string runtimeConfigPath)
    {
        var arguments = new List<string>();

        var muxer = new Muxer();

        string host = muxer.MuxerPath;
        if (host == null)
        {
            throw new Exception(LocalizableStrings.UnableToLocateDotnetMultiplexer);
        }

        arguments.Add("exec");

        if (runtimeConfigPath != null)
        {
            arguments.Add("--runtimeconfig");
            arguments.Add(runtimeConfigPath);
        }

        if (depsFilePath != null)
        {
            arguments.Add("--depsfile");
            arguments.Add(depsFilePath);
        }

        foreach (var packageFolder in packageFolders)
        {
            arguments.Add("--additionalprobingpath");
            arguments.Add(packageFolder);
        }

        if (_addAdditionalArguments != null)
        {
            _addAdditionalArguments(commandPath, arguments);
        }

        arguments.Add(commandPath);
        arguments.AddRange(commandArguments);

        return CreateCommandSpec(host, arguments);
    }

    private static CommandSpec CreateCommandSpec(
        string commandPath,
        IEnumerable<string> commandArguments)
    {
        var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(commandArguments);

        return new CommandSpec(commandPath, escapedArgs);
    }
}
