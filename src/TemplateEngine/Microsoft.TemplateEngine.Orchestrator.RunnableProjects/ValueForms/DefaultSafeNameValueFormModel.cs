using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    public class DefaultSafeNameValueFormModel : IValueForm
    {
        public DefaultSafeNameValueFormModel()
        {
        }

        public static readonly string FormName = "safe_name";

        public virtual string Identifier => FormName;

        public virtual string Name => Identifier;

        public IValueForm FromJObject(string name, JObject configuration)
        {
            throw new NotImplementedException();
        }

        public virtual string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            string workingValue = Regex.Replace(value, @"(^\s+|\s+$)", "");
            workingValue = Regex.Replace(workingValue, @"(((?<=\.)|^)(?=\d)|\W)", "_");

            return workingValue;
        }
    }
}
