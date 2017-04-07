using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public static class ValueFormRegistry
    {
        private static readonly IReadOnlyDictionary<string, IValueForm> FormLookup = SetupFormLookup();

        private static IReadOnlyDictionary<string, IValueForm> SetupFormLookup()
        {
            Dictionary<string, IValueForm> lookup = new Dictionary<string, IValueForm>(StringComparer.OrdinalIgnoreCase);
            IValueForm x = new ReplacementValueFormModel();
            lookup[x.Identifier] = x;
            x = new ChainValueFormModel();
            lookup[x.Identifier] = x;
            x = new XmlEncodeValueFormModel();
            lookup[x.Identifier] = x;
            x = new IdentityValueForm();
            lookup[x.Identifier] = x;
            return lookup;
        }

        public static IValueForm GetForm(string name, JObject obj)
        {
            string identifier = obj.ToString("identifier");

            if (!FormLookup.TryGetValue(identifier, out IValueForm value))
            {
                return FormLookup["identity"].FromJObject(name, obj);
            }

            return value.FromJObject(name, obj);
        }
    }

    public interface IValueForm
    {
        string Identifier { get; }

        string Name { get; }

        string Process(IReadOnlyDictionary<string, IValueForm> forms, string value);

        IValueForm FromJObject(string name, JObject configuration);
    }

    public class ReplacementValueFormModel : IValueForm
    {
        private readonly Regex _match;
        private readonly string _replacment;
        
        public ReplacementValueFormModel()
        {
        }

        public ReplacementValueFormModel(string name, string pattern, string replacement)
        {
            _match = new Regex(pattern);
            _replacment = replacement;
            Name = name;
        }

        public string Identifier => "replace";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new ReplacementValueFormModel(name, configuration.ToString("pattern"), configuration.ToString("replacement"));
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return _match.Replace(value, _replacment);
        }
    }

    public class ChainValueFormModel : IValueForm
    {
        private readonly IReadOnlyList<string> _steps;

        public ChainValueFormModel()
        {
        }

        public ChainValueFormModel(string name, IReadOnlyList<string> steps)
        {
            Name = name;
            _steps = steps;
        }

        public string Identifier => "chain";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new ChainValueFormModel(name, configuration.ArrayAsStrings("steps"));
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            string result = value;

            foreach (string step in _steps)
            {
                result = forms[step].Process(forms, result);
            }

            return result;
        }
    }

    public class XmlEncodeValueFormModel : IValueForm
    {
        private static readonly XmlWriterSettings Settings = new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Fragment };

        public string Identifier => "xmlEncode";

        public string Name { get; }

        public XmlEncodeValueFormModel()
        {
        }

        public XmlEncodeValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new XmlEncodeValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            StringBuilder output = new StringBuilder();
            using (XmlWriter w = XmlWriter.Create(output, Settings))
            {
                w.WriteString(value);
            }
            return output.ToString();
        }
    }

    public class LowerCaseValueFormModel : IValueForm
    {
        public string Identifier => "lowerCase";

        public string Name { get; }

        public LowerCaseValueFormModel()
        {
        }

        public LowerCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new LowerCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToLower();
        }
    }

    public class LowerCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "lowerCaseInvariant";

        public string Name { get; }

        public LowerCaseInvariantValueFormModel()
        {
        }

        public LowerCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new LowerCaseInvariantValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToLowerInvariant();
        }
    }

    public class UpperCaseValueFormModel : IValueForm
    {
        public string Identifier => "upperCase";

        public string Name { get; }

        public UpperCaseValueFormModel()
        {
        }

        public UpperCaseValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new UpperCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToUpper();
        }
    }

    public class UpperCaseInvariantValueFormModel : IValueForm
    {
        public string Identifier => "upperCaseInvariant";

        public string Name { get; }

        public UpperCaseInvariantValueFormModel()
        {
        }

        public UpperCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new UpperCaseInvariantValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToUpperInvariant();
        }
    }
}
