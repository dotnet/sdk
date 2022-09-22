// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class JsonEncodeValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "jsonEncode";

        internal JsonEncodeValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return JsonConvert.SerializeObject(value);
        }
    }
}
