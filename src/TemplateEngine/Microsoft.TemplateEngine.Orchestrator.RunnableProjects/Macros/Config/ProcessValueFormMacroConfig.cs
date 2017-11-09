using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class ProcessValueFormMacroConfig : IMacroConfig
    {
        public ProcessValueFormMacroConfig(string sourceSymbol, string symbolName, string dataType, string valueForm, IReadOnlyDictionary<string, IValueForm> forms)
        {
            DataType = dataType;
            SourceVariable = sourceSymbol;
            VariableName = symbolName;
            FormName = valueForm;
            Forms = forms;
        }

        public string DataType { get; }

        public string VariableName { get; }

        public string Type => "processValueForm";

        public string SourceVariable { get; }

        public string FormName { get; }

        public IReadOnlyDictionary<string, IValueForm> Forms { get; }
    }
}
