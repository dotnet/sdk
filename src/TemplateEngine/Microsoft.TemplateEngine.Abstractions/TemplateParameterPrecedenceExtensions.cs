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
        return priority switch
        {
            TemplateParameterPriority.Required => PrecedenceDefinition.Required,
            TemplateParameterPriority.Optional => PrecedenceDefinition.Optional,
            TemplateParameterPriority.Implicit => PrecedenceDefinition.Implicit,
            TemplateParameterPriority.Suggested => throw new NotImplementedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
        };
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
        return precedenceDefinition switch
        {
            PrecedenceDefinition.Required => TemplateParameterPriority.Required,
            PrecedenceDefinition.Optional => TemplateParameterPriority.Optional,
            PrecedenceDefinition.Implicit => TemplateParameterPriority.Implicit,
            _ => TemplateParameterPriority.Optional,
        };
    }
}
