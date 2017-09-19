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
        public static void AugmentFileRenames(IEngineEnvironmentSettings environmentSettings, string sourceName, IFileSystemInfo configFile, string sourceDirectory, ref string targetDirectory, object resolvedNameParamValue, IParameterSet parameterSet, Dictionary<string, string> fileRenames)
        {
            IReadOnlyDictionary<string, string> substringRenames = SetupSubstringRenames(environmentSettings, sourceName, ref targetDirectory, resolvedNameParamValue, parameterSet);
            IProcessor processor = SetupRenameProcessor(environmentSettings, substringRenames, fileRenames);

            IDirectory sourceBaseDirectoryInfo = configFile.Parent.Parent.DirectoryInfo(sourceDirectory.TrimEnd('/'));

            foreach (IFileSystemInfo fileSystemEntry in sourceBaseDirectoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                string templateRelativePath = fileSystemEntry.PathRelativeTo(sourceBaseDirectoryInfo);
                string originalTargetPath = templateRelativePath;

                using (Stream source = new MemoryStream(Encoding.UTF8.GetBytes(templateRelativePath)))
                using (Stream target = new MemoryStream())
                {
                    processor.Run(source, target);

                    byte[] targetData = new byte[target.Length];
                    target.Position = 0;
                    target.Read(targetData, 0, targetData.Length);
                    string replacedTargetRelativePath = Encoding.UTF8.GetString(targetData);
                    
                    if (!string.Equals(originalTargetPath, replacedTargetRelativePath))
                    {
                        fileRenames[templateRelativePath] = replacedTargetRelativePath;
                    }
                }
            }
        }

        // Creates and returns the processor used to create the file rename mapping.
        private static IProcessor SetupRenameProcessor(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, string> substringReplacementMap, IReadOnlyDictionary<string, string> fileRenames)
        {
            List<IOperationProvider> operations = new List<IOperationProvider>();

            foreach (KeyValuePair<string, string> replacement in fileRenames)
            {
                IOperationProvider replacementOperation = new Replacement(replacement.Key.TokenConfig(), replacement.Value, null, true);
                operations.Add(replacementOperation);
            }

            foreach (KeyValuePair<string, string> replacement in substringReplacementMap)
            {
                IOperationProvider replacementOperation = new Replacement(replacement.Key.TokenConfig(), replacement.Value, null, true);
                operations.Add(replacementOperation);
            }

            IVariableCollection variables = new VariableCollection();
            EngineConfig config = new EngineConfig(environmentSettings, variables);
            IProcessor processor = Processor.Create(config, operations);
            return processor;
        }

        // Generates a mapping from source to target substrings in filenames, based on the parameters with FileRename defined.
        // Also sets up rename concerns for the target directory.
        private static IReadOnlyDictionary<string, string> SetupSubstringRenames(IEngineEnvironmentSettings environmentSettings, string sourceName, ref string targetDirectory, object resolvedNameParamValue, IParameterSet parameterSet)
        {
            Dictionary<string, string> substringRenames = new Dictionary<string, string>();

            SetupRenameForTargetDirectory(sourceName, resolvedNameParamValue, ref targetDirectory, substringRenames);

            foreach (IExtendedTemplateParameter parameter in parameterSet.ParameterDefinitions.OfType<IExtendedTemplateParameter>())
            {
                if (!string.IsNullOrEmpty(parameter.FileRename))
                {
                    if (parameterSet.TryGetRuntimeValue(environmentSettings, parameter.Name, out object value) && value is string valueString)
                    {
                        substringRenames.Add(parameter.FileRename, valueString);
                    }
                }
            }

            return substringRenames;
        }

        // Sets up a rename for the target directory based on the "name" parameter.
        private static void SetupRenameForTargetDirectory(string sourceName, object resolvedNameParamValue, ref string targetDirectory, Dictionary<string, string> substringRenames)
        {
            // setup the rename of the base directory to the output "name" param directory
            if (resolvedNameParamValue != null && sourceName != null)
            {
                string targetName = ((string)resolvedNameParamValue).Trim();
                targetDirectory = targetDirectory.Replace(sourceName, targetName);
                substringRenames.Add(sourceName, targetName);
            }
        }
    }
}
