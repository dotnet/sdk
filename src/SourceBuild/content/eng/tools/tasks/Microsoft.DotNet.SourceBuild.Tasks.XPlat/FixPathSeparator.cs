// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    /*
     * This task replaces both types of path separators ('/' and '\') with the separator for the current
     * platform. This workaround a NuGet issue where `nuget pack` does not translate path separators causing
     * packages that don't appear to have the right assets in them.
     */
    public class FixPathSeparator : Task
    {
        [Required]
        public ITaskItem[] NuSpecFiles { get; set; }

        public override bool Execute()
        {
            foreach (ITaskItem item in NuSpecFiles)
            {
                string pathToNuSpec = item.GetMetadata("FullPath");

                XDocument doc = XDocument.Load(pathToNuSpec);

                XElement contentFilesElement = doc.ElementIgnoringNamespace("package").ElementIgnoringNamespace("metadata").ElementIgnoringNamespace("contentFiles");
                XElement filesElement = doc.ElementIgnoringNamespace("package").ElementIgnoringNamespace("files");

                if (contentFilesElement != null)
                {
                    foreach (XElement element in contentFilesElement.ElementsIgnroingNamespace("files"))
                    {
                        UpdateDirectorySeperatorInAttribute(element, "include");
                        UpdateDirectorySeperatorInAttribute(element, "exclude");
                    }
                }

                if (filesElement != null)
                {
                    foreach (XElement element in filesElement.ElementsIgnroingNamespace("file"))
                    {
                        UpdateDirectorySeperatorInAttribute(element, "src");
                        UpdateDirectorySeperatorInAttribute(element, "target");
                        UpdateDirectorySeperatorInAttribute(element, "exclude");
                    }
                }

                using (FileStream fs = File.Open(pathToNuSpec, FileMode.Truncate))
                {
                    doc.Save(fs);
                }
            }

            return true;
        }

        private static void UpdateDirectorySeperatorInAttribute(XElement element, XName name)
        {
            XAttribute attribute = element.Attribute(name);

            if (attribute != null)
            {
                element.SetAttributeValue(name, attribute.Value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            }
        }
    }

    static class XContainerExtensions
    {
        public static IEnumerable<XElement> ElementsIgnroingNamespace(this XContainer container, XName elementName)
        {
            return container.Elements().Where(e => e.Name.LocalName == elementName.LocalName);
        }

        public static XElement ElementIgnoringNamespace(this XContainer container, XName elementName)
        {
            return container.ElementsIgnroingNamespace(elementName).FirstOrDefault();
        }
    }
}
