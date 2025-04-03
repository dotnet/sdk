// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    /*
     * This task adds a source to a well-formed NuGet.Config file. If a source with `SourceName` is already present, then
     * the path of the source is changed. Otherwise, the source is added as the first source in the list, after any clear
     * elements (if present).
     */
    public class AddSourceToNuGetConfig : Task
    {
        [Required]
        public string NuGetConfigFile { get; set; }

        [Required]
        public string SourceName { get; set; }

        [Required]
        public string SourcePath { get; set; }

        public override bool Execute()
        {
            string xml = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(xml);
            XDocument d = XDocument.Parse(xml);
            XElement packageSourcesElement = d.Root.Descendants().First(e => e.Name == "packageSources");
            XElement toAdd = new XElement("add", new XAttribute("key", SourceName), new XAttribute("value", SourcePath));
            XElement clearTag = new XElement("clear");

            XElement exisitingSourceBuildElement = packageSourcesElement.Descendants().FirstOrDefault(e => e.Name == "add" && e.Attribute(XName.Get("key")).Value == SourceName);
            XElement lastClearElement = packageSourcesElement.Descendants().LastOrDefault(e => e.Name == "clear");

            if (exisitingSourceBuildElement != null)
            {
                exisitingSourceBuildElement.ReplaceWith(toAdd);
            }
            else if (lastClearElement != null)
            {
                lastClearElement.AddAfterSelf(toAdd);
            }
            else
            {
                packageSourcesElement.AddFirst(toAdd);
                packageSourcesElement.AddFirst(clearTag);
            }

            using (var w = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                d.Save(w);
            }

            return true;
        }
    }
}
