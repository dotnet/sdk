// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GuidMacro : BaseNondeterministicGenSymMacro<GuidMacroConfig>
    {
        internal const string MacroType = "guid";
        private static readonly Guid DeterministicModeValue = new("12345678-1234-1234-1234-1234567890AB");

        public override Guid Id { get; } = new Guid("10919008-4E13-4FA8-825C-3B4DA855578E");

        public override string Type => MacroType;

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, GuidMacroConfig config)
            => EvaluateInternal(Guid.NewGuid(), environmentSettings, vars, config);

        public override void EvaluateDeterministically(
            IEngineEnvironmentSettings environmentSettings,
            IVariableCollection variables,
            GuidMacroConfig config)
        {
            environmentSettings.Host.Logger.LogDebug("[{macro}]: deterministic mode enabled.", nameof(GuidMacro));
            EvaluateInternal(DeterministicModeValue, environmentSettings, variables, config);
        }

        public override GuidMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);

        private void EvaluateInternal(Guid g, IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, GuidMacroConfig config)
        {
            for (int i = 0; i < config.Format.Length; ++i)
            {
                bool isUpperCase = char.IsUpper(config.Format[i]);
                string value = g.ToString(config.Format[i].ToString());
                value = isUpperCase ? value.ToUpperInvariant() : value.ToLowerInvariant();

                // Not breaking any dependencies on exact param names and on the
                //  case insensitive matching of parameters (https://github.com/dotnet/templating/blob/7e14ef44/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/RunnableProjectGenerator.cs#L726)
                //  we need to introduce new parameters - with distinc naming for upper- and lower- casing replacements
                string legacyName = config.VariableName + "-" + config.Format[i];
                string newName = config.VariableName +
                        (isUpperCase ? GuidMacroConfig.UpperCaseDenominator : GuidMacroConfig.LowerCaseDenominator) +
                        config.Format[i];

                vars[legacyName] = value;
                vars[newName] = value;

                environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(GuidMacro), legacyName, value);
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(GuidMacro), newName, value);
            }

            string defaultValue = char.IsUpper(config.DefaultFormat[0]) ?
                g.ToString(config.DefaultFormat).ToUpperInvariant() :
                g.ToString(config.DefaultFormat).ToLowerInvariant();

            vars[config.VariableName] = defaultValue;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(GuidMacro), config.VariableName, defaultValue);
        }
    }
}
