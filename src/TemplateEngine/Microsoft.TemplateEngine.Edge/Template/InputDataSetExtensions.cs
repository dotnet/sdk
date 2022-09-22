// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Template;

internal static class InputDataSetExtensions
{
    public static IParameterSetData ToParameterSetData(this InputDataSet inputData)
    {
        return new ParameterSetData(
            inputData.ParameterDefinitionSet,
            inputData.Values.Select(d => new ParameterData(d.ParameterDefinition, d.Value, d.DataSource, !(d is EvaluatedInputParameterData ed && ed.IsEnabledConditionResult == false)))
                .ToList());
    }

    [Obsolete("IParameterSet should not be used - it is replaced with IParameterSetData", false)]
    public static InputDataSet ToInputDataSet(this IParameterSet parameterSet)
    {
        IParameterDefinitionSet parametersDefinition = new ParameterDefinitionSet(parameterSet.ParameterDefinitions);
        IReadOnlyList<InputParameterData> data = parameterSet.ResolvedValues.Select(p =>
                new InputParameterData(p.Key, p.Value, DataSource.User, InputDataStateUtil.GetInputDataState(p.Value)))
            .ToList();
        return new InputDataSet(parametersDefinition, data);
    }

    public static void VerifyInputData(this InputDataSet inputData)
    {
        if (inputData.Values.OfType<EvaluatedInputParameterData>().Any())
        {
            ErrorOutOnMismatchedConditionEvaluation(
                inputData.Values.Where(p =>
                    !(p is EvaluatedInputParameterData evaluated && evaluated.IsEnabledConditionResult != null) ^
                    string.IsNullOrEmpty(p.ParameterDefinition.Precedence.IsEnabledCondition)).ToList());

            ErrorOutOnMismatchedConditionEvaluation(
                inputData.Values.Where(p =>
                    !(p is EvaluatedInputParameterData evaluated && evaluated.IsRequiredConditionResult != null) ^
                    string.IsNullOrEmpty(p.ParameterDefinition.Precedence.IsRequiredCondition)).ToList());
        }

        inputData.Values.ForEach(VerifyConditions);
        inputData.Values.ForEach(VerifyInputState);
    }

    public static bool HasConditions(this InputDataSet inputData)
    {
        return inputData.ParameterDefinitionSet.Any(p =>
            !string.IsNullOrEmpty(p.Precedence.IsRequiredCondition) ||
            !string.IsNullOrEmpty(p.Precedence.IsEnabledCondition));
    }

    public static EvaluatedPrecedence GetEvaluatedPrecedence(this InputParameterData inputParameterData)
    {
        EvaluatedInputParameterData? dt = inputParameterData as EvaluatedInputParameterData;

        return inputParameterData.ParameterDefinition.Precedence.PrecedenceDefinition switch
        {
            PrecedenceDefinition.Required => EvaluatedPrecedence.Required,
            // Conditionally required state is only set if enabled condition is not  present
            PrecedenceDefinition.ConditionalyRequired => dt!.IsRequiredConditionResult!.Value ? EvaluatedPrecedence.Required : EvaluatedPrecedence.Optional,
            PrecedenceDefinition.Optional => EvaluatedPrecedence.Optional,
            PrecedenceDefinition.Implicit => EvaluatedPrecedence.Implicit,
            PrecedenceDefinition.ConditionalyDisabled => !dt!.IsEnabledConditionResult!.Value
                                ? EvaluatedPrecedence.Disabled
                                :
                                (dt.IsRequiredConditionResult.HasValue && dt.IsRequiredConditionResult.Value) || dt.ParameterDefinition.Precedence.IsRequired
                                    ? EvaluatedPrecedence.Required : EvaluatedPrecedence.Optional,
            PrecedenceDefinition.Disabled => EvaluatedPrecedence.Disabled,
            _ => throw new ArgumentOutOfRangeException("PrecedenceDefinition"),
        };
    }

    private static void ErrorOutOnMismatchedConditionEvaluation(IReadOnlyList<InputParameterData> offendingParameters)
    {
        if (offendingParameters.Any())
        {
            throw new Exception(
                string.Format(LocalizableStrings.EvaluatedInputDataSet_Error_MismatchedConditions, string.Join(", ", offendingParameters)));
        }
    }

    private static void VerifyConditions(InputParameterData inputParameterData)
    {
        if (
            inputParameterData is EvaluatedInputParameterData evaluatedInputParameterData
            &&
            (
            string.IsNullOrEmpty(inputParameterData.ParameterDefinition.Precedence.IsEnabledCondition) ^ !evaluatedInputParameterData.IsEnabledConditionResult.HasValue
            ||
            string.IsNullOrEmpty(inputParameterData.ParameterDefinition.Precedence.IsRequiredCondition) ^ !evaluatedInputParameterData.IsRequiredConditionResult.HasValue))
        {
            throw new ArgumentException(string.Format(LocalizableStrings.EvaluatedInputParameterData_Error_ConditionsInvalid, inputParameterData.ParameterDefinition.Name));
        }
    }

    private static void VerifyInputState(InputParameterData inputParameterData)
    {
        if (inputParameterData.InputDataState == InputDataState.Unset)
        {
            if (inputParameterData.Value != null)
            {
                throw new ArgumentException(
                    string.Format(
                        "It's disallowed to pass an input data value (even empty string) when it's tagged as InputDataState.Unset. Param: {0}",
                        inputParameterData.ParameterDefinition.Name));
            }
        }
        else if (InputDataStateUtil.GetInputDataState(inputParameterData.Value) != inputParameterData.InputDataState)
        {
            throw new ArgumentException(
                string.Format(
                    "Param {0} has disallowed combination of input data value ({1}) and InputDataState ({2}).",
                    inputParameterData.ParameterDefinition.Name,
                    inputParameterData.Value,
                    inputParameterData.InputDataState));
        }
    }
}
