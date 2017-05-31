using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class DefaultSafeNamespaceValueFormModel : IValueForm
    {
        public DefaultSafeNamespaceValueFormModel()
        {
        }

        public virtual string Identifier => "safe_namespace";

        public string Name => Identifier;

        public IValueForm FromJObject(string name, JObject configuration)
        {
            throw new NotImplementedException();
        }

        public virtual string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            string workingValue = Regex.Replace(value, @"(^\s+|\s+$)", "");
            workingValue = Regex.Replace(workingValue, @"(((?<=\.)|^)(?=\d)|[^\w\.])", "_");

            return workingValue;
        }
    }
}
