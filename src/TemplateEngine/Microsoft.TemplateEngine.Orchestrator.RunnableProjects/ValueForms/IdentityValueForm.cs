// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class IdentityValueForm : IValueForm
    {
        internal static readonly string FormName = "identity";

        public string Identifier => FormName;

        public string Name { get; }

        internal IdentityValueForm()
        {
        }

        internal IdentityValueForm(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new IdentityValueForm(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value;
        }
    }
}
