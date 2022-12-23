// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Abstractions.Parameters;

/// <inheritdoc/>
public class ParameterSetData : IParameterSetData
{
    private readonly IReadOnlyDictionary<ITemplateParameter, ParameterData> _parametersData;

    /// <summary>
    /// Creates new instance of the <see cref="ParameterSetData"/> data type.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="parameterData"></param>
    public ParameterSetData(IParameterDefinitionSet parameters, IReadOnlyList<ParameterData> parameterData)
    {
        ParametersDefinition = new ParameterDefinitionSet(parameters.AsReadonlyDictionary());
        _parametersData = parameterData.ToDictionary(d => d.ParameterDefinition, d => d);
    }

    /// <summary>
    /// Creates new instance of the <see cref="ParameterSetData"/> data type, not initialized with any actual instantiation data.
    /// To be used for compatibility purposes in places where old dictionary parameter set was used.
    /// </summary>
    /// <param name="templateInfo"></param>
    public ParameterSetData(ITemplateInfo templateInfo)
        : this(templateInfo, (IReadOnlyDictionary<string, object?>?)null)
    { }

    /// <summary>
    /// Creates new instance of the <see cref="ParameterSetData"/> data type.
    /// To be used for compatibility purposes in places where old dictionary parameter set was used.
    /// </summary>
    /// <param name="templateInfo"></param>
    /// <param name="inputParameters"></param>
    public ParameterSetData(ITemplateInfo templateInfo, IReadOnlyDictionary<string, string?>? inputParameters)
        : this(templateInfo, inputParameters?.ToDictionary(p => p.Key, p => (object?)p.Value))
    { }

    /// <summary>
    /// Creates new instance of the <see cref="ParameterSetData"/> data type.
    /// To be used for compatibility purposes in places where old dictionary parameter set was used.
    /// </summary>
    /// <param name="templateInfo"></param>
    /// <param name="inputParameters"></param>
    public ParameterSetData(ITemplateInfo templateInfo, IReadOnlyDictionary<string, object?>? inputParameters)
    {
        ParametersDefinition = new ParameterDefinitionSet(templateInfo.ParameterDefinitions);
        _parametersData = templateInfo.ParameterDefinitions.ToDictionary(p => p, p =>
        {
            object? value = null;
            bool isSet = inputParameters != null && inputParameters.TryGetValue(p.Name, out value);
            return new ParameterData(p, value, isSet ? DataSource.User : DataSource.NoSource);
        });
    }

    /// <summary>
    /// Empty instance.
    /// </summary>
    public static IParameterSetData Empty =>
        new ParameterSetData(new ParameterDefinitionSet((IReadOnlyDictionary<string, ITemplateParameter>?)null), System.Array.Empty<ParameterData>());

    /// <inheritdoc/>
    public IParameterDefinitionSet ParametersDefinition { get; }

    /// <inheritdoc/>
    public int Count => _parametersData.Count;

    /// <inheritdoc/>
    public IEnumerable<ITemplateParameter> Keys => _parametersData.Keys;

    /// <inheritdoc/>
    public IEnumerable<ParameterData> Values => _parametersData.Values;

    /// <inheritdoc/>
    public ParameterData this[ITemplateParameter key] => _parametersData[key];

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<ITemplateParameter, ParameterData>> GetEnumerator() => _parametersData.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool ContainsKey(ITemplateParameter key) => _parametersData.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(ITemplateParameter key, out ParameterData value) => _parametersData.TryGetValue(key, out value);
}
