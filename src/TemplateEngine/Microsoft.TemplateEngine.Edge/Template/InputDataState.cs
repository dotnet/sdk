// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Edge.Template;

/// <summary>
/// Enumeration indicating how should the engine treat incoming value (as ignored, explicitly unset or standard value).
/// </summary>
public enum InputDataState
{
    /// <summary>
    /// Parameter is represented in input with nonempty value.
    /// </summary>
    Set,

    /// <summary>
    /// Parameter is not represented in input.
    /// </summary>
    Unset,

    /// <summary>
    /// Parameter is represented in input data with a null or empty value - this can e.g. indicate multichoice with no option selected.
    /// In CLI this is represented by option with explicit null string ('dotnet new mytemplate --myoptionA ""').
    /// In TemplateCreator this is represented by explicit null value.
    /// </summary>
    ExplicitEmpty
}

public static class InputDataStateUtil
{
    /// <summary>
    /// Tags the input value with <see cref="InputDataState"/> based on it's definition.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static InputDataState GetInputDataState(object? value)
    {
        // This is not and extension method as it's reach would be too broad (applicable to object)

        return value == null || (value is string str && string.IsNullOrEmpty(str))
            ? InputDataState.ExplicitEmpty
            : InputDataState.Set;
    }
}
