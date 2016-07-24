using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class ComputedSymbol : ISymbolModel
    {
        public string Value { get; internal set; }

        public string Type { get; private set; }

        string ISymbolModel.Binding
        {
            get { return null; }
            set { }
        }

        string ISymbolModel.Replaces
        {
            get { return null; }
            set { }
        }

        public static ISymbolModel FromJObject(JObject jObject)
        {
            ComputedSymbol sym = new ComputedSymbol
            {
                Value = jObject.ToString(nameof(Value)),
                Type = jObject.ToString(nameof(Type))
            };

            return sym;
        }
    }
}