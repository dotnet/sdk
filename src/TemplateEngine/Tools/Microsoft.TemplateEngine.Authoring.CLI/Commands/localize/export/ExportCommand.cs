// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.TemplateLocalizer.Core;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands
{
    internal sealed class ExportCommand : ExecutableCommand<ExportCommandArgs>
    {
        private const string CommandName = "export";

        private readonly CliArgument<IEnumerable<string>> _templatePathArgument = new("template-path")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = LocalizableStrings.command_export_help_templatePath_description,
        };

        private readonly CliOption<IEnumerable<string>> _languageOption = new("--language", "-l")
        {
            Description = LocalizableStrings.command_export_help_language_description,
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly CliOption<bool> _recursiveOption = new("recursive", new[] { "--recursive", "-r" })
        {
            Description = LocalizableStrings.command_export_help_recursive_description,
        };

        private readonly CliOption<bool> _dryRunOption = new("--dry-run", "-d")
        {
            Description = LocalizableStrings.command_export_help_dryrun_description,
        };

        public ExportCommand()
            : base(CommandName, LocalizableStrings.command_export_help_description)
        {
            Arguments.Add(_templatePathArgument);
            Options.Add(_recursiveOption);
            Options.Add(_languageOption);
            Options.Add(_dryRunOption);
        }

        protected internal override ExportCommandArgs ParseContext(ParseResult parseResult)
        {
            return new ExportCommandArgs(
                templatePath: parseResult.GetValue(_templatePathArgument),
                language: parseResult.GetValue(_languageOption),
                recursive: parseResult.GetValue(_recursiveOption),
                dryRun: parseResult.GetValue(_dryRunOption));
        }

        protected override async Task<int> ExecuteAsync(ExportCommandArgs args, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ILogger logger = loggerFactory.CreateLogger<ExportCommand>();

            bool failed = false;
            List<string> templateJsonFiles = new();

            if (args.TemplatePaths == null || !args.TemplatePaths.Any())
            {
                // This shouldn't happen since command line parser will ensure that there is at least one path.
                logger.LogError(LocalizableStrings.generic_log_commandExecutionFailed, CommandName);
                return 1;
            }

            foreach (string templatePath in args.TemplatePaths)
            {
                int filesBeforeAdd = templateJsonFiles.Count;
                templateJsonFiles.AddRange(GetTemplateJsonFiles(templatePath, args.SearchSubdirectories));

                if (filesBeforeAdd == templateJsonFiles.Count)
                {
                    // No new files has been added by this path. This is an indication of a bad input.
                    logger.LogError(LocalizableStrings.command_export_log_templateJsonNotFound, templatePath);
                    failed = true;
                }
            }

            if (failed)
            {
                logger.LogError(LocalizableStrings.generic_log_commandExecutionFailed, CommandName);
                return 1;
            }

            List<ExportResult> exportResults = new();
            List<(string TemplateJsonPath, Task<ExportResult> Task)> runningExportTasks = new(templateJsonFiles.Count);

            foreach (string templateJsonPath in templateJsonFiles)
            {
                ExportOptions exportOptions = new(args.DryRun, targetDirectory: null, args.Languages);
                runningExportTasks.Add(
                    (templateJsonPath,
                    new TemplateLocalizer.Core.TemplateLocalizer(loggerFactory).ExportLocalizationFilesAsync(templateJsonPath, exportOptions, cancellationToken)));
            }

            try
            {
                _ = await Task.WhenAll(runningExportTasks.Select(t => t.Task)).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not TaskCanceledException)
            {
                // Task.WhenAll will only throw one of the exceptions. We need to log them all. Handle this outside of catch block.
            }
            cancellationToken.ThrowIfCancellationRequested();

            foreach ((string TemplateJsonPath, Task<ExportResult> Task) pathTaskPair in runningExportTasks)
            {
                if (pathTaskPair.Task.IsCanceled)
                {
                    logger.LogWarning(LocalizableStrings.command_export_log_cancelled, pathTaskPair.TemplateJsonPath);
                    continue;
                }
                else if (pathTaskPair.Task.IsFaulted)
                {
                    failed = true;
                    logger.LogError(pathTaskPair.Task.Exception, LocalizableStrings.command_export_log_templateExportFailedWithException, pathTaskPair.TemplateJsonPath);
                }
                else
                {
                    // Tasks is known to have already completed. We can get the result without await.
                    ExportResult result = pathTaskPair.Task.Result;
                    exportResults.Add(result);
                    failed |= !result.Succeeded;
                }
            }

            PrintResults(exportResults, logger);
            return (failed || cancellationToken.IsCancellationRequested) ? 1 : 0;
        }

        /// <summary>
        /// Given a <paramref name="path"/>, finds and returns all the template.json files. The search rules are executed in the following order:
        /// <list type="bullet">
        /// <item>If path points to a template.json file, it is directly returned.</item>
        /// <item>If path points to a template directory, path to the "&lt;directory&gt;/.template.config/template.json" file is returned.</item>
        /// <item>If path points to a "template.config" directory, path to the "&lt;directory&gt;/template.json" file is returned.</item>
        /// <item>If path points to any other directory and <paramref name="searchSubdirectories"/> is <see langword="true"/>, path to all the
        /// ".template.config/template.json" files under the given directory is returned.</item>
        /// </list>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchSubdirectories">Indicates weather the subdirectories should be searched
        /// in the case that <paramref name="path"/> points to a directory. This parameter has no effect
        /// if <paramref name="path"/> points to a file.</param>
        /// <returns>A path for each of the found "template.json" files.</returns>
        private static IEnumerable<string> GetTemplateJsonFiles(string path, bool searchSubdirectories)
        {
            if (string.IsNullOrEmpty(path))
            {
                yield break;
            }

            if (File.Exists(path))
            {
                yield return path;
                yield break;
            }

            if (!Directory.Exists(path))
            {
                // This path neither points to a file nor to a directory.
                yield break;
            }

            if (!searchSubdirectories)
            {
                string filePath = Path.Combine(path, ".template.config", "template.json");
                if (File.Exists(filePath))
                {
                    yield return filePath;
                }
                else
                {
                    filePath = Path.Combine(path, "template.json");
                    if (File.Exists(filePath))
                    {
                        yield return filePath;
                    }
                }

                yield break;
            }

            foreach (string filePath in Directory.EnumerateFiles(path, "template.json", SearchOption.AllDirectories))
            {
                string? directoryName = Path.GetFileName(Path.GetDirectoryName(filePath));
                if (directoryName == ".template.config")
                {
                    yield return filePath;
                }
            }
        }

        private void PrintResults(IReadOnlyList<ExportResult> results, ILogger logger)
        {
            using IDisposable? scope = logger.BeginScope("Results");
            logger.LogInformation(LocalizableStrings.command_export_log_executionEnded, results.Count);

            foreach (ExportResult result in results)
            {
                if (result.Succeeded)
                {
                    logger.LogInformation(LocalizableStrings.command_export_log_templateExportSucceeded, result.TemplateJsonPath);
                }
                else
                {
                    if (result.InnerException != null)
                    {
                        logger.LogError(result.InnerException, LocalizableStrings.command_export_log_templateExportFailedWithException, result.TemplateJsonPath);
                    }
                    else
                    {
                        logger.LogError(LocalizableStrings.command_export_log_templateExportFailedWithError, result.ErrorMessage, result.TemplateJsonPath);
                    }
                }
            }
        }
    }
}
