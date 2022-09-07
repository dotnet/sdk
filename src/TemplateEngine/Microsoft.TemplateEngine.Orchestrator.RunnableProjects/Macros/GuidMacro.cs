// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GuidMacro : BaseGeneratedSymbolMacro<GuidMacroConfig>
    {
        internal const string MacroType = "guid";

        public override Guid Id { get; } = new Guid("10919008-4E13-4FA8-825C-3B4DA855578E");

        public override string Type => MacroType;

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, GuidMacroConfig config)
        {
            Guid g = Guid.NewGuid();

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

        protected override GuidMacroConfig CreateConfig(IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);
    }
}
