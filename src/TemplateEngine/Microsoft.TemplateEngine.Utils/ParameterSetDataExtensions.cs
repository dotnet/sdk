// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Utils
{
    public static class ParameterSetDataExtensions
    {
        /// <summary>
        /// Creates instance of <see cref="IParameterSetData"/> from the legacy <see cref="IParameterSet"/>.
        /// </summary>
        /// <param name="parameterSet">Legacy parameterset to be converted.</param>
        /// <returns></returns>
        [Obsolete("IParameterSet should not be used - it is replaced with IParameterSetData", false)]
        public static IParameterSetData ToParameterSetData(this IParameterSet parameterSet)
        {
            IParameterDefinitionSet parametersDefinition = new ParameterDefinitionSet(parameterSet.ParameterDefinitions);
            IReadOnlyList<ParameterData> data = parameterSet.ResolvedValues.Select<KeyValuePair<ITemplateParameter, object?>, ParameterData>(p =>
                    new ParameterData(p.Key, p.Value, DataSource.User))
                .ToList();
            return new ParameterSetData(parametersDefinition, data);
        }
    }
}
