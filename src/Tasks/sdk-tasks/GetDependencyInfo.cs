// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Gets version and commit of a dependency by its name
    /// from eng/Version.Details.xml
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class GetDependencyInfo : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
        public TaskEnvironment? TaskEnvironment { get; set; }

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
                string absolutePath = TaskEnvironment?.GetAbsolutePath(VersionDetailsXmlFile) ?? VersionDetailsXmlFile;
                XDocument document;
                using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    document = XDocument.Load(stream);
                }
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
