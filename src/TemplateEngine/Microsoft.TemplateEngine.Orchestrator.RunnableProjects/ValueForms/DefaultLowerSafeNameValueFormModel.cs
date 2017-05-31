using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class DefaultLowerSafeNameValueFormModel : DefaultSafeNameValueFormModel
    {
        public override string Identifier => "lower_safe_name";

        public override string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return base.Process(forms, value).ToLowerInvariant();
        }
    }
}
