// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class FileRenameGenerator
    {
        private readonly IProcessor _symbolRenameProcessor;

        internal FileRenameGenerator(
            IEngineEnvironmentSettings environmentSettings,
            string? sourceName,
            object? resolvedNameParamValue,
            IVariableCollection variables,
            IReadOnlyList<IReplacementTokens> symbolBasedFileRenames)
        {
            _symbolRenameProcessor = SetupSymbolBasedRenameProcessor(environmentSettings, sourceName, resolvedNameParamValue, variables, symbolBasedFileRenames);
        }

        /// <summary>
        /// Creates the complete file rename mapping for the template invocation being processed.
        /// Renames are based on:
        ///  - parameters with a FileRename specified
        ///  - the source and target names.
        /// Any input fileRenames will be applied before the parameter symbol renames.
        /// </summary>
        internal static IReadOnlyDictionary<string, string> AugmentFileRenames(
            IEngineEnvironmentSettings environmentSettings,
            string? sourceName,
            IDirectory templateSourceDir,
            string sourceDirectory,
            ref string targetDirectory,
            object? resolvedNameParamValue,
            IVariableCollection variables,
            Dictionary<string, string> fileRenames,
            IReadOnlyList<IReplacementTokens>? symbolBasedFileRenames = null)
        {
            Dictionary<string, string> allRenames = new(StringComparer.Ordinal);

            IProcessor sourceRenameProcessor = SetupSourceBasedRenameProcessor(environmentSettings, fileRenames);
            IProcessor symbolRenameProcessor = SetupSymbolBasedRenameProcessor(environmentSettings, sourceName, resolvedNameParamValue, variables, symbolBasedFileRenames);

            //replace sourceName in target directory, if needed
            if (sourceName is not null && resolvedNameParamValue is not null)
            {
                string targetName = resolvedNameParamValue.ToString().Trim();
                targetDirectory = targetDirectory.Replace(sourceName, targetName);
            }

            IDirectory? sourceBaseDirectoryInfo = templateSourceDir.DirectoryInfo(sourceDirectory.TrimEnd('/'));

            if (sourceBaseDirectoryInfo is null)
            {
                return allRenames;
            }

            foreach (IFileSystemInfo fileSystemEntry in sourceBaseDirectoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                string sourceTemplateRelativePath = fileSystemEntry.PathRelativeTo(sourceBaseDirectoryInfo);

                // first apply the sources renames, then apply the symbol renames to that result.
                string renameFromSourcesValue = ApplyRenameProcessor(sourceRenameProcessor, sourceTemplateRelativePath);
                string renameFinalTargetValue = ApplyRenameProcessor(symbolRenameProcessor, renameFromSourcesValue);

                if (!string.Equals(sourceTemplateRelativePath, renameFinalTargetValue, StringComparison.Ordinal))
                {
                    allRenames[sourceTemplateRelativePath] = renameFinalTargetValue;
                }
            }

            return allRenames;
        }

        /// <summary>
        /// Applies configured renames to <paramref name="stringToReplace"/>.
        /// </summary>
        internal string ApplyRenameToString(string stringToReplace) => ApplyRenameProcessor(_symbolRenameProcessor, stringToReplace);

        private static string ApplyRenameProcessor(IProcessor processor, string sourceFilename)
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes(sourceFilename));
            using MemoryStream target = new();

            _ = processor.Run(source, target);
            return Encoding.UTF8.GetString(target.ToArray());
        }

        /// <summary>
        /// Creates and returns the processor used to create the file rename mapping for source based file renames.
        /// </summary>
        private static IProcessor SetupSourceBasedRenameProcessor(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, string> substringReplacementMap)
        {
            List<IOperationProvider> operations = new();
            foreach (KeyValuePair<string, string> replacement in substringReplacementMap)
            {
                IOperationProvider replacementOperation = new Replacement(replacement.Key.TokenConfig(), replacement.Value, null, true);
                operations.Add(replacementOperation);
            }
            return SetupProcessor(environmentSettings, operations);
        }

        /// <summary>
        /// Creates and returns the processor used to create the file rename mapping based on the symbols with fileRename defined.
        /// Also sets up rename for the target directory.
        /// </summary>
        private static IProcessor SetupSymbolBasedRenameProcessor(
            IEngineEnvironmentSettings environmentSettings,
            string? sourceName,
            object? resolvedNameParamValue,
            IVariableCollection variables,
            IReadOnlyList<IReplacementTokens>? symbolBasedFileRenames)
        {
            List<IOperationProvider> operations = new();
            if (resolvedNameParamValue != null && sourceName != null)
            {
                SetupRenameForTargetDirectory(sourceName, resolvedNameParamValue, operations);
            }

            if (symbolBasedFileRenames != null)
            {
                foreach (IReplacementTokens fileRenameToken in symbolBasedFileRenames)
                {
                    if (variables.TryGetValue(fileRenameToken.VariableName, out object? newValueObject))
                    {
                        string newValue = newValueObject?.ToString() ?? string.Empty;
                        operations.Add(new Replacement(fileRenameToken.OriginalValue, newValue, null, true));
                    }
                }
            }
            return SetupProcessor(environmentSettings, operations);
        }

        /// <summary>
        /// Sets up a rename based on the "name" parameter.
        /// </summary>
        /// <param name="sourceName"></param>
        /// <param name="resolvedNameParamValue"></param>
        /// <param name="operations"></param>
        private static void SetupRenameForTargetDirectory(
            string sourceName,
            object resolvedNameParamValue,
            List<IOperationProvider> operations)
        {
            string targetName = ((string)resolvedNameParamValue).Trim();
            operations.Add(new Replacement(sourceName.TokenConfig(), targetName, null, true));
        }

        private static IProcessor SetupProcessor(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IOperationProvider> operations)
        {
            IVariableCollection variables = new VariableCollection();
            EngineConfig config = new(environmentSettings.Host.Logger, variables);
            IProcessor processor = Processor.Create(config, operations);
            return processor;
        }
    }
}
