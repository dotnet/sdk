// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class ParameterBasedVariableCollection : VariableCollection, IParameterBasedVariableCollection
    {
        public ParameterBasedVariableCollection()
            : this(TemplateEngine.Abstractions.Parameters.ParameterSetData.Empty)
        { }

        public ParameterBasedVariableCollection(IParameterSetData parameterSetData)
            : base()
        {
            ParameterSetData = parameterSetData;
        }

        public ParameterBasedVariableCollection(
            IVariableCollection? parent,
            IDictionary<string, object> values,
            IParameterSetData parameterSetData)
            : base(parent, values)
            => ParameterSetData = parameterSetData;

        public IParameterSetData ParameterSetData { get; private set; }

        public static ParameterBasedVariableCollection Root(IParameterSetData parameterSetData) =>
            new(null, new Dictionary<string, object>(), parameterSetData);

        public static IParameterBasedVariableCollection SetupParameterBasedVariables(IParameterSetData parameters, IVariableConfig variableConfig)
        {
            IParameterBasedVariableCollection variables = Root(parameters);

            Dictionary<string, ParameterBasedVariableCollection> collections = new Dictionary<string, ParameterBasedVariableCollection>();

            foreach (KeyValuePair<string, string> source in variableConfig.Sources)
            {
                ParameterBasedVariableCollection? variablesForSource = null;
                string format = source.Value;

                switch (source.Key)
                {
                    //may be extended for other categories in future if needed.
                    case "user":
                        variablesForSource = VariableCollectionFromParameters(parameters, format);

                        if (variableConfig.FallbackFormat != null)
                        {
                            ParameterBasedVariableCollection variablesFallback = VariableCollectionFromParameters(parameters, variableConfig.FallbackFormat);
                            variablesFallback.Parent = variablesForSource;
                            variablesForSource = variablesFallback;
                        }
                        break;
                }
                if (variablesForSource != null)
                {
                    collections[source.Key] = variablesForSource;
                }
            }

            foreach (string order in variableConfig.Order)
            {
                IParameterBasedVariableCollection current = collections[order.ToString()];

                IVariableCollection tmp = current;
                while (tmp.Parent != null)
                {
                    tmp = tmp.Parent;
                }

                tmp.Parent = variables;
                variables = current;
            }

            return variables;
        }

        private static ParameterBasedVariableCollection VariableCollectionFromParameters(IParameterSetData parameters, string format)
        {
            ParameterBasedVariableCollection vc = new ParameterBasedVariableCollection(parameters);
            foreach (ITemplateParameter param in parameters.ParametersDefinition)
            {
                string key = string.Format(format ?? "{0}", param.Name);

                if (parameters.TryGetValue(param, out ParameterData value) &&
                    value.IsEnabled && value.Value != null)
                {
                    vc[key] = value.Value;
                }
            }

            return vc;
        }
    }
}
