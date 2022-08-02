// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    [Obsolete("Use IBindSymbolSource component instead to get or provide the values to be bound.")]
    public static class RuntimeValueUtil
    {
        [Obsolete("Use IBindSymbolSource components instead to get the values to be bound.")]
        public static bool TryGetRuntimeValue(this IParameterSet parameters, IEngineEnvironmentSettings environmentSettings, string name, out object? value, bool skipEnvironmentVariableSearch = false)
        {
            if (parameters.TryGetParameterDefinition(name, out ITemplateParameter param)
                && parameters.ResolvedValues.TryGetValue(param, out object? newValueObject)
                && newValueObject != null)
            {
                value = newValueObject;
                return true;
            }

            if ((environmentSettings.Host.TryGetHostParamDefault(name, out string? newValue) && newValue != null)
                || (!skipEnvironmentVariableSearch && environmentSettings.Environment.GetEnvironmentVariables().TryGetValue(name, out newValue) && newValue != null))
            {
                value = newValue;
                return true;
            }

            value = null;
            return false;
        }
    }
}
