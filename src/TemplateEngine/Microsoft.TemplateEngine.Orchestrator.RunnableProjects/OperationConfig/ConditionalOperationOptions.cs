// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig
{
    internal class ConditionalOperationOptions
    {
        private const EvaluatorType DefaultEvaluatorType = EvaluatorType.CPP;
        private const bool DefaultWholeLine = true;
        private const bool DefaultTrimWhitespace = true;

        internal ConditionalOperationOptions()
        {
            EvaluatorType = DefaultEvaluatorType;
            WholeLine = DefaultWholeLine;
            TrimWhitespace = DefaultTrimWhitespace;
        }

        internal EvaluatorType EvaluatorType { get; set; }

        internal bool WholeLine { get; set; }

        internal bool TrimWhitespace { get; set; }

        internal string? Id { get; set; }

        internal bool OnByDefault { get; set; }

        internal static ConditionalOperationOptions FromJObject(JObject rawConfiguration)
        {
            ConditionalOperationOptions options = new ConditionalOperationOptions();

            string? evaluatorType = rawConfiguration.ToString("evaluator");
            options.EvaluatorType = EvaluatorSelector.ParseEvaluatorName(evaluatorType, DefaultEvaluatorType);
            options.TrimWhitespace = rawConfiguration.ToBool("trim", DefaultTrimWhitespace);
            options.WholeLine = rawConfiguration.ToBool("wholeLine", DefaultWholeLine);
            options.OnByDefault = rawConfiguration.ToBool("onByDefault");

            string? id = rawConfiguration.ToString("id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                options.Id = id!;
            }

            return options;
        }
    }
}
