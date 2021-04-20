// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class ConditionalOperationOptions
    {
        private static readonly string DefaultEvaluatorType = "C++";
        private static readonly bool DefaultWholeLine = true;
        private static readonly bool DefaultTrimWhitespace = true;
        private static readonly string DefaultId = null;

        internal ConditionalOperationOptions()
        {
            EvaluatorType = DefaultEvaluatorType;
            WholeLine = DefaultWholeLine;
            TrimWhitespace = DefaultTrimWhitespace;
            Id = DefaultId;
        }

        internal string EvaluatorType { get; set; }
        internal bool WholeLine { get; set; }
        internal bool TrimWhitespace { get; set; }
        internal string Id { get; set; }
        internal bool OnByDefault { get; set; }

        internal static ConditionalOperationOptions FromJObject(JObject rawConfiguration)
        {
            ConditionalOperationOptions options = new ConditionalOperationOptions();

            string evaluatorType = rawConfiguration.ToString("evaluator");
            if (!string.IsNullOrWhiteSpace(evaluatorType))
            {
                options.EvaluatorType = evaluatorType;
            }

            options.TrimWhitespace = rawConfiguration.ToBool("trim", true);
            options.WholeLine = rawConfiguration.ToBool("wholeLine", true);
            options.OnByDefault = rawConfiguration.ToBool("onByDefault");

            string id = rawConfiguration.ToString("id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                options.Id = id;
            }

            return options;
        }
    }
}
