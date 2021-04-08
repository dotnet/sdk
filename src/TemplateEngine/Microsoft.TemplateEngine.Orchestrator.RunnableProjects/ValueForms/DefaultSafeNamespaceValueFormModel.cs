using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultSafeNamespaceValueFormModel : IValueForm
    {
        private readonly string _name;

        internal DefaultSafeNamespaceValueFormModel()
            : this(null)
        {
        }

        internal DefaultSafeNamespaceValueFormModel(string name)
        {
            _name = name;
        }

        internal static readonly string FormName = "safe_namespace";

        public virtual string Identifier => _name ?? FormName;

        public string Name => Identifier;

        public virtual IValueForm FromJObject(string name, JObject configuration)
        {
            return new DefaultSafeNamespaceValueFormModel(name);
        }

        public virtual string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            string workingValue = Regex.Replace(value, @"(^\s+|\s+$)", "");
            workingValue = Regex.Replace(workingValue, @"(((?<=\.)|^)((?=\d)|\.)|[^\w\.])|(\.$)", "_");

            return workingValue;
        }
    }
}
