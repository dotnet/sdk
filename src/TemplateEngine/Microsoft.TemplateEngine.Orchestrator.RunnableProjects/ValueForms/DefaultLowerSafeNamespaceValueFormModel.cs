// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultLowerSafeNamespaceValueFormModel : DefaultSafeNamespaceValueFormModel
    {
        internal new static readonly string FormName = "lower_safe_namespace";
        private readonly string _name;

        internal DefaultLowerSafeNamespaceValueFormModel()
            : base()
        {
        }

        internal DefaultLowerSafeNamespaceValueFormModel(string name)
            : base(name)
        {
            _name = name;
        }

        public override string Identifier => _name ?? FormName;

        public override string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return base.Process(forms, value).ToLowerInvariant();
        }

        public override IValueForm FromJObject(string name, JObject configuration)
        {
            return new DefaultLowerSafeNamespaceValueFormModel(name);
        }
    }
}
