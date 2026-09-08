// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Parameters;

public static class ParameterSetDataExtensions
{
    /// <summary>
    /// Fetches the value of parameter from the set, based on the name of the parameter.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    public static ParameterData GetValue(this IParameterSetData data, string parameterName)
    {
        return data[data.ParametersDefinition[parameterName]];
    }

    /// <summary>
    /// Attempts to fetch the value of parameter from the set, based on the name of the parameter.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="parameterName"></param>
    /// <param name="parameterData"></param>
    /// <returns>True if parameter data was found. False otherwise.</returns>
    public static bool TryGetValue(this IParameterSetData data, string parameterName, out ParameterData? parameterData)
    {
        parameterData = null;
        return
            data.ParametersDefinition.TryGetValue(parameterName, out ITemplateParameter parameter) &&
            data.TryGetValue(parameter, out parameterData);
    }
}
