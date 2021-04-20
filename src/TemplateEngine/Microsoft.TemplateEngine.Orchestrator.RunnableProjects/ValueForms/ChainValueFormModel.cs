// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class ChainValueFormModel : IValueForm
    {
        private readonly IReadOnlyList<string> _steps;

        internal ChainValueFormModel()
        {
        }

        internal ChainValueFormModel(string name, IReadOnlyList<string> steps)
        {
            Name = name;
            _steps = steps;
        }

        public string Identifier => "chain";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new ChainValueFormModel(name, configuration.ArrayAsStrings("steps"));
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            string result = value;

            foreach (string step in _steps)
            {
                result = forms[step].Process(forms, result);
            }

            return result;
        }
    }
}
