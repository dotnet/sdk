// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Gets version and commit of a dependency by its name
    /// from eng/Version.Details.xml
    /// </summary>
    public class GetDependencyInfo : Task
    {
        [Required]
        public string VersionDetailsXmlFile { get; set; }

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
                XDocument document = XDocument.Load(VersionDetailsXmlFile);
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
                Log.LogWarning($"GetComponentCommit failed for VersionDetailsXmlFile={VersionDetailsXmlFile}, DependencyName={DependencyName}: {ex}");
            }

            return true;
        }
    }
}
