using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class VsTemplate : ITemplate
    {
        public VsTemplate(ITemplateSourceFile source, IConfiguredTemplateSource templateSource, IGenerator generator)
        {
            SourceFile = source;
            Source = templateSource;
            Generator = generator;

            using(Stream src = source.OpenRead())
            {
                XDocument doc = XDocument.Load(src);
                DefaultName = doc.Root.Descendants().FirstOrDefault(x => x.Name.LocalName == "DefaultName")?.Value;
                XElement idElement = doc.Root.Descendants().FirstOrDefault(x => x.Name.LocalName == "TemplateID");
                IEnumerable<XElement> customParameters = doc.Root.Descendants().Where(x => x.Name.LocalName == "CustomParameter");
                List<CustomParameter> declaredParams = new List<CustomParameter>();

                foreach (XElement parameter in customParameters)
                {
                    string name = parameter.Attributes().First(x => x.Name.LocalName == "Name").Value;
                    name = name.Substring(1, name.Length - 2);

                    declaredParams.Add(new CustomParameter
                    {
                        Name = name,
                        DefaultValue = parameter.Attributes().First(x => x.Name.LocalName == "Value").Value
                    });
                }

                CustomParameters = declaredParams;
                Name = idElement.Value;
                VsTemplateFile = doc;
            }
        }

        public IGenerator Generator { get; }

        public string Name { get; }

        public IConfiguredTemplateSource Source { get; }

        public string DefaultName { get; }

        public ITemplateSourceFile SourceFile { get; }

        public XDocument VsTemplateFile { get; }

        public IReadOnlyList<CustomParameter> CustomParameters { get; }

        public string ShortName => Name;

        public bool TryGetProperty(string name, out string value)
        {
            switch (name.ToLowerInvariant())
            {
                case "diskpath":
                    value = SourceFile.FullPath;
                    return true;
            }

            value = null;
            return false;
        }
    }

    internal class CustomParameter
    {
        public string Name { get; set; }

        public string DefaultValue { get; set; }
    }
}