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
     * This task removes internet sources from a given NuGet.config.  In the offline build mode, it removes all
     * feeds that begin with http or https.  In the online build mode, it removes only the internal dnceng feeds that
     * source-build does not have access to.
     */
    public class RemoveInternetSourcesFromNuGetConfig : Task
    {
        [Required]
        public string NuGetConfigFile { get; set; }

        /// <summary>
        /// Whether to work in offline mode (remove all internet sources) or online mode (remove only authenticated sources)
        /// </summary>
        public bool BuildWithOnlineSources { get; set; }

        /// <summary>
        /// A list of prefix strings that make the task keep a package source unconditionally. For
        /// example, a source named 'darc-pub-dotnet-aspnetcore-e81033e' will be kept if the prefix
        /// 'darc-pub-dotnet-aspnetcore-' is in this list.
        /// </summary>
        public string[] KeepFeedPrefixes { get; set; }

        public override bool Execute()
        {
            string xml = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(xml);
            XDocument d = XDocument.Parse(xml);
            XElement packageSourcesElement = d.Root.Descendants().First(e => e.Name == "packageSources");
            XElement disabledPackageSourcesElement = d.Root.Descendants().FirstOrDefault(e => e.Name == "disabledPackageSources");

            IEnumerable<XElement> local = packageSourcesElement.Descendants().Where(e =>
            {
                if (e.Name == "add")
                {
                    string feedName = e.Attribute("key").Value;
                    if (KeepFeedPrefixes
                        ?.Any(prefix => feedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        == true)
                    {
                        return true;
                    }

                    string feedUrl = e.Attribute("value").Value;
                    if (BuildWithOnlineSources)
                    {
                        return !( feedUrl.StartsWith("https://pkgs.dev.azure.com/dnceng/_packaging", StringComparison.OrdinalIgnoreCase) ||
                            feedUrl.StartsWith("https://pkgs.dev.azure.com/dnceng/internal/_packaging", StringComparison.OrdinalIgnoreCase) );
                    }
                    else
                    {
                        return !(feedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || feedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
                    }
                }

                return true;
            });

            packageSourcesElement.ReplaceNodes(local.ToArray());

            // Remove disabledPackageSources element so if any internal packages remain, they are used in source-build
            disabledPackageSourcesElement?.ReplaceNodes(new XElement("clear"));

            using (var w = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                d.Save(w);
            }

            return true;
        }
    }
}
