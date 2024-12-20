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

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    /*
     * This task removes internet sources from a given NuGet.config.  In the offline build mode, it removes all
     * feeds that begin with http or https.  In the online build mode, it removes only the internal dnceng feeds that
     * source-build does not have access to.
     */
    public class RemoveInternetSourcesFromNuGetConfig : Task
    {
        [Required]
        public required string NuGetConfigFile { get; set; }

        /// <summary>
        /// Whether to work in offline mode (remove all internet sources) or online mode (remove only authenticated sources)
        /// </summary>
        public bool BuildWithOnlineFeeds { get; set; }

        /// <summary>
        /// A list of prefix strings that make the task keep a package source unconditionally. For
        /// example, a source named 'darc-pub-dotnet-aspnetcore-e81033e' will be kept if the prefix
        /// 'darc-pub-dotnet-aspnetcore-' is in this list.
        /// </summary>
        public string[] KeepFeedPrefixes { get; set; } = [];

        private readonly string[] Sections = [ "packageSources" ];

        public override bool Execute()
        {
            string xml = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(xml);
            XDocument d = XDocument.Parse(xml);
            XElement? disabledPackageSourcesElement = d.Root?.Descendants().FirstOrDefault(e => e.Name == "disabledPackageSources");

            foreach (string sectionName in Sections)
            {
                ProcessSection(d, sectionName);
            }

            // Remove disabledPackageSources element so if any internal packages remain, they are used in source-build
            disabledPackageSourcesElement?.ReplaceNodes(new XElement("clear"));

            using (var w = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                d.Save(w);
            }

            return true;
        }

        private void ProcessSection(XDocument d, string sectionName)
        {
            XElement? sectionElement = d.Root?.Descendants().FirstOrDefault(e => e.Name == sectionName);
            if (sectionElement == null)
            {
                return;
            }

            IEnumerable<XElement> local = sectionElement.Descendants().Where(e =>
            {
                if (e.Name == "add")
                {
                    string? feedName = e.Attribute("key")?.Value;
                    if (feedName != null &&
                        KeepFeedPrefixes
                        ?.Any(prefix => feedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        == true)
                    {
                        return true;
                    }

                    string? feedUrl = e.Attribute("value")?.Value;
                    if (feedUrl != null)
                    {
                        if (BuildWithOnlineFeeds)
                        {
                            return !(feedUrl.StartsWith("https://pkgs.dev.azure.com/dnceng/_packaging", StringComparison.OrdinalIgnoreCase) ||
                                feedUrl.StartsWith("https://pkgs.dev.azure.com/dnceng/internal/_packaging", StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            return !(feedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || feedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }

                return true;
            });

            sectionElement.ReplaceNodes(local.ToArray());
        }
    }
}
