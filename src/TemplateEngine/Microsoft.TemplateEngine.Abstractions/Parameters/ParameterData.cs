// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Parameters;

/// <summary>
/// Data model for template parameter instance data. Mainly used as input for <see cref="IGenerator"/>.
/// </summary>
public class ParameterData
{
    /// <summary>
    /// Creates new instance of <see cref="ParameterData"/> class.
    /// </summary>
    /// <param name="parameterDefinition">Descriptor of the parameter inferred from the template.</param>
    /// <param name="value">The value of the parameter derived from the instantiation context.</param>
    /// <param name="source">Source of value for template instantiation in the current context.</param>
    /// <param name="isEnabled">Indicates whether the parameter is enabled or disabled (and hence completely ignored).</param>
    public ParameterData(
        ITemplateParameter parameterDefinition,
        object? value,
        DataSource source,
        bool isEnabled = true)
    {
        ParameterDefinition = parameterDefinition;
        Value = value;
        DataSource = source;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Descriptor of the parameter inferred from the template.
    /// </summary>
    public ITemplateParameter ParameterDefinition { get; }

    /// <summary>
    /// The value of the parameter derived from the instantiation context (actual source of value indicated in <see cref="DataSource"/>).
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Source of value for template instantiation in the current context.
    /// </summary>
    public DataSource DataSource { get; }

    /// <summary>
    /// Indicates whether the parameter is enabled or disabled (and hence completely ignored). Disabling of parameter can be achieved via
    ///  condition or constant for IsEnabled property in Template.
    /// </summary>
    public bool IsEnabled { get; }

    public override string ToString() => $"{ParameterDefinition}: {Value?.ToString() ?? "<null>"}";
}
