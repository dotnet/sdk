using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class VsTemplate : ITemplate
    {
        public VsTemplate(IFile source, IMountPoint templateSource, IGenerator generator)
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

        public Guid GeneratorId => Generator.Id;

        public string GroupIdentity => null;

        public string Author => null;

        public string Name { get; }

        public IReadOnlyDictionary<string, string> Tags => new Dictionary<string, string>();

        public Guid ConfigMountPointId => Configuration.MountPoint.Info.MountPointId;

        public string ConfigPlace => Configuration.FullPath;

        public IFileSystemInfo Configuration => SourceFile;

        public IMountPoint Source { get; }

        public IReadOnlyList<string> Classifications => new List<string>();

        public string DefaultName { get; }

        public IFile SourceFile { get; }

        public XDocument VsTemplateFile { get; }

        public IReadOnlyList<CustomParameter> CustomParameters { get; }

        public string ShortName => Name;

        public string Identity => Name;

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