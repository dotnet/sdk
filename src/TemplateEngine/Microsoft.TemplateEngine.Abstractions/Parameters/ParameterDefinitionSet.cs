// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Abstractions.Parameters
{
    /// <inheritdoc />
    public class ParameterDefinitionSet : IParameterDefinitionSet
    {
        /// <summary>
        /// Empty descriptor set.
        /// </summary>
        public static readonly IParameterDefinitionSet Empty = new ParameterDefinitionSet((IEnumerable<ITemplateParameter>?)null);

        private readonly IReadOnlyDictionary<string, ITemplateParameter> _parameters;

        /// <summary>
        /// Initializes new instance of the <see cref="ParameterDefinitionSet"/> type.
        /// </summary>
        /// <param name="parameters"></param>
        public ParameterDefinitionSet(IReadOnlyDictionary<string, ITemplateParameter>? parameters) =>
            _parameters = parameters ?? new Dictionary<string, ITemplateParameter>();

        /// <summary>
        /// Initializes new instance of the <see cref="ParameterDefinitionSet"/> type.
        /// </summary>
        /// <param name="parameters"></param>
        public ParameterDefinitionSet(IEnumerable<ITemplateParameter>? parameters)
            : this(parameters?.ToDictionary(p => p.Name, p => p))
        { }

        /// <summary>
        /// Initializes new instance of the <see cref="ParameterDefinitionSet"/> type.
        /// </summary>
        /// <param name="other">Instance to be cloned.</param>
        public ParameterDefinitionSet(IParameterDefinitionSet other) : this(other.AsReadonlyDictionary())
        { }

        /// <inheritdoc />
        public IEnumerable<string> Keys => _parameters.Keys;

        /// <inheritdoc />
        public IEnumerable<ITemplateParameter> Values => _parameters.Values;

        /// <inheritdoc />
        public int Count => _parameters.Count;

        /// <inheritdoc />
        public ITemplateParameter this[string key] => _parameters[key];

        /// <inheritdoc />
        public ITemplateParameter this[int index] => _parameters.Values.ElementAt(index);

        /// <inheritdoc />
        public IReadOnlyDictionary<string, ITemplateParameter> AsReadonlyDictionary() => _parameters;

        /// <inheritdoc />
        public bool ContainsKey(string key) => _parameters.ContainsKey(key);

        /// <inheritdoc />
        public IEnumerator<ITemplateParameter> GetEnumerator() => _parameters.Values.GetEnumerator();

        /// <inheritdoc />
        public bool TryGetValue(string key, out ITemplateParameter value) => _parameters.TryGetValue(key, out value);

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => _parameters.Values.GetEnumerator();
    }
}
