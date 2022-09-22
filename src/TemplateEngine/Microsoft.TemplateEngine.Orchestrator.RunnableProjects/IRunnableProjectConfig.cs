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
        IReadOnlyList<KeyValuePair<string, IGlobalRunConfig>> SpecialOperationConfig { get; }

        IGlobalRunConfig OperationConfig { get; }

        IReadOnlyList<FileSourceMatchInfo> Sources { get; }

        string Identity { get; }

        IReadOnlyList<string> IgnoreFileNames { get; }

        IReadOnlyList<PostActionModel> PostActionModels { get; }

        IReadOnlyList<PrimaryOutputModel> PrimaryOutputs { get; }

        void Evaluate(IVariableCollection rootVariableCollection);

        Task EvaluateBindSymbolsAsync(IEngineEnvironmentSettings settings, IVariableCollection variableCollection, CancellationToken cancellationToken);
    }
}
