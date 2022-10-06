// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    /// <summary>
    /// Base class for the macro defined via generated symbol.
    /// </summary>
    /// <typeparam name="T">The macro config.</typeparam>
#pragma warning disable CS0618 // Type or member is obsolete
    internal abstract class BaseGeneratedSymbolMacro<T> : BaseMacro<T>, IGeneratedSymbolMacro, IDeferredMacro where T : BaseMacroConfig, IMacroConfig
#pragma warning restore CS0618 // Type or member is obsolete
    {
        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not IGeneratedSymbolConfig deferredConfig)
            {
                throw new InvalidCastException($"Couldn't cast the {nameof(rawConfig)} as {nameof(IGeneratedSymbolConfig)}.");
            }
            return CreateConfig(deferredConfig);
        }

        public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IGeneratedSymbolConfig deferredConfig)
        {
            Evaluate(environmentSettings, vars, CreateConfig(deferredConfig));
        }

        protected abstract T CreateConfig(IGeneratedSymbolConfig deferredConfig);
    }
}
