using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mutant.Chicken.Abstractions;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    internal class VsTemplate : ITemplate
    {
        public VsTemplate(TemplateSourceFile source, IConfiguredTemplateSource templateSource, IGenerator generator)
        {
            SourceFile = source;
            Source = templateSource;
            Generator = generator;

            using(Stream src = source.OpenRead())
            {
                XDocument doc = XDocument.Load(src);
                XElement idElement = doc.Root.Descendants().FirstOrDefault(x => x.Name.LocalName == "TemplateID");
                Name = idElement.Value;
                VsTemplateFile = doc;
            }
        }

        public IGenerator Generator { get; }

        public string Name { get; }

        public IConfiguredTemplateSource Source { get; }
        public TemplateSourceFile SourceFile { get; }

        public XDocument VsTemplateFile { get; }

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
}