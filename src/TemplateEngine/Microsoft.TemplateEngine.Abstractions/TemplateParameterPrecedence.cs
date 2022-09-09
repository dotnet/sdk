// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions;

/// <summary>
/// Indication of template parameter precedence and conditions that could influence it.
/// </summary>
public class TemplateParameterPrecedence
{
    /// <summary>
    /// Default optional precedence.
    /// </summary>
    public static readonly TemplateParameterPrecedence Default = new(PrecedenceDefinition.Optional);

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateParameterPrecedence"/> class.
    /// </summary>
    /// <param name="precedenceDefinition"></param>
    /// <param name="isRequiredCondition"></param>
    /// <param name="isEnabledCondition"></param>
    /// <param name="isRequired"></param>
    public TemplateParameterPrecedence(
        PrecedenceDefinition precedenceDefinition,
        string? isRequiredCondition = null,
        string? isEnabledCondition = null,
        bool isRequired = false)
    {
        PrecedenceDefinition = precedenceDefinition;
        IsRequiredCondition = isRequiredCondition;
        IsEnabledCondition = isEnabledCondition;
        IsRequired = isRequired;
        VerifyConditions();
    }

    /// <summary>
    /// Actual precedence definition - can be used to sort parameters (e.g. for tab completion purposes).
    /// </summary>
    public PrecedenceDefinition PrecedenceDefinition { get; }

    /// <summary>
    /// IsRequiredCondition value - if it was specified in template.
    /// </summary>
    public string? IsRequiredCondition { get; }

    /// <summary>
    /// IsEnabledCondition value - if it was specified in template.
    /// </summary>
    public string? IsEnabledCondition { get; }

    /// <summary>
    /// Indicates whether parameter is unconditionally required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Indicates whether parameter might be required (depending on values of other parameters).
    /// </summary>
    public bool CanBeRequired =>
        PrecedenceDefinition == PrecedenceDefinition.Required ||
        !string.IsNullOrEmpty(IsRequiredCondition) ||
        IsRequired;

    private void VerifyConditions()
    {
        // If enable condition is set - parameter is conditionally disabled (regardless if require condition is set or not)
        // Conditionally required is if and only if the only require condition is set

        if (!(string.IsNullOrEmpty(IsRequiredCondition) ^ PrecedenceDefinition == PrecedenceDefinition.ConditionalyRequired
              ||
              !string.IsNullOrEmpty(IsEnabledCondition) ^ PrecedenceDefinition == PrecedenceDefinition.ConditionalyDisabled)
            &&
            !(!string.IsNullOrEmpty(IsRequiredCondition) && !string.IsNullOrEmpty(IsEnabledCondition) && PrecedenceDefinition == PrecedenceDefinition.ConditionalyDisabled))
        {
            // TODO: localize
            throw new ArgumentException("Mismatched precedence definition");
        }
    }
}
