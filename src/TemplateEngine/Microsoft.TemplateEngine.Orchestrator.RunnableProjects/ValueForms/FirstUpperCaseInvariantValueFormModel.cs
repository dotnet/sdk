// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class FirstUpperCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "firstUpperCaseInvariant";

        public string Name { get; }

        internal FirstUpperCaseInvariantValueFormModel()
        {
        }

        internal FirstUpperCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new FirstUpperCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            switch (value)
            {
                case null: return null;
                case "": return value;
                default: return value.First().ToString().ToUpperInvariant() + value.Substring(1);
            }
        }
    }
}
