// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class JsonEncodeValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "jsonEncode";

        private static readonly JsonSerializerOptions JsonEncodeOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal JsonEncodeValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return JsonSerializer.Serialize(value, JsonEncodeOptions);
        }
    }
}
