// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class FirstLowerCaseValueFormModel : IValueForm
    {
        public string Identifier => "firstLowerCase";

        public string Name { get; }

        internal FirstLowerCaseValueFormModel()
        {
        }

        internal FirstLowerCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new FirstLowerCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            switch (value)
            {
                case null: return null;
                case "": return value;
                default: return value.First().ToString().ToLower() + value.Substring(1);
            }
        }
    }
}
