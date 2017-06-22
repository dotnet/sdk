using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class DefaultLowerSafeNamespaceValueFormModel : DefaultSafeNamespaceValueFormModel
    {
        public new static readonly string FormName = "lower_safe_namespace";

        public override string Identifier => FormName;

        public override string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return base.Process(forms, value).ToLowerInvariant();
        }
    }
}
