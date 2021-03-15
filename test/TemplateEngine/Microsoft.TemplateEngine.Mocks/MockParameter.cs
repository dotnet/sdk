using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockParameter : ITemplateParameter, IAllowDefaultIfOptionWithoutValue
    {
        public string Documentation { get; set; }

        public string Name { get; set; }

        public TemplateParameterPriority Priority { get; set; }

        public string Type { get; set; }

        public bool IsName { get; set; }

        public string DefaultValue { get; set; }

        public string DefaultIfOptionWithoutValue { get; set; }

        public string DataType { get; set; }

        public IReadOnlyDictionary<string, ParameterChoice> Choices { get; set; }
    }
}
