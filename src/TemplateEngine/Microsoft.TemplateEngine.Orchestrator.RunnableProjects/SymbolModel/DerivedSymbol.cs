using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class DerivedSymbol : BaseValueSymbol
    {
        internal const string TypeName = "derived";

        internal string ValueTransform { get; set; }

        internal string ValueSource { get; set; }

        internal static ISymbolModel FromJObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            DerivedSymbol symbol = FromJObject<DerivedSymbol>(jObject, localization, defaultOverride);

            symbol.ValueTransform = jObject.ToString(nameof(ValueTransform));
            symbol.ValueSource = jObject.ToString(nameof(ValueSource));

            return symbol;
        }
    }
}
