// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    /// <summary>
    /// Base class for the macro defined via generated symbol.
    /// </summary>
    /// <typeparam name="T">The macro config.</typeparam>
#pragma warning disable CS0618 // Type or member is obsolete
    internal abstract class BaseGeneratedSymbolMacro<T> : BaseMacro<T>, IGeneratedSymbolMacro, IGeneratedSymbolMacro<T>, IDeferredMacro where T : BaseMacroConfig, IMacroConfig
#pragma warning restore CS0618 // Type or member is obsolete
    {
        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not IGeneratedSymbolConfig deferredConfig)
            {
                throw new InvalidCastException($"Couldn't cast the {nameof(rawConfig)} as {nameof(IGeneratedSymbolConfig)}.");
            }
            return CreateConfig(environmentSettings, deferredConfig);
        }

        public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IGeneratedSymbolConfig deferredConfig)
        {
            Evaluate(environmentSettings, vars, CreateConfig(environmentSettings, deferredConfig));
        }

        public abstract T CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig);

        IMacroConfig IGeneratedSymbolMacro.CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig) => CreateConfig(environmentSettings, generatedSymbolConfig);
    }

    /// <summary>
    /// Base class for the macro defined via generated symbol, that may run undeterministially and implements deterministic mode for the macro.
    /// Note: this class only implements deterministic mode for the macro as <see cref="IMacro{T}"/>. To implement deterministic mode for direct generated symbol evaluation, use <see cref="BaseNondeterministicGenSymMacro{T}"/>.
    /// </summary>
    /// <typeparam name="T">The macro config.</typeparam>
    internal abstract class BaseNondeterministicMacro<T> : BaseGeneratedSymbolMacro<T>, IDeterministicModeMacro<T> where T : BaseMacroConfig, IMacroConfig
    {
        public abstract void EvaluateDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, T config);
    }

    /// <summary>
    /// Base class for the macro defined via generated symbol, that may run undeterministially and implements deterministic mode for the macro as generated symbol.
    /// </summary>
    /// <typeparam name="T">The macro config.</typeparam>
    internal abstract class BaseNondeterministicGenSymMacro<T> : BaseNondeterministicMacro<T>, IDeterministicModeMacro<T>, IDeterministicModeMacro where T : BaseMacroConfig, IMacroConfig
    {
        //public void EvaluateDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig config) => EvaluateDeterministically(environmentSettings, variables, CreateConfig(environmentSettings, config));

        public void EvaluateConfigDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IMacroConfig config)
        {
            if (config is not T calculatedConfig)
            {
                throw new InvalidCastException($"Couldn't cast the {nameof(config)} as {typeof(T)}.");
            }
            EvaluateDeterministically(environmentSettings, variables, calculatedConfig);
        }
    }
}
