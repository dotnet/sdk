using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    public class DerivedSymbol : BaseValueSymbol
    {
        internal const string TypeName = "derived";

        public string ValueTransform { get; set; }

        public string ValueSource { get; set; }

        public static ISymbolModel FromJObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            DerivedSymbol symbol = FromJObject<DerivedSymbol>(jObject, localization, defaultOverride);

            symbol.ValueTransform = jObject.ToString(nameof(ValueTransform));
            symbol.ValueSource = jObject.ToString(nameof(ValueSource));

            return symbol;
        }
    }
}
