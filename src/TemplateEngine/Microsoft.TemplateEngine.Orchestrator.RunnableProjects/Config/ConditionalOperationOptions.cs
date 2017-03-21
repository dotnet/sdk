using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ConditionalOperationOptions
    {
        private static readonly string DefaultEvaluatorType = "C++";
        private static readonly bool DefaultWholeLine = true;
        private static readonly bool DefaultTrimWhitespace = true;
        private static readonly string DefaultId = null;

        public ConditionalOperationOptions()
        {
            EvaluatorType = DefaultEvaluatorType;
            WholeLine = DefaultWholeLine;
            TrimWhitespace = DefaultTrimWhitespace;
            Id = DefaultId;
        }

        public string EvaluatorType { get; set; }
        public bool WholeLine { get; set; }
        public bool TrimWhitespace { get; set; }
        public string Id { get; set; }
        public bool OnByDefault { get; set; }

        public static ConditionalOperationOptions FromJObject(JObject rawConfiguration)
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
