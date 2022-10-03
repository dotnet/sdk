// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface IRunnableProjectConfig
    {
        /// <summary>
        /// Gets the list of <see cref="IGlobalRunConfig"/> to be applied to specific files included in glob.
        /// </summary>
        IReadOnlyList<(string Glob, IGlobalRunConfig RunConfig)> SpecialOperationConfig { get; }

        /// <summary>
        /// Gets the <see cref="IGlobalRunConfig"/> to be applied to all template files.
        /// </summary>
        IGlobalRunConfig GlobalOperationConfig { get; }

        /// <summary>
        /// Gets the list of evaluated sources based on configuration. <see cref="Evaluate(IVariableCollection)"/> method should be called first before accessing it.
        /// </summary>
        IReadOnlyList<FileSourceMatchInfo> EvaluatedSources { get; }

        IReadOnlyList<string> IgnoreFileNames { get; }

        IReadOnlyList<PostActionModel> PostActionModels { get; }

        IReadOnlyList<PrimaryOutputModel> PrimaryOutputs { get; }

        void Evaluate(IVariableCollection rootVariableCollection);

        Task EvaluateBindSymbolsAsync(IEngineEnvironmentSettings settings, IVariableCollection variableCollection, CancellationToken cancellationToken);
    }
}
