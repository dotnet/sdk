// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Edge.Template;

/// <summary>
/// Data model for passing data into the <see cref="TemplateCreator"/>.
/// </summary>
public class InputDataSet : IReadOnlyDictionary<ITemplateParameter, InputParameterData>
{
    private readonly IReadOnlyDictionary<ITemplateParameter, InputParameterData> _parametersData;

    /// <summary>
    /// Creates new instance of the <see cref="InputDataSet"/> type.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="parameterData"></param>
    public InputDataSet(IParameterDefinitionSet parameters, IReadOnlyList<InputParameterData> parameterData)
    {
        _parametersData = parameterData.ToDictionary(d => d.ParameterDefinition, d => d);
        ParameterDefinitionSet = new ParameterDefinitionSet(parameters.AsReadonlyDictionary());
    }

    /// <summary>
    /// Creates new instance of the <see cref="InputDataSet"/> type.
    /// To be used to convert legacy parameters dictionaries into this data model.
    /// </summary>
    /// <param name="templateInfo"></param>
    public InputDataSet(ITemplateInfo templateInfo)
        : this(templateInfo, (IReadOnlyDictionary<string, object?>?)null)
    { }

    /// <summary>
    /// Creates new instance of the <see cref="InputDataSet"/> type.
    /// To be used to convert legacy parameters dictionaries into this data model.
    /// </summary>
    /// <param name="templateInfo"></param>
    /// <param name="inputParameters"></param>
    public InputDataSet(ITemplateInfo templateInfo, IReadOnlyDictionary<string, string?> inputParameters)
        : this(templateInfo, inputParameters.ToDictionary(p => p.Key, p => (object?)p.Value))
    { }

    private InputDataSet(ITemplateInfo templateInfo, IReadOnlyDictionary<string, object?>? inputParameters)
    {
        _parametersData = templateInfo.ParameterDefinitions.ToDictionary(p => p, p =>
        {
            object? value = null;
            bool isSet = inputParameters != null && inputParameters.TryGetValue(p.Name, out value);
            return new InputParameterData(p, value, isSet ? DataSource.User : DataSource.NoSource, isSet ? InputDataStateUtil.GetInputDataState(value) : InputDataState.Unset);
        });
        ParameterDefinitionSet = new ParameterDefinitionSet(templateInfo.ParameterDefinitions);
    }

    /// <summary>
    /// Indicates whether template creator should ignore evaluation results that does not match validation evaluation.
    ///  Warning will be logged and external evaluation results will be used.
    /// </summary>
    public bool ContinueOnMismatchedConditionsEvaluation { get; init; }

    /// <summary>
    /// Descriptors of the parameters.
    /// </summary>
    public ParameterDefinitionSet ParameterDefinitionSet { get; }

    /// <inheritdoc/>
    public IEnumerable<ITemplateParameter> Keys => _parametersData.Keys;

    /// <inheritdoc/>
    public IEnumerable<InputParameterData> Values => _parametersData.Values;

    /// <inheritdoc/>
    public int Count => _parametersData.Count;

    /// <inheritdoc/>
    public InputParameterData this[ITemplateParameter key] => _parametersData[key];

    /// <inheritdoc/>
    public bool ContainsKey(ITemplateParameter key) => _parametersData.ContainsKey(key);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<ITemplateParameter, InputParameterData>> GetEnumerator() => _parametersData.GetEnumerator();

    /// <inheritdoc/>
    public bool TryGetValue(ITemplateParameter key, out InputParameterData value) => _parametersData.TryGetValue(key, out value);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _parametersData.GetEnumerator();
}
