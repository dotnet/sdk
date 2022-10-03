// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class PrimaryOutput : ICreationPath
    {
        public PrimaryOutput(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new System.ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            }

            Path = path;
        }

        public string Path { get; }

        internal static IReadOnlyList<ICreationPath> Evaluate(
            IEngineEnvironmentSettings environmentSettings,
            IReadOnlyList<PrimaryOutputModel> modelList,
            IVariableCollection rootVariableCollection,
            string? sourceName,
            object? resolvedName,
            IReadOnlyList<IReplacementTokens>? filenameReplacements)
        {
            List<ICreationPath> pathList = new();

            rootVariableCollection ??= new VariableCollection();

            foreach (PrimaryOutputModel model in modelList)
            {
                model.EvaluateCondition(environmentSettings.Host.Logger, rootVariableCollection);
                if (!model.ConditionResult)
                {
                    // Condition on the primary output was evaluated to false. Don't include this primary output.
                    continue;
                }

                string resolvedPath = FileRenameGenerator.ApplyRenameToPrimaryOutput(
                                        model.Path,
                                        environmentSettings,
                                        sourceName,
                                        resolvedName,
                                        rootVariableCollection,
                                        filenameReplacements);

                ICreationPath path = new PrimaryOutput(resolvedPath);
                pathList.Add(path);

            }

            return pathList;
        }
    }
}
