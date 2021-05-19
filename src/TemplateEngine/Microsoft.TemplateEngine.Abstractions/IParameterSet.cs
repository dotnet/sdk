// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines a set of template parameters.
    /// </summary>
    public interface IParameterSet
    {
        /// <summary>
        /// Gets an enumerator iterating through the parameter definitions of the template.
        /// </summary>
        IEnumerable<ITemplateParameter> ParameterDefinitions { get; }

        /// <summary>
        /// Gets a collection of template parameters and their values.
        /// </summary>
        IDictionary<ITemplateParameter, object> ResolvedValues { get; }

        /// <summary>
        /// Gets a parameter definition with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Parameter name to get.</param>
        /// <param name="parameter">Retrieved parameter or null if the parameter is not found.</param>
        /// <returns>true if the parameter was retrieved, false otherwise.</returns>
        bool TryGetParameterDefinition(string name, out ITemplateParameter parameter);
    }
}
