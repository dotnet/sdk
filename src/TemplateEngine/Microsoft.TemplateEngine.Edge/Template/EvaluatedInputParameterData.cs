// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Edge.Template;

/// <summary>
/// Type representing input data into <see cref="TemplateCreator"/> that are able to hold information about externally evaluated conditions
///  on template parameters.
/// </summary>
public class EvaluatedInputParameterData : InputParameterData
{
    /// <summary>
    /// Constructor for <see cref="EvaluatedInputParameterData"/> type, that allows specification of results of external evaluation of conditions.
    /// </summary>
    /// <param name="parameterDefinition"></param>
    /// <param name="value">A string converted value of parameter or null for explicit unset. It's possible to indicate missing of parameter on input via <see cref="InputDataState"/> argument.</param>
    /// <param name="dataSource"></param>
    /// <param name="isEnabledConditionResult"></param>
    /// <param name="isRequiredConditionResult"></param>
    /// <param name="inputDataState">
    /// InputDataState.Unset indicates a situation that parameter was not specified on input (distinct situation from explicit null).
    ///  This would normally be achieved by not passing the parameter at all into the <see cref="EvaluatedInputParameterData"/>, however then it would not be possible
    ///  to specify the results of conditions calculations.
    /// </param>
    public EvaluatedInputParameterData(
        ITemplateParameter parameterDefinition,
        object? value,
        DataSource dataSource,
        bool? isEnabledConditionResult,
        bool? isRequiredConditionResult,
        InputDataState inputDataState = InputDataState.Set)
        : base(parameterDefinition, value, dataSource, inputDataState)
    {
        IsEnabledConditionResult = isEnabledConditionResult;
        IsRequiredConditionResult = isRequiredConditionResult;
    }

    /// <summary>
    /// Externally (by the host) supplied result of the IsEnabledCondition on the template parameter.
    /// </summary>
    public bool? IsEnabledConditionResult { get; }

    /// <summary>
    /// Externally (by the host) supplied result of the IsRequiredCondition on the template parameter.
    /// </summary>
    public bool? IsRequiredConditionResult { get; }
}
