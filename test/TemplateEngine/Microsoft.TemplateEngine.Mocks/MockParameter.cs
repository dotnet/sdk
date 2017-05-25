using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockParameter : ITemplateParameter
    {
        public string Documentation { get; set; }

        public string Name { get; set; }

        public TemplateParameterPriority Priority { get; set; }

        public string Type { get; set; }

        public bool IsName { get; set; }

        public string DefaultValue { get; set; }

        public string DataType { get; set; }

        public IReadOnlyDictionary<string, string> Choices { get; set; }
    }
}
