// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli;

public static class Parser
{
    internal static RootCommand RootCommand { get; } = CreateCommand();

    private static RootCommand CreateCommand()
    {
        var versionOption = new Option<bool>("--version") { Description = "Display .NET SDK version." };
        var infoOption = new Option<bool>("--info") { Description = "Display .NET information." };

        var rootCommand = new RootCommand("The .NET CLI")
        {
            versionOption,
            infoOption,
        };

        rootCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(versionOption))
            {
                CommandLineInfo.PrintVersion();
                return 0;
            }
            if (parseResult.GetValue(infoOption))
            {
                CommandLineInfo.PrintInfo();
                return 0;
            }
            parseResult.InvocationConfiguration.Output.WriteLine("Usage: dn [options]");
            return 0;
        });

        ConfigureSolutionCommand(rootCommand);

        return rootCommand;
    }

    private static void ConfigureSolutionCommand(RootCommand rootCommand)
    {
        var slnCommand = new Command("solution", "Manage .NET solution files.");
        slnCommand.Aliases.Add("sln");

        // sln list
        var listSlnArg = new Argument<string>("SLN_FILE")
        {
            Description = "The solution file or directory to operate on. If not specified, the command searches the current directory.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var solutionFolderOption = new Option<bool>("--solution-folders") { Description = "Display solution folders." };
        var listCommand = new Command("list", "List all projects in a solution file.") { listSlnArg, solutionFolderOption };
        listCommand.SetAction(parseResult =>
        {
            try
            {
                string fileOrDirectory = parseResult.GetValue(listSlnArg) ?? Directory.GetCurrentDirectory();
                bool displaySolutionFolders = parseResult.GetValue(solutionFolderOption);
                string solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(fileOrDirectory, includeSolutionFilterFiles: true);
                SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);

                string[] paths;
                if (displaySolutionFolders)
                {
                    paths = [.. solution.SolutionFolders
                        .Select(folder => Path.GetDirectoryName(folder.Path.TrimStart('/')) ?? string.Empty)];
                }
                else
                {
                    paths = [.. solution.SolutionProjects.Select(project => project.FilePath)];
                }

                if (paths.Length == 0)
                {
                    Reporter.Output.WriteLine(CliStrings.NoProjectsFound);
                }
                else
                {
                    Array.Sort(paths);
                    string header = displaySolutionFolders ? "Solution Folder(s)" : "Project(s)";
                    Reporter.Output.WriteLine(header);
                    Reporter.Output.WriteLine(new string('-', header.Length));
                    foreach (string path in paths)
                    {
                        Reporter.Output.WriteLine(path);
                    }
                }
                return 0;
            }
            catch (GracefulException ex)
            {
                Reporter.Error.WriteLine(ex.Message.Red());
                return 1;
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(string.Format(CliStrings.InvalidSolutionFormatString, parseResult.GetValue(listSlnArg) ?? "", ex.Message).Red());
                return 1;
            }
        });

        // sln migrate
        var migrateSlnArg = new Argument<string>("SLN_FILE")
        {
            Description = "The solution file or directory to operate on. If not specified, the command searches the current directory.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var migrateCommand = new Command("migrate", "Migrate a solution file to the new slnx format.") { migrateSlnArg };
        migrateCommand.SetAction(parseResult =>
        {
            try
            {
                string fileOrDirectory = parseResult.GetValue(migrateSlnArg) ?? Directory.GetCurrentDirectory();
                string slnFileFullPath = SlnFileFactory.GetSolutionFileFullPath(fileOrDirectory);
                if (slnFileFullPath.HasExtension(".slnx"))
                {
                    Reporter.Error.WriteLine("The solution is already in the slnx format.".Red());
                    return 1;
                }
                string slnxFileFullPath = Path.ChangeExtension(slnFileFullPath, "slnx");
                SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(slnFileFullPath);
                SolutionSerializers.SlnXml.SaveAsync(slnxFileFullPath, solution, CancellationToken.None).Wait();
                Reporter.Output.WriteLine("The solution was migrated successfully.");
                Reporter.Output.WriteLine(slnxFileFullPath);
                return 0;
            }
            catch (GracefulException ex)
            {
                Reporter.Error.WriteLine(ex.Message.Red());
                return 1;
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(string.Format(CliStrings.InvalidSolutionFormatString, parseResult.GetValue(migrateSlnArg) ?? "", ex.Message).Red());
                return 1;
            }
        });

        // sln remove
        var removeSlnArg = new Argument<string>("SLN_FILE")
        {
            Description = "The solution file or directory to operate on. If not specified, the command searches the current directory.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var removeProjectsArg = new Argument<string[]>("PROJECT_PATH")
        {
            Description = "The paths to the projects to remove from the solution.",
            Arity = ArgumentArity.OneOrMore
        };
        var removeCommand = new Command("remove", "Remove one or more projects from a solution file.") { removeSlnArg, removeProjectsArg };
        removeCommand.SetAction(parseResult =>
        {
            try
            {
                string fileOrDirectory = parseResult.GetValue(removeSlnArg) ?? Directory.GetCurrentDirectory();
                string[] projects = parseResult.GetValue(removeProjectsArg) ?? [];
                string solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(fileOrDirectory, includeSolutionFilterFiles: true);

                if (projects.Length == 0)
                {
                    Reporter.Error.WriteLine("You must specify at least one project to remove.".Red());
                    return 1;
                }

                var relativeProjectPaths = projects
                    .Select(p => Path.GetFullPath(p))
                    .Select(p => Path.GetRelativePath(
                        Path.GetDirectoryName(solutionFileFullPath)!,
                        Directory.Exists(p)
                            ? GetProjectFileFromDirectory(p)
                            : p));

                if (solutionFileFullPath.HasExtension(SlnfFileHelper.SlnfExtension))
                {
                    RemoveProjectsFromSolutionFilter(solutionFileFullPath, relativeProjectPaths);
                }
                else
                {
                    RemoveProjectsFromSolution(solutionFileFullPath, relativeProjectPaths);
                }
                return 0;
            }
            catch (GracefulException ex)
            {
                Reporter.Error.WriteLine(ex.Message.Red());
                return 1;
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(string.Format(CliStrings.InvalidSolutionFormatString, parseResult.GetValue(removeSlnArg) ?? "", ex.Message).Red());
                return 1;
            }
        });

        slnCommand.Subcommands.Add(listCommand);
        slnCommand.Subcommands.Add(migrateCommand);
        slnCommand.Subcommands.Add(removeCommand);

        slnCommand.SetAction(parseResult =>
        {
            Reporter.Output.WriteLine("Required command was not provided.");
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine("Usage: dotnet solution [command] [options]");
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine("Commands:");
            Reporter.Output.WriteLine("  list       List all projects in a solution file.");
            Reporter.Output.WriteLine("  migrate    Migrate a solution file to the new slnx format.");
            Reporter.Output.WriteLine("  remove     Remove one or more projects from a solution file.");
            return 1;
        });

        rootCommand.Subcommands.Add(slnCommand);
    }

    public static ParseResult Parse(string[] args) => RootCommand.Parse(args);

    public static int Invoke(ParseResult parseResult) => parseResult.Invoke();

    /// <summary>
    /// Find a single *proj file in the given directory.
    /// </summary>
    private static string GetProjectFileFromDirectory(string projectDirectory)
    {
        DirectoryInfo dir;
        try
        {
            dir = new DirectoryInfo(projectDirectory);
        }
        catch (ArgumentException)
        {
            throw new GracefulException(string.Format("Could not find a project or directory `{0}`.", projectDirectory));
        }

        if (!dir.Exists)
        {
            throw new GracefulException(string.Format("Could not find a project or directory `{0}`.", projectDirectory));
        }

        FileInfo[] files = dir.GetFiles("*proj");
        if (files.Length == 0)
        {
            throw new GracefulException(string.Format("Could not find any project in `{0}`.", projectDirectory));
        }

        if (files.Length > 1)
        {
            throw new GracefulException(string.Format("Found more than one project in `{0}`. Specify which one to use.", projectDirectory));
        }

        return files[0].FullName;
    }

    private static void RemoveProjectsFromSolution(string solutionFileFullPath, IEnumerable<string> projectPaths)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);
        ISolutionSerializer serializer = solution.SerializerExtension!.Serializer;

        // set UTF-8 BOM encoding for .sln
        if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
        {
            solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
            {
                Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            });
        }

        foreach (var projectPath in projectPaths)
        {
            var project = solution.FindProject(projectPath);
            // If the project is not found, try to find it by name without extension
            if (project is null && !Path.HasExtension(projectPath))
            {
                var projectsMatchByName = solution.SolutionProjects.Where(p => Path.GetFileNameWithoutExtension(p.DisplayName)?.Equals(projectPath) == true);
                project = projectsMatchByName.Count() == 1 ? projectsMatchByName.First() : null;
            }
            if (project is null)
            {
                Reporter.Output.WriteLine(CliStrings.ProjectNotFoundInTheSolution, projectPath);
            }
            else
            {
                solution.RemoveProject(project);
                Reporter.Output.WriteLine("Project `{0}` removed from the solution.", projectPath);
            }
        }

        // Remove empty solution folders
        for (int i = 0; i < solution.SolutionFolders.Count; i++)
        {
            var folder = solution.SolutionFolders[i];
            int nonFolderDescendants = 0;
            Stack<SolutionFolderModel> stack = new();
            stack.Push(folder);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                nonFolderDescendants += current.Files?.Count ?? 0;
                foreach (var child in solution.SolutionItems)
                {
                    if (child is { Parent: var parent } && parent == current)
                    {
                        if (child is SolutionFolderModel childFolder)
                        {
                            stack.Push(childFolder);
                        }
                        else
                        {
                            nonFolderDescendants++;
                        }
                    }
                }
            }

            if (nonFolderDescendants == 0)
            {
                solution.RemoveFolder(folder);
                i--;
            }
        }

        serializer.SaveAsync(solutionFileFullPath, solution, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void RemoveProjectsFromSolutionFilter(string slnfFileFullPath, IEnumerable<string> projectPaths)
    {
        SolutionModel filteredSolution = SlnFileFactory.CreateFromFilteredSolutionFile(slnfFileFullPath);
        string parentSolutionPath = filteredSolution.Description!;

        var existingProjects = filteredSolution.SolutionProjects.Select(p => p.FilePath).ToHashSet();

        foreach (var projectPath in projectPaths)
        {
            if (existingProjects.Remove(projectPath))
            {
                Reporter.Output.WriteLine("Project `{0}` removed from the solution.", projectPath);
            }
            else
            {
                Reporter.Output.WriteLine(CliStrings.ProjectNotFoundInTheSolution, projectPath);
            }
        }

        SlnfFileHelper.SaveSolutionFilter(slnfFileFullPath, parentSolutionPath, existingProjects.OrderBy(p => p));
    }
}

