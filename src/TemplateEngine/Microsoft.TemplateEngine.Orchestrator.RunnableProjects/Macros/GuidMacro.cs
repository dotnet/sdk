using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GuidMacro : IMacro
    {
        public Guid Id => new Guid("10919008-4E13-4FA8-825C-3B4DA855578E");

        public string Type => "guid";

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            switch (def.ToString("action"))
            {
                case "new":
                    string fmt = def.ToString("format");
                    if (fmt != null)
                    {
                        Guid g = Guid.NewGuid();
                        string value = char.IsUpper(fmt[0]) ? g.ToString(fmt[0].ToString()).ToUpperInvariant() : g.ToString(fmt[0].ToString()).ToLowerInvariant();
                        Parameter p = new Parameter
                        {
                            IsVariable = true,
                            Name = variableName
                        };

                        setter(p, value);
                    }
                    else
                    {
                        Guid g = Guid.NewGuid();
                        const string guidFormats = "ndbpxNDPBX";
                        for (int i = 0; i < guidFormats.Length; ++i)
                        {
                            Parameter p = new Parameter
                            {
                                IsVariable = true,
                                Name = variableName + "-" + guidFormats[i]
                            };

                            string rplc = char.IsUpper(guidFormats[i]) ? g.ToString(guidFormats[i].ToString()).ToUpperInvariant() : g.ToString(guidFormats[i].ToString()).ToLowerInvariant();
                            setter(p, rplc);
                        }

                        Parameter pd = new Parameter
                        {
                            IsVariable = true,
                            Name = variableName
                        };

                        setter(pd, g.ToString("D"));
                    }

                    break;
            }
        }
    }
}