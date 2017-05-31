using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class SymbolValueFormsModel
    {
        public static SymbolValueFormsModel Empty { get; } = new SymbolValueFormsModel(Empty<string>.List.Value);

        // by default, symbols get the "identity" value form, for a direct replacement
        public static SymbolValueFormsModel Default { get; } = new SymbolValueFormsModel(new List<string>() { "identity" });

        public IReadOnlyList<string> GlobalForms { get; }

        private SymbolValueFormsModel(IReadOnlyList<string> globalForms)
        {
            GlobalForms = globalForms;
        }

        public static SymbolValueFormsModel FromJObject(JObject obj)
        {
            IReadOnlyList<string> globalForms = obj.ArrayAsStrings("global");
            return new SymbolValueFormsModel(globalForms);
        }
    }
}
