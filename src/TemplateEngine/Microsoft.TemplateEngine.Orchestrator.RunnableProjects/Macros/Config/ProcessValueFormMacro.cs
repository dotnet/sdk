using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class ProcessValueFormMacroConfig : IMacroConfig
    {
        public ProcessValueFormMacroConfig(string sourceSymbol, string symbolName, string valueForm, IReadOnlyDictionary<string, IValueForm> forms)
        {
            SourceVariable = sourceSymbol;
            VariableName = symbolName;
            Form = valueForm;
            Forms = forms;
        }

        public string VariableName { get; }

        public string Type => "processValueForm";

        public string SourceVariable { get; }

        public string Form { get; }

        public IReadOnlyDictionary<string, IValueForm> Forms { get; }
    }
}
