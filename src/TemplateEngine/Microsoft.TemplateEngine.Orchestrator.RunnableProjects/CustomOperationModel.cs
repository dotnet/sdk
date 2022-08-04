// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CustomOperationModel
    {
        public string Type { get; set; }

        public string Condition { get; set; }

        internal JObject Configuration { get; set; }

        internal static CustomOperationModel FromJObject(JObject jObject)
        {
            CustomOperationModel model = new CustomOperationModel
            {
                Type = jObject.ToString(nameof(Type)),
                Condition = jObject.ToString(nameof(Condition)),
                Configuration = jObject.Get<JObject>(nameof(Configuration)),
            };

            return model;
        }
    }
}
