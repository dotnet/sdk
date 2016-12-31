//using Microsoft.TemplateEngine.Abstractions;
//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace Microsoft.TemplateEngine.Mocks
//{
//    public class MockParameter : ITemplateParameter
//    {
//        public string Description { get; set; }

//        public string DefaultValue { get; set; }

//        public string Name { get; set; }

//        public bool IsName { get; set; }

//        public TemplateParameterPriority Requirement { get; set; }

//        public string Type { get; set; }

//        public bool IsVariable { get; set; }

//        public string DataType { get; set; }

//        public IReadOnlyList<string> Choices { get; set; }

//        string ITemplateParameter.Documentation => Description;

//        string ITemplateParameter.Name => Name;

//        TemplateParameterPriority ITemplateParameter.Priority => Requirement;

//        string ITemplateParameter.Type => Type;

//        bool ITemplateParameter.IsName => IsName;

//        string ITemplateParameter.DefaultValue => DefaultValue;

//        string ITemplateParameter.DataType => DataType;

//        IReadOnlyList<string> ITemplateParameter.Choices => Choices;

//        public override string ToString()
//        {
//            return $"{Name} ({Type})";
//        }
//    }
//}