#else
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.StaticCompletions;
using System.Reflection;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.BuildServer;
using Microsoft.DotNet.Cli.Commands.Clean;
using Microsoft.DotNet.Cli.Commands.Dnx;
using Microsoft.DotNet.Cli.Commands.Format;
using Microsoft.DotNet.Cli.Commands.Fsi;
using Microsoft.DotNet.Cli.Commands.Help;
using Microsoft.DotNet.Cli.Commands.Hidden.Add;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Complete;
using Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Commands.Hidden.Parse;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Pack;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Project;
using Microsoft.DotNet.Cli.Commands.Publish;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.Api;
using Microsoft.DotNet.Cli.Commands.Sdk;
using Microsoft.DotNet.Cli.Commands.Solution;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Tool;
using Microsoft.DotNet.Cli.Commands.Tool.Store;
using Microsoft.DotNet.Cli.Commands.VSTest;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Commands.Workload.Search;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Help;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.TemplateEngine.Cli;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli;

public static class Parser
{
    private static DotNetCommandDefinition CreateCommand()
    {
        var rootCommand = new DotNetCommandDefinition();

        for (int i = rootCommand.Options.Count - 1; i >= 0; i--)
        {
            Option option = rootCommand.Options[i];

            if (option is VersionOption)
            {
                rootCommand.Options.RemoveAt(i);
            }
            else if (option is HelpOption helpOption)
            {
                helpOption.Action = new PrintHelpAction(helpOption, DotnetHelpBuilder.Instance.Value);
                option.Description = CliStrings.ShowHelpDescription;
            }
        }

        // Augment the definition of each subcommand with command-specific actions and completions.
        AddCommandParser.ConfigureCommand(rootCommand.AddCommand);
        BuildCommandParser.ConfigureCommand(rootCommand.BuildCommand);
        BuildServerCommandParser.ConfigureCommand(rootCommand.BuildServerCommand);
        CleanCommandParser.ConfigureCommand(rootCommand.CleanCommand);
        DnxCommandParser.ConfigureCommand(rootCommand.DnxCommand);
        FormatCommandParser.ConfigureCommand(rootCommand.FormatCommand);
        CompleteCommandParser.ConfigureCommand(rootCommand.CompleteCommand);
        FsiCommandParser.ConfigureCommand(rootCommand.FsiCommand);
        ListCommandParser.ConfigureCommand(rootCommand.ListCommand);
        MSBuildCommandParser.ConfigureCommand(rootCommand.MSBuildCommand);

        // Currently `new` command implementation replaces the definition entirely:
        rootCommand.Subcommands[rootCommand.Subcommands.IndexOf(rootCommand.NewCommand)] = NewCommandParser.ConfigureCommand(rootCommand.NewCommand);

        // TODO: https://github.com/dotnet/sdk/issues/52661
        // https://github.com/NuGet/NuGet.Client/blob/bf048eb714eb6b1912ba868edca4c7cfec454841/src/NuGet.Core/NuGet.CommandLine.XPlat/Commands/Why/WhyCommand.cs
        // Add `why` subcommand to the definition instead.
        var nugetCommand = rootCommand.NuGetCommand;
        NuGet.CommandLine.XPlat.Commands.Why.WhyCommand.GetWhyCommand(nugetCommand, NuGetVirtualProjectBuilder.Instance);

        NuGetCommandParser.ConfigureCommand(nugetCommand);

        PackCommandParser.ConfigureCommand(rootCommand.PackCommand);
        PackageCommandParser.ConfigureCommand(rootCommand.PackageCommand);
        ParseCommandParser.ConfigureCommand(rootCommand.ParseCommand);
        ProjectCommandParser.ConfigureCommand(rootCommand.ProjectCommand);
        PublishCommandParser.ConfigureCommand(rootCommand.PublishCommand);
        ReferenceCommandParser.ConfigureCommand(rootCommand.ReferenceCommand);
        RemoveCommandParser.ConfigureCommand(rootCommand.RemoveCommand);
        RestoreCommandParser.ConfigureCommand(rootCommand.RestoreCommand);
        RunCommandParser.ConfigureCommand(rootCommand.RunCommand);
        RunApiCommandParser.ConfigureCommand(rootCommand.RunApiCommand);
        SolutionCommandParser.ConfigureCommand(rootCommand.SolutionCommand);
        StoreCommandParser.ConfigureCommand(rootCommand.StoreCommand);
        TestCommandParser.ConfigureCommand(rootCommand.TestCommand);
        ToolCommandParser.ConfigureCommand(rootCommand.ToolCommand);
        VSTestCommandParser.ConfigureCommand(rootCommand.VSTestCommand);
        HelpCommandParser.ConfigureCommand(rootCommand.HelpCommand);
        SdkCommandParser.ConfigureCommand(rootCommand.SdkCommand);
        InternalReportInstallSuccessCommandParser.ConfigureCommand(rootCommand.InternalReportInstallSuccessCommand);
        WorkloadCommandParser.ConfigureCommand(rootCommand.WorkloadCommand);
        CompletionsCommandParser.ConfigureCommand(rootCommand.CompletionsCommand);

        rootCommand.DiagOption.Action = new HandleDiagnosticAction(rootCommand.DiagOption);
        rootCommand.VersionOption.Action = new PrintVersionAction(rootCommand.VersionOption);
        rootCommand.InfoOption.Action = new PrintInfoAction(rootCommand.InfoOption);
        rootCommand.CliSchemaOption.Action = new PrintCliSchemaAction(rootCommand.CliSchemaOption);

        // TODO: https://github.com/dotnet/sdk/issues/52661
        // https://github.com/NuGet/NuGet.Client/blob/bf048eb714eb6b1912ba868edca4c7cfec454841/src/NuGet.Core/NuGet.CommandLine.XPlat/NuGetCommands.cs
        // Add `package` subcommands to the definition instead.
        NuGet.CommandLine.XPlat.NuGetCommands.Add(rootCommand, CommonOptions.CreateInteractiveOption(acceptArgument: true), NuGetVirtualProjectBuilder.Instance);

        rootCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(rootCommand.DiagOption) && parseResult.Tokens.Count == 1)
            {
                // When user does not specify any args except of diagnostics ("dotnet -d"),
                // we do nothing as HandleDiagnosticAction already enabled the diagnostic output.
                return 0;
            }
            else
            {
                // When user does not specify any args (just "dotnet"), a usage needs to be printed.
                parseResult.InvocationConfiguration.Output.WriteLine(CliUsage.HelpText);
                return 0;
            }
        });

        return rootCommand;
    }

    public static Command? GetBuiltInCommand(string commandName) =>
        RootCommand.Subcommands.FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Implements token-per-line response file handling for the CLI. We use this instead of the built-in S.CL handling
    /// to ensure backwards-compatibility with MSBuild.
    /// </summary>
    public static bool TokenPerLine(string tokenToReplace, out IReadOnlyList<string>? replacementTokens, out string? errorMessage)
    {
        var filePath = Path.GetFullPath(tokenToReplace);
        if (File.Exists(filePath))
        {
            var lines = File.ReadAllLines(filePath);
            var trimmedLines =
                lines
                    // Remove content in the lines that start with # after trimmer leading whitespace
                    .Select(line => line.TrimStart().StartsWith('#') ? string.Empty : line)
                    // trim leading/trailing whitespace to not pass along dead spaces
                    .Select(x => x.Trim())
                    // Remove empty lines
                    .Where(line => line.Length > 0);
            replacementTokens = [.. trimmedLines];
            errorMessage = null;
            return true;
        }
        else
        {
            replacementTokens = null;
            errorMessage = string.Format(CliStrings.ResponseFileNotFound, tokenToReplace);
            return false;
        }
    }

    public static ParserConfiguration ParserConfiguration { get; } = new()
    {
        EnablePosixBundling = false,
        ResponseFileTokenReplacer = TokenPerLine
    };

    public static InvocationConfiguration InvocationConfiguration { get; } = new()
    {
        EnableDefaultExceptionHandler = false,
    };

    /// <summary>
    /// The root command for the .NET CLI.
    /// </summary>
    /// <remarks>
    /// If you use this Command directly, you _must_ use <see cref="ParserConfiguration"/>
    /// and <see cref="InvocationConfiguration"/> to ensure that the command line parser
    /// and invoker are configured correctly.
    /// </remarks>
    internal static DotNetCommandDefinition RootCommand { get; } = CreateCommand();

    /// <summary>
    /// You probably want to use <see cref="Parse(string[])"/> instead of this method.
    /// This has to internally split the string into an array of arguments
    /// before parsing, which is not as efficient as using the array overload.
    /// And also won't always split tokens the way the user will expect on their shell.
    /// </summary>
    public static ParseResult Parse(string commandLineUnsplit) => RootCommand.Parse(commandLineUnsplit, ParserConfiguration);
    public static ParseResult Parse(string[] args) => RootCommand.Parse(args, ParserConfiguration);
    public static int Invoke(ParseResult parseResult) => parseResult.Invoke(InvocationConfiguration);
    public static Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default) => parseResult.InvokeAsync(InvocationConfiguration, cancellationToken);
    public static int Invoke(string[] args) => Invoke(Parse(args));
    public static Task<int> InvokeAsync(string[] args, CancellationToken cancellationToken = default) => InvokeAsync(Parse(args), cancellationToken);

    internal static int ExceptionHandler(Exception? exception, ParseResult parseResult)
    {
        if (exception is TargetInvocationException)
        {
            exception = exception.InnerException;
        }

        if (exception is GracefulException)
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
        }
        else if (exception is CommandParsingException)
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
            parseResult.ShowHelp();
        }
        else if (exception is not null && exception.GetType().Name.Equals("WorkloadManifestCompositionException"))
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
        }
        else if (exception is not null)
        {
            Reporter.Error.Write("Unhandled exception: ".Red().Bold());
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose ?
                exception.ToString().Red().Bold() :
                exception.Message.Red().Bold());
        }

        return 1;
    }

    internal class DotnetHelpBuilder : HelpBuilder
    {
        private DotnetHelpBuilder(int maxWidth = int.MaxValue) : base(maxWidth) { }

        public static Lazy<HelpBuilder> Instance = new(() =>
        {
            int windowWidth;
            try
            {
                windowWidth = Console.WindowWidth;
            }
            catch
            {
                windowWidth = int.MaxValue;
            }

            DotnetHelpBuilder dotnetHelpBuilder = new(windowWidth);

            return dotnetHelpBuilder;
        });

        public static void additionalOption(HelpContext context)
        {
            List<TwoColumnHelpRow> options = [];
            HashSet<Option> uniqueOptions = [];
            foreach (Option option in context.Command.Options)
            {
                if (!option.Hidden && uniqueOptions.Add(option))
                {
                    options.Add(context.HelpBuilder.GetTwoColumnRow(option, context));
                }
            }

            if (options.Count <= 0)
            {
                return;
            }

            context.Output.WriteLine(CliStrings.MSBuildAdditionalOptionTitle);
            context.HelpBuilder.WriteColumns(options, context);
            context.Output.WriteLine();
        }

        public override void Write(HelpContext context)
        {
            var command = context.Command;
            var helpArgs = new string[] { "--help" };

            // custom help overrides
            if (command.Equals(RootCommand))
            {
                Console.Out.WriteLine(CliUsage.HelpText);
                return;
            }

            // argument/option cleanups specific to help
            foreach (var option in command.Options)
            {
                option.EnsureHelpName();
            }

            if (IsInNuGetCommandTree(command))
            {
                NuGetCommand.Run(context.ParseResult);
            }
            else if (command is MSBuildCommandDefinition)
            {
                new MSBuildForwardingApp(MSBuildArgs.ForHelp).Execute();
                context.Output.WriteLine();
                additionalOption(context);
            }
            else if (command is VSTestCommandDefinition)
            {
                new VSTestForwardingApp(helpArgs).Execute();
            }
            else if (command is FormatCommandDefinition format)
            {
                var arguments = context.ParseResult.GetValue(format.Arguments) ?? [];
                new FormatForwardingApp([.. arguments, .. helpArgs]).Execute();
            }
            else if (command is FsiCommandDefinition)
            {
                new FsiForwardingApp(helpArgs).Execute();
            }
            else if (command is ICustomHelp helpCommand)
            {
                var blocks = helpCommand.CustomHelpLayout();
                foreach (var block in blocks)
                {
                    block(context);
                }
            }
            else
            {
                // TODO: avoid modifying the commands:
                // https://github.com/dotnet/sdk/issues/52136

                if (command.Name.Equals(ListReferenceCommandDefinition.Name))
                {
                    Command? listCommand = command.Parents.Single() as Command;
                    if (listCommand is not null)
                    {
                        for (int i = 0; i < listCommand.Arguments.Count; i++)
                        {
                            if (listCommand.Arguments[i].Name == CliStrings.SolutionOrProjectArgumentName)
                            {
                                // Name is immutable now, so we create a new Argument with the right name..
                                listCommand.Arguments[i] = ListCommandDefinition.CreateSlnOrProjectArgument(CliStrings.ProjectArgumentName, CliStrings.ProjectArgumentDescription);
                            }
                        }
                    }
                }
                else if (command.Name.Equals(AddPackageCommandDefinition.Name) || command.Name.Equals(AddCommandDefinition.Name))
                {
                    // Don't show package completions in help
                    foreach (var argument in command.Arguments)
                    {
                        argument.CompletionSources.Clear();
                    }
                }
                else if (command is WorkloadSearchCommandDefinition workloadSearchCommand)
                {
                    // Set shorter description for displaying parent command help.
                    workloadSearchCommand.VersionCommand.Description = CliStrings.ShortWorkloadSearchVersionDescription;
                }

                base.Write(context);
            }
        }

        private static bool IsInNuGetCommandTree(Command command)
        {
            Command? current = command;
            while (current is not null)
            {
                if (current is NuGetCommandDefinition)
                {
                    return true;
                }
                current = current.Parents.FirstOrDefault(p => p is Command) as Command;
            }
            return false;
        }
    }
}
#endif
