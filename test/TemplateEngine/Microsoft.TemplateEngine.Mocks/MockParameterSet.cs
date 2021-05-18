// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockParameterSet : IParameterSet
    {
        public IEnumerable<ITemplateParameter> ParameterDefinitions
        {
            get
            {
                return new List<ITemplateParameter>();
            }
        }

        public IEnumerable<string> RequiredBrokerCapabilities
        {
            get
            {
                return new List<string>();
            }
        }

        public IDictionary<ITemplateParameter, object> ResolvedValues
        {
            get
            {
                return new Dictionary<ITemplateParameter, object>();
            }
        }

        public bool TryGetParameterDefinition(string name, out ITemplateParameter parameter)
        {
            parameter = new TemplateParameter(name, "parameter", "string");
            return true;
        }
    }
}
