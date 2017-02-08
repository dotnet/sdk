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
    }
}
