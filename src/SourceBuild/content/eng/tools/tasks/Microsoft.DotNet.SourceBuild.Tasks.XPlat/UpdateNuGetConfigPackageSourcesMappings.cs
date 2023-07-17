// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    /*
     * This task updates the package source mappings in the NuGet.Config.
     * If package source mappings are used, source-build packages sources will be added with the cumulative package patterns
     * for all of the existing package sources. When building offline, the existing package source mappings will be removed;
     * otherwise they will be preserved after the source-build sources.
     */
    public class UpdateNuGetConfigPackageSourcesMappings : Task
    {
        [Required]
        public string NuGetConfigFile { get; set; }

        /// <summary>
        /// Whether to work in offline mode (remove all internet sources) or online mode (remove only authenticated sources)
        /// </summary>
        public bool BuildWithOnlineSources { get; set; }

        /// <summary>
        /// A list of all source-build specific NuGet sources.
        /// </summary>
        public string[] SourceBuildSources { get; set; }

        public override bool Execute()
        {
            string xml = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(xml);
            XDocument document = XDocument.Parse(xml);
            XElement pkgSrcMappingElement = document.Root.Descendants().FirstOrDefault(e => e.Name == "packageSourceMapping");

            if (pkgSrcMappingElement == null)
            {
                return true;
            }

            // Union all package sources to get the distinct list.  These will get added to the source-build sources.
            string[] packagePatterns = pkgSrcMappingElement.Descendants()
                .Where(e => e.Name == "packageSource")
                .SelectMany(e => e.Descendants().Where(e => e.Name == "package"))
                .Select(e => e.Attribute("pattern").Value)
                .Distinct()
                .ToArray();

            if (!BuildWithOnlineSources)
            {
                // When building offline remove all packageSourceMappings.
                pkgSrcMappingElement?.ReplaceNodes(new XElement("clear"));
            }

            XElement pkgSrcMappingClearElement = pkgSrcMappingElement.Descendants().FirstOrDefault(e => e.Name == "clear");
            if (pkgSrcMappingClearElement == null)
            {
                pkgSrcMappingClearElement = new XElement("clear");
                pkgSrcMappingElement.AddFirst(pkgSrcMappingClearElement);
            }

            foreach (string packageSource in SourceBuildSources)
            {
                XElement pkgSrc = new XElement("packageSource", new XAttribute("key", packageSource));
                foreach (string packagePattern in packagePatterns)
                {
                    pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
                }

                pkgSrcMappingClearElement.AddAfterSelf(pkgSrc);
            }

            using (var writer = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                document.Save(writer);
            }

            return true;
        }
    }
}
