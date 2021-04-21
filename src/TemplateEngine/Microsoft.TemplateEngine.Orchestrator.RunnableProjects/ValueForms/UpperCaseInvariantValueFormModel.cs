// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class UpperCaseInvariantValueFormModel : IValueForm
    {
        internal UpperCaseInvariantValueFormModel()
        {
        }

        internal UpperCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public string Identifier => "upperCaseInvariant";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new UpperCaseInvariantValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToUpperInvariant();
        }
    }
}
