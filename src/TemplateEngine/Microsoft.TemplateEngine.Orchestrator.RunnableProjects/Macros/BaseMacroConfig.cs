// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
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
                if (jToken.Type is not JTokenType.Boolean and not JTokenType.String)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' should be a boolean value.", config.VariableName);
                }
                if (bool.TryParse(jToken.ToString(), out bool result))
                {
                    return result;
                }
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' should be a boolean value.", config.VariableName);
            }
            catch (TemplateAuthoringException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' is not a valid JSON.", config.VariableName, ex);
            }
        }

        protected static int ConvertJTokenToInt(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken.Type is not JTokenType.Integer and not JTokenType.String)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' should be an integer.", config.VariableName);
                }
                if (int.TryParse(jToken.ToString(), out int result))
                {
                    return result;
                }
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' should be an integer.", config.VariableName);
            }
            catch (TemplateAuthoringException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' is not a valid JSON.", config.VariableName, ex);
            }
        }

        protected static string ConvertJTokenToString(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken.Type != JTokenType.String)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' should be a string.", config.VariableName);
                }
                return jToken.ToString();
            }
            catch (TemplateAuthoringException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' is not a valid JSON.", config.VariableName, ex);
            }
        }

        protected static JArray ConvertJTokenToJArray(string token, IGeneratedSymbolConfig config, string parameterName)
        {
            try
            {
                var jToken = JToken.Parse(token);
                if (jToken.Type != JTokenType.Array)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' should be a JSON array.", config.VariableName);
                }
                return (JArray)jToken;
            }
            catch (TemplateAuthoringException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}': '{parameterName}' is not a valid JSON.", config.VariableName, ex);
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
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': the pattern '{regex}' is invalid.", generatedSymbolConfig.VariableName);
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
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}' of type '{Type}' should have '{parameterName}' property defined.");
            }
            return ConvertJTokenToString(token, config, parameterName);
        }

        protected TVal GetMandatoryParameterValue<TVal>(IGeneratedSymbolConfig config, string parameterName, Func<string, IGeneratedSymbolConfig, string, TVal> converter)
        {
            if (!config.Parameters.TryGetValue(parameterName, out string token))
            {
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}' of type '{Type}' should have '{parameterName}' property defined.");
            }
            return converter(token, config, parameterName);
        }

        protected JArray GetMandatoryParameterArray(IGeneratedSymbolConfig config, string parameterName)
        {
            if (!config.Parameters.TryGetValue(parameterName, out string token))
            {
                throw new TemplateAuthoringException($"Generated symbol '{config.VariableName}' of type '{Type}' should have '{parameterName}' property defined.");
            }
            return ConvertJTokenToJArray(token, config, parameterName);
        }
    }
}
