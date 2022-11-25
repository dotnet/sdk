// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal abstract class BaseMacro<T> : IMacro<T> where T : BaseMacroConfig, IMacroConfig
    {
        public abstract string Type { get; }

        public abstract Guid Id { get; }

        public abstract void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, T config);

        [Obsolete]
        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, IMacroConfig config)
        {
            if (config is not T castConfig)
            {
                throw new InvalidCastException($"Couldn't cast the {nameof(config)} as {typeof(T).Name}.");
            }
            Evaluate(environmentSettings, variableCollection, castConfig);
        }
    }
}
