using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class DefaultLowerSafeNamespaceValueFormModel : DefaultSafeNamespaceValueFormModel
    {
        public new static readonly string FormName = "lower_safe_namespace";

        public DefaultLowerSafeNamespaceValueFormModel()
            : base()
        {
        }

        public DefaultLowerSafeNamespaceValueFormModel(string name)
            : base(name)
        {
        }

        public override string Identifier => FormName;

        public override string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return base.Process(forms, value).ToLowerInvariant();
        }

        public override IValueForm FromJObject(string name, JObject configuration)
        {
            return new DefaultLowerSafeNamespaceValueFormModel(name);
        }
    }
}
