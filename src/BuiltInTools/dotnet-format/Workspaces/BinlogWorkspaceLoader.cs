// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal static class BinlogWorkspaceLoader
    {
        public static async Task<Workspace?> LoadAsync(
            string binlogPath,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            List<CscInvocation> invocations;
            try
            {
                invocations = BinlogParser.ExtractCscInvocations(binlogPath);
            }
            catch (Exception ex)
            {
                logger.LogError(Resources.Failed_to_read_binary_log_0_1, binlogPath, ex.Message);
                return null;
            }

            if (invocations.Count == 0)
            {
                logger.LogError(Resources.No_C_compiler_invocations_found_in_binary_log_0, binlogPath);
                return null;
            }

            logger.LogDebug(Resources.Found_0_C_compiler_invocations_in_binary_log, invocations.Count);

            var workspace = new AdhocWorkspace();

            foreach (var invocation in invocations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var parsedArgs = CSharpCommandLineParser.Default.Parse(
                    invocation.CommandLineArgs,
                    invocation.ProjectDirectory,
                    sdkDirectory: null);

                if (parsedArgs.Errors.Any())
                {
                    logger.LogWarning(Resources.Warning_Command_line_parse_errors_for_project_0, invocation.ProjectName);
                    foreach (var error in parsedArgs.Errors)
                    {
                        logger.LogDebug("  {0}", error.GetMessage());
                    }
                }

                var projectId = ProjectId.CreateNewId(invocation.ProjectName);

                var metadataReferences = new List<MetadataReference>();
                foreach (var reference in parsedArgs.MetadataReferences)
                {
                    if (File.Exists(reference.Reference))
                    {
                        metadataReferences.Add(MetadataReference.CreateFromFile(reference.Reference));
                    }
                    else
                    {
                        logger.LogDebug(Resources.Warning_Reference_not_found_0, reference.Reference);
                    }
                }

                var projectInfo = ProjectInfo.Create(
                    projectId,
                    VersionStamp.Default,
                    invocation.ProjectName,
                    invocation.ProjectName,
                    LanguageNames.CSharp,
                    compilationOptions: parsedArgs.CompilationOptions,
                    parseOptions: parsedArgs.ParseOptions,
                    metadataReferences: metadataReferences);

                var solution = workspace.CurrentSolution.AddProject(projectInfo);

                // Add analyzer config files (.editorconfig, globalconfig)
                foreach (var configPath in parsedArgs.AnalyzerConfigPaths)
                {
                    if (File.Exists(configPath))
                    {
                        var docId = DocumentId.CreateNewId(projectId, configPath);
                        solution = solution.AddAnalyzerConfigDocument(
                            docId,
                            Path.GetFileName(configPath),
                            SourceText.From(await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false)),
                            filePath: configPath);
                    }
                }

                // Add source files
                foreach (var sourceFile in parsedArgs.SourceFiles)
                {
                    var filePath = sourceFile.Path;
                    if (!File.Exists(filePath))
                    {
                        logger.LogDebug(Resources.Warning_Source_file_not_found_0, filePath);
                        continue;
                    }

                    var text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                    var documentId = DocumentId.CreateNewId(projectId, filePath);
                    solution = solution.AddDocument(
                        documentId,
                        Path.GetFileName(filePath),
                        SourceText.From(text),
                        filePath: filePath);
                }

                workspace.TryApplyChanges(solution);
            }

            return workspace;
        }
    }
}
