// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultLowerSafeNameValueFormModel : DefaultSafeNameValueFormModel
    {
        internal new static readonly string FormName = "lower_safe_name";
        private readonly string _name;

        internal DefaultLowerSafeNameValueFormModel()
            : base()
        {
        }

        internal DefaultLowerSafeNameValueFormModel(string name)
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
            return new DefaultLowerSafeNameValueFormModel(name);
        }
    }
}
