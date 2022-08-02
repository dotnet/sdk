// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions;

public static class TemplateParameterPrecedenceExtensions
{
    /// <summary>
    /// Converts legacy parameter priority to the PrecedenceDefinition.
    /// </summary>
    /// <param name="priority"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [Obsolete("TemplateParameterPriority is Obsolete and should be replaced with PrecedenceDefinition.")]
    public static PrecedenceDefinition ToPrecedenceDefinition(this TemplateParameterPriority priority)
    {
        switch (priority)
        {
            case TemplateParameterPriority.Required:
                return PrecedenceDefinition.Required;
            case TemplateParameterPriority.Optional:
                return PrecedenceDefinition.Optional;
            case TemplateParameterPriority.Implicit:
                return PrecedenceDefinition.Implicit;
            default:
                throw new ArgumentOutOfRangeException(nameof(priority), priority, null);
        }
    }

    /// <summary>
    /// Converts legacy parameter priority to the TemplateParameterPrecedence.
    /// </summary>
    /// <param name="priority"></param>
    /// <returns></returns>
    [Obsolete("TemplateParameterPriority is Obsolete and should be replaced with PrecedenceDefinition.")]
    public static TemplateParameterPrecedence ToTemplateParameterPrecedence(this TemplateParameterPriority priority)
    {
        return new TemplateParameterPrecedence(priority.ToPrecedenceDefinition(), null, null);
    }

    /// <summary>
    /// Converts the PrecedenceDefinition to legacy TemplateParameterPriority.
    /// </summary>
    /// <param name="precedenceDefinition"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [Obsolete("TemplateParameterPriority is Obsolete and should be replaced with PrecedenceDefinition.")]
    public static TemplateParameterPriority ToTemplateParameterPriority(this PrecedenceDefinition precedenceDefinition)
    {
        switch (precedenceDefinition)
        {
            case PrecedenceDefinition.Required:
                return TemplateParameterPriority.Required;
            case PrecedenceDefinition.Optional:
                return TemplateParameterPriority.Optional;
            case PrecedenceDefinition.Implicit:
                return TemplateParameterPriority.Implicit;
            case PrecedenceDefinition.ConditionalyDisabled:
            case PrecedenceDefinition.Disabled:
            case PrecedenceDefinition.ConditionalyRequired:
            default:
                throw new ArgumentOutOfRangeException(nameof(precedenceDefinition), precedenceDefinition, "Conversion to obsolete TemplateParameterPriority is not defined for current value");
        }
    }
}
