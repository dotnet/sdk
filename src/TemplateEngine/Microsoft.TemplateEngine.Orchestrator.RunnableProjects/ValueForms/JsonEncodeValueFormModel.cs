// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class JsonEncodeValueFormModel : IValueForm
    {
        internal JsonEncodeValueFormModel()
        {
        }

        internal JsonEncodeValueFormModel(string name)
        {
            Name = name;
        }

        public string Identifier => "jsonEncode";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new JsonEncodeValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return JsonConvert.SerializeObject(value);
        }
    }
}
