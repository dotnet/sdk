// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Edge.Template;

/// <summary>
/// Type representing input data into <see cref="TemplateCreator"/>.
/// </summary>
public class InputParameterData
{
    /// <summary>
    /// Creates new instance of the <see cref="InputParameterData"/> type.
    /// </summary>
    /// <param name="parameterDefinition"></param>
    /// <param name="value"></param>
    /// <param name="dataSource"></param>
    /// <param name="inputDataState"></param>
    public InputParameterData(
        ITemplateParameter parameterDefinition,
        object? value,
        DataSource dataSource = DataSource.User,
        InputDataState inputDataState = InputDataState.Set)
    {
        ParameterDefinition = parameterDefinition;
        Value = value;
        DataSource = dataSource;
        InputDataState = inputDataState;
    }

    /// <summary>
    /// Descriptor of the parameter.
    /// </summary>
    public ITemplateParameter ParameterDefinition { get; }

    /// <summary>
    /// Value of the parameter.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Source of the parameter value. If supplied by the host - leave the default value of <see cref="DataSource.User"/>.
    /// </summary>
    public DataSource DataSource { get; }

    /// <summary>
    /// Input data state - indicates how the actual value should be treated (ignored, regarded as explicitly unset value, etc.).
    /// </summary>
    public InputDataState InputDataState { get; }

    public override string ToString() => $"{ParameterDefinition}: {Value?.ToString() ?? "<null>"}";
}
