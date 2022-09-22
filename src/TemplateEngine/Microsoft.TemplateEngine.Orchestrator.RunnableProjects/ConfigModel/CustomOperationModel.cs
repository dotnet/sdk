// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the operation configuration.
    /// </summary>
    public sealed class CustomOperationModel : ConditionedConfigurationElement
    {
        internal CustomOperationModel() { }

        /// <summary>
        /// Gets the operation type.
        /// </summary>
        public string? Type { get; internal init; }

        /// <summary>
        /// Gets the operation raw configuration in JSON format.
        /// </summary>
        public string? Configuration { get; internal init; }

        internal static CustomOperationModel FromJObject(JObject jObject)
        {
            CustomOperationModel model = new CustomOperationModel
            {
                Type = jObject.ToString(nameof(Type)),
                Condition = jObject.ToString(nameof(Condition)),
                Configuration = jObject.Get<JObject>(nameof(Configuration))?.ToString(),
            };

            return model;
        }
    }
}
