// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IRunnableProjectConfig
    {
        /// <summary>
        /// Gets the list of <see cref="GlobalRunConfig"/> to be applied to specific files included in glob.
        /// </summary>
        IReadOnlyList<(string Glob, GlobalRunConfig RunConfig)> SpecialOperationConfig { get; }

        /// <summary>
        /// Gets the <see cref="GlobalRunConfig"/> to be applied to all template files.
        /// </summary>
        GlobalRunConfig GlobalOperationConfig { get; }

        /// <summary>
        /// Gets the list of evaluated sources based on configuration. <see cref="Evaluate(IVariableCollection)"/> method should be called first before accessing it.
        /// </summary>
        IReadOnlyList<FileSourceMatchInfo> EvaluatedSources { get; }

        IReadOnlyList<string> IgnoreFileNames { get; }

        /// <summary>
        /// Gets the list of enabled post actions. <see cref="Evaluate(IVariableCollection)"/> method should be called first before accessing it.
        /// </summary>
        IReadOnlyList<IPostAction> PostActions { get; }

        /// <summary>
        /// Gets the list of enabled primary outputs. <see cref="Evaluate(IVariableCollection)"/> method should be called first before accessing it.
        /// </summary>
        IReadOnlyList<ICreationPath> PrimaryOutputs { get; }

        /// <summary>
        /// Evaluates conditional elements based on <paramref name="rootVariableCollection"/>.
        /// </summary>
        /// <param name="rootVariableCollection"></param>
        void Evaluate(IVariableCollection rootVariableCollection);

        /// <summary>
        /// Removes parameter from template.
        /// </summary>
        /// <param name="parameter"></param>
        void RemoveParameter(ITemplateParameter parameter);

        Task EvaluateBindSymbolsAsync(IEngineEnvironmentSettings settings, IVariableCollection variableCollection, CancellationToken cancellationToken);
    }
}
