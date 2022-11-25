// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal abstract class BaseMacroConfig : IMacroConfig
    {
        protected BaseMacroConfig(string type, string variableName, string? dataType = null)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
            }
            Type = type;
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException($"'{nameof(variableName)}' cannot be null or whitespace.", nameof(variableName));
            }
            VariableName = variableName;
            if (!string.IsNullOrWhiteSpace(dataType))
            {
                DataType = dataType!;
            }
        }

        public string VariableName { get; }

        public string Type { get; }

        internal string DataType { get; } = "string";

        internal abstract void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars);

        internal virtual void EvaluateDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars) => Evaluate(environmentSettings, vars);
    }

    internal abstract class BaseMacroConfig<TMacro, TMacroConfig> : BaseMacroConfig, IMacroConfig
        where TMacro : IMacro<TMacroConfig>
        where TMacroConfig : BaseMacroConfig<TMacro, TMacroConfig>, IMacroConfig
    {
        protected BaseMacroConfig(TMacro macro, string variableName, string? dataType = null)
            : base(macro.Type, variableName, dataType)
        {
            Macro = macro ?? throw new ArgumentNullException(nameof(macro));
        }

        internal TMacro Macro { get; }

        internal override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars)
        {
            Macro.Evaluate(environmentSettings, vars, (TMacroConfig)this);
        }

        internal override void EvaluateDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars)
        {
            if (Macro is IDeterministicModeMacro<TMacroConfig> deterministicMacro)
            {
                deterministicMacro.EvaluateDeterministically(environmentSettings, vars, (TMacroConfig)this);
            }
            else
            {
                Evaluate(environmentSettings, vars);
            }
        }

        protected static string? GetOptionalParameterValue(IGeneratedSymbolConfig config, string parameterName, string? defaultValue = default)
        {
            return GetOptionalParameterValue(config, parameterName, ConvertJTokenToString, defaultValue);
        }

        protected static TVal? GetOptionalParameterValue<TVal>(IGeneratedSymbolConfig config, string parameterName, Func<string, IGeneratedSymbolConfig, string, TVal> converter, TVal? defaultValue = default)
        {
            if (config.Parameters.TryGetValue(parameterName, out string token))
            {
                return converter(token, config, parameterName);
            }
            return defaultValue;
        }

        protected static bool ConvertJTokenToBool(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken.TryParseBool(out bool result))
                {
                    return result;
                }
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_ValueShouldBeBoolean, config.VariableName, parameterName), config.VariableName);
            }
            catch (Exception ex) when (ex is not TemplateAuthoringException)
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_InvalidJSON, config.VariableName, parameterName), config.VariableName, ex);
            }
        }

        protected static int ConvertJTokenToInt(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken.TryParseInt(out int result))
                {
                    return result;
                }
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_ValueShouldBeInteger, config.VariableName, parameterName), config.VariableName);
            }
            catch (Exception ex) when (ex is not TemplateAuthoringException)
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_InvalidJSON, config.VariableName, parameterName), config.VariableName, ex);
            }
        }

        protected static string ConvertJTokenToString(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken is not JValue val)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_ValueShouldBeString, config.VariableName, parameterName), config.VariableName);
                }
                return val.ToString();
            }
            catch (Exception ex) when (ex is not TemplateAuthoringException)
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_InvalidJSON, config.VariableName, parameterName), config.VariableName, ex);
            }
        }

        protected static JArray ConvertJTokenToJArray(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken.Type != JTokenType.Array)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_ValueShouldBeArray, config.VariableName, parameterName), config.VariableName);
                }
                return (JArray)jToken;
            }
            catch (Exception ex) when (ex is not TemplateAuthoringException)
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_InvalidJSON, config.VariableName, parameterName), config.VariableName, ex);
            }
        }

        protected static void IsValidRegex(string regex, IGeneratedSymbolConfig? generatedSymbolConfig = null)
        {
            try
            {
                Regex.Match(string.Empty, regex);
            }
            catch (ArgumentException)
            {
                if (generatedSymbolConfig is not null)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_InvalidRegex, generatedSymbolConfig.VariableName, regex), generatedSymbolConfig.VariableName);
                }
                else
                {
                    throw new ArgumentException($"The pattern '{regex}' is invalid.");
                }
            }
        }

        protected string GetMandatoryParameterValue(IGeneratedSymbolConfig config, string parameterName)
        {
            if (!config.Parameters.TryGetValue(parameterName, out string token))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_MissingMandatoryProperty, config.VariableName, Type, parameterName), config.VariableName);
            }
            return ConvertJTokenToString(token, config, parameterName);
        }

        protected TVal GetMandatoryParameterValue<TVal>(IGeneratedSymbolConfig config, string parameterName, Func<string, IGeneratedSymbolConfig, string, TVal> converter)
        {
            if (!config.Parameters.TryGetValue(parameterName, out string token))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_MissingMandatoryProperty, config.VariableName, Type, parameterName), config.VariableName);
            }
            return converter(token, config, parameterName);
        }

        protected JArray GetMandatoryParameterArray(IGeneratedSymbolConfig config, string parameterName)
        {
            if (!config.Parameters.TryGetValue(parameterName, out string token))
            {
                throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_MissingMandatoryProperty, config.VariableName, Type, parameterName), config.VariableName);
            }
            return ConvertJTokenToJArray(token, config, parameterName);
        }
    }
}
