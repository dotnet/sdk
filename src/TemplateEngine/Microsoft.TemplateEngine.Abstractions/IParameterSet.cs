// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IParameterSet
    {
        IEnumerable<ITemplateParameter> ParameterDefinitions { get; }

        IEnumerable<string> RequiredBrokerCapabilities { get; }

        IDictionary<ITemplateParameter, object> ResolvedValues { get; }

        bool TryGetParameterDefinition(string name, out ITemplateParameter parameter);
    }
}
