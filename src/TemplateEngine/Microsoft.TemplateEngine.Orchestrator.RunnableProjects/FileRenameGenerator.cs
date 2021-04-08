using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    internal static class FileRenameGenerator
    {
        // Creates the complete file rename mapping for the template invocation being processed.
        // Renames are based on:
        //  - parameters with a FileRename specified
        //  - the source & target names.
        // Any input fileRenames will be applied before the parameter symbol renames.
        internal static IReadOnlyDictionary<string, string> AugmentFileRenames(
            IEngineEnvironmentSettings environmentSettings,
            string sourceName,
            IFileSystemInfo configFile,
            string sourceDirectory,
            ref string targetDirectory,
            object resolvedNameParamValue,
            IParameterSet parameterSet,
            Dictionary<string, string> fileRenames,
            IReadOnlyList<IReplacementTokens> symbolBasedFileRenames = null)
        {
            Dictionary<string, string> allRenames = new Dictionary<string, string>(StringComparer.Ordinal);

            IProcessor sourceRenameProcessor = SetupRenameProcessor(environmentSettings, fileRenames);
            IProcessor symbolRenameProcessor = SetupSymbolBasedRenameProcessor(environmentSettings, sourceName, ref targetDirectory, resolvedNameParamValue, parameterSet, symbolBasedFileRenames);

            IDirectory sourceBaseDirectoryInfo = configFile.Parent.Parent.DirectoryInfo(sourceDirectory.TrimEnd('/'));

            foreach (IFileSystemInfo fileSystemEntry in sourceBaseDirectoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                string sourceTemplateRelativePath = fileSystemEntry.PathRelativeTo(sourceBaseDirectoryInfo);

                // first apply the sources renames, then apply the symbol renames to that result.
                string renameFromSourcesValue = ApplyRenameProcessorToFilename(sourceRenameProcessor, sourceTemplateRelativePath);
                string renameFinalTargetValue = ApplyRenameProcessorToFilename(symbolRenameProcessor, renameFromSourcesValue);

                if (!string.Equals(sourceTemplateRelativePath, renameFinalTargetValue, StringComparison.Ordinal))
                {
                    allRenames[sourceTemplateRelativePath] = renameFinalTargetValue;
                }
            }

            return allRenames;
        }

        internal static string ApplyRenameToPrimaryOutput(
            string primaryOutputPath,
            IEngineEnvironmentSettings environmentSettings,
            string sourceName,
            object resolvedNameParamValue,
            IParameterSet parameterSet,
            IReadOnlyList<IReplacementTokens> symbolBasedFileRenames = null)
        {
            string targetDirectoryStub = string.Empty;
            IProcessor symbolRenameProcessor = SetupSymbolBasedRenameProcessor(environmentSettings, sourceName, ref targetDirectoryStub, resolvedNameParamValue, parameterSet, symbolBasedFileRenames);
            return ApplyRenameProcessorToFilename(symbolRenameProcessor, primaryOutputPath);
        }

        private static string ApplyRenameProcessorToFilename(IProcessor processor, string sourceFilename)
        {
            using (Stream source = new MemoryStream(Encoding.UTF8.GetBytes(sourceFilename)))
            using (Stream target = new MemoryStream())
            {
                processor.Run(source, target);

                byte[] targetData = new byte[target.Length];
                target.Position = 0;
                target.Read(targetData, 0, targetData.Length);
                return Encoding.UTF8.GetString(targetData);
            }
        }

        // Creates and returns the processor used to create the file rename mapping for source based file renames.
        private static IProcessor SetupRenameProcessor(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, string> substringReplacementMap)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();
            foreach (KeyValuePair<string, string> replacement in substringReplacementMap)
            {
                IOperationProvider replacementOperation = new Replacement(replacement.Key.TokenConfig(), replacement.Value, null, true);
                operations.Add(replacementOperation);
            }
            return SetupProcessor(environmentSettings, operations);
        }

        // Creates and returns the processor used to create the file rename mapping based on the symbols with fileRename defined.
        // Also sets up rename for the target directory.
        private static IProcessor SetupSymbolBasedRenameProcessor(
            IEngineEnvironmentSettings environmentSettings,
            string sourceName,
            ref string targetDirectory,
            object resolvedNameParamValue,
            IParameterSet parameterSet,
            IReadOnlyList<IReplacementTokens> symbolBasedFileRenames)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();
            SetupRenameForTargetDirectory(sourceName, resolvedNameParamValue, ref targetDirectory, operations);

            if (symbolBasedFileRenames != null)
            {
                foreach (IReplacementTokens fileRenameToken in symbolBasedFileRenames)
                {
                    if (parameterSet.TryGetRuntimeValue(environmentSettings, fileRenameToken.VariableName, out object value) && value is string valueString)
                    {
                        operations.Add(new Replacement(fileRenameToken.OriginalValue, valueString, null, true));
                    }
                }
            }
            return SetupProcessor(environmentSettings, operations);
        }

        // Sets up a rename for the target directory based on the "name" parameter.
        private static void SetupRenameForTargetDirectory(
            string sourceName,
            object resolvedNameParamValue,
            ref string targetDirectory,
            List<IOperationProvider> operations)
        {
            if (resolvedNameParamValue != null && sourceName != null)
            {
                string targetName = ((string)resolvedNameParamValue).Trim();
                targetDirectory = targetDirectory.Replace(sourceName, targetName);
                operations.Add(new Replacement(sourceName.TokenConfig(), targetName, null, true));
            }
        }

        private static IProcessor SetupProcessor(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IOperationProvider> operations)
        {
            IVariableCollection variables = new VariableCollection();
            EngineConfig config = new EngineConfig(environmentSettings, variables);
            IProcessor processor = Processor.Create(config, operations);
            return processor;
        }
    }
}
