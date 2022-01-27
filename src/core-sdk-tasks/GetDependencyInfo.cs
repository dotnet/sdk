// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    /// <summary>
    /// Gets version and commit of a dependency by its name
    /// from eng/Version.Details.xml
    /// </summary>
    public class GetDependencyInfo : Task
    {
        [Required]
        public string DotnetInstallerCommit { get; set; }

        [Required]
        public string DependencyName { get; set; }

        [Output]
        public string DependencyVersion { get; set; }

        [Output]
        public string DependencyCommit { get; set; }

        public override bool Execute()
        {
            try
            {
                using Stream file = new HttpClient().GetStreamAsync(
                        $"https://raw.githubusercontent.com/dotnet/installer/{DotnetInstallerCommit}/eng/Version.Details.xml")
                    .Result;

                XDocument document = XDocument.Load(file);
                XElement dependency = document
                    .Element("Dependencies")?
                    .Element("ProductDependencies")?
                    .Elements("Dependency")
                    .FirstOrDefault(d => DependencyName.Equals(d.Attribute("Name")?.Value));

                if (dependency != null)
                {
                    DependencyVersion = dependency.Attribute("Version")?.Value;
                    DependencyCommit = dependency.Element("Sha")?.Value;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"GetComponentCommit failed for DotnetInstallerCommit={DotnetInstallerCommit}, DependencyName={DependencyName}: {ex}");
            }
            return true;
        }
    }
}
