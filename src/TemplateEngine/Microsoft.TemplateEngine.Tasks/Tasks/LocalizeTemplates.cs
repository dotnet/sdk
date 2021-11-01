// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Tasks.Utilities;
using Microsoft.TemplateEngine.TemplateLocalizer.Core;

namespace Microsoft.TemplateEngine.Tasks
{
    /// <summary>
    /// A task that exposes template localization functionality of
    /// Microsoft.TemplateEngine.TemplateLocalizer through MSBuild.
    /// </summary>
    public sealed class LocalizeTemplates : Build.Utilities.Task, ICancelableTask
    {
        private volatile CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Gets or sets the path to the template to be localized.
        /// </summary>
        [Required]
        public string? TemplateFolder { get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether the subfolders
        /// should be searched for templates.
        /// </summary>
        [Required]
        public bool SearchSubfolders { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of supported languages for which
        /// localization files will be created.
        /// </summary>
        public string[]? Languages { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(TemplateFolder))
            {
                Log.LogError(LocalizableStrings.Command_Localize_Log_TemplateFolderNotSet);
                return false;
            }

            List<string> templateJsonFiles = GetTemplateJsonFiles(TemplateFolder!, SearchSubfolders).ToList();

            if (templateJsonFiles.Count == 0)
            {
                Log.LogError(LocalizableStrings.Command_Localize_Log_TemplateJsonNotFound, TemplateFolder);
                return false;
            }

            List<(string TemplateJsonPath, Task<ExportResult> Task)> runningExportTasks = new(templateJsonFiles.Count);

            using var loggerProvider = new MSBuildLoggerProvider(Log);
            ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
            using CancellationTokenSource cancellationTokenSource = GetOrCreateCancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            foreach (string templateJsonPath in templateJsonFiles)
            {
                ExportOptions exportOptions = new(dryRun: false, targetDirectory: null, Languages ?? ExportOptions.DefaultLanguages);
                Task<ExportResult> exportTask = new TemplateLocalizer.Core.TemplateLocalizer(msbuildLoggerFactory)
                    .ExportLocalizationFilesAsync(templateJsonPath, exportOptions, cancellationToken);
                runningExportTasks.Add((templateJsonPath, exportTask));
            }

            try
            {
                Task.WhenAll(runningExportTasks.Select(t => t.Task)).Wait();
            }
            catch (Exception)
            {
                // Task.WhenAll will only throw one of the exceptions. We need to log them all. Handle this outside of catch block.
            }

            bool failed = false;
            foreach ((string TemplateJsonPath, Task<ExportResult> Task) pathTaskPair in runningExportTasks)
            {
                if (pathTaskPair.Task.IsCanceled)
                {
                    Log.LogError(LocalizableStrings.Command_Localize_Log_FileProcessingCancelled, pathTaskPair.TemplateJsonPath);
                }
                else if (pathTaskPair.Task.IsFaulted)
                {
                    failed = true;
                    Log.LogErrorFromException(pathTaskPair.Task.Exception, showStackTrace: true, showDetail: true, pathTaskPair.TemplateJsonPath);
                }
                else
                {
                    // Tasks is known to have already completed. We can get the result without await.
                    ExportResult result = pathTaskPair.Task.Result;
                    if (!result.Succeeded)
                    {
                        Log.LogError("", "", "", result.TemplateJsonPath, 0, 0, 0, 0, result.ErrorMessage);
                    }
                    failed |= !result.Succeeded;
                }
            }

            return !failed && !cancellationToken.IsCancellationRequested;
        }

        public void Cancel() => GetOrCreateCancellationTokenSource().Cancel();

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
        /// <param name="path">Path to search for template.json files.</param>
        /// <param name="searchSubdirectories">Indicates weather the subdirectories should be searched
        /// in the case that <paramref name="path"/> points to a directory. This parameter has no effect
        /// if <paramref name="path"/> points to a file.</param>
        /// <returns>A path for each of the found "template.json" files.</returns>
        private IEnumerable<string> GetTemplateJsonFiles(string path, bool searchSubdirectories)
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

        private CancellationTokenSource GetOrCreateCancellationTokenSource()
        {
            if (_cancellationTokenSource != null)
            {
                return _cancellationTokenSource;
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            if (Interlocked.CompareExchange(ref _cancellationTokenSource, cts, null) != null)
            {
                // Reference was already set. This instance is not needed.
                cts.Dispose();
            }

            return _cancellationTokenSource;
        }
    }
}
