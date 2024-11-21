// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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
     * This task adds a package source entry to a well-formed NuGet.Config file. If a package source key is already present, then
     * the value of the package source entry is changed. Otherwise, the package source is added as the first element in the list, after any clear
     * elements (if present).
     */
    public class AddPackageSourcesToNuGetConfig : Task
    {
        // NuGet.config constants
        private const string PackageSourcesElement = "packageSources";
        private const string AddSourcesElement = "add";
        private const string ClearSourcesElement = "clear";
        private const string KeySourcesAttribute = "key";
        private const string ValueSourcesAttribute = "value";

        // MSBuild item metadata
        private const string PackageSourceItemValueMetadata = "Value";

        /// <summary>
        /// Path to the NuGet Config file to update.
        /// </summary>
        [Required]
        public required string NuGetConfigFile { get; set; }

        /// <summary>
        /// PackageSources items with identity as the package source key attribute
        /// and the 'value' metadata as the package source value attribute. 
        /// </summary>
        [Required]
        public required ITaskItem[] PackageSources { get; set; }

        /// <summary>
        /// Add the package source element only if an element with the same package source key doesn't exist. Otherwise
        /// replace the package source value of the existing element.
        /// </summary>
        public bool SkipIfPackageSourceKeyAlreadyExists { get; set; } = false;

        /// <summary>
        /// Add the package source element only if an element with the same package source value doesn't exist.
        /// </summary>
        public bool SkipIfPackageSourceValueAlreadyExists { get; set; } = true;

        public override bool Execute()
        {
            string nuGetConfigContent = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(nuGetConfigContent);
            XDocument xDocument = XDocument.Parse(nuGetConfigContent);
            XElement? packageSourcesElement = xDocument.Root!.Descendants().SingleOrDefault(e => e.Name == nameof(PackageSourcesElement));

            if (packageSourcesElement is null)
            {
                throw new Exception($"NuGet config file '{NuGetConfigFile}' is malformed as it doesn't have a '{PackageSourcesElement}' element.");
            }

            XElement? lastPackageSourceClearElement = packageSourcesElement.Descendants().LastOrDefault(e => e.Name == nameof(ClearSourcesElement));

            foreach (ITaskItem packageSource in PackageSources)
            {
                string packageSourceKey = packageSource.ItemSpec;
                string packageSourceValue = packageSource.GetMetadata(nameof(PackageSourceItemValueMetadata));

                // Check if the package source value already exists in the NuGet.Config file. If it does, skip adding it when SkipIfPackageSourceValueAlreadyExists is true.
                bool hasExisitingPackageSourceValue = packageSourcesElement.Descendants().Any(e => e.Name == nameof(AddSourcesElement) && e.Attribute(XName.Get(nameof(ValueSourcesAttribute)))?.Value == packageSourceValue);
                if (hasExisitingPackageSourceValue && SkipIfPackageSourceValueAlreadyExists)
                {
                    continue;
                }

                // Check if the package source key already exists in the NuGet.Config file. If it does, skip modifying it when SkipIfPackageSourceKeyAlreadyExists is true.
                XElement? existingPackageSourceElement = packageSourcesElement.Descendants().SingleOrDefault(e => e.Name == nameof(AddSourcesElement) && e.Attribute(XName.Get(nameof(KeySourcesAttribute)))?.Value == packageSourceKey);
                if (existingPackageSourceElement is not null && SkipIfPackageSourceKeyAlreadyExists)
                {
                    continue;
                }

                XElement newPackageSourceElement = new XElement(nameof(AddSourcesElement), new XAttribute(nameof(KeySourcesAttribute), packageSourceKey), new XAttribute(nameof(ValueSourcesAttribute), packageSourceValue));
                if (existingPackageSourceElement is not null)
                {
                    existingPackageSourceElement.ReplaceWith(newPackageSourceElement);
                }
                else
                {
                    if (lastPackageSourceClearElement is null)
                    {
                        lastPackageSourceClearElement = new XElement(nameof(ClearSourcesElement));
                        packageSourcesElement.AddFirst(lastPackageSourceClearElement);
                    }

                    lastPackageSourceClearElement.AddAfterSelf(newPackageSourceElement);
                }
            }

            using (XmlWriter xmlWriter = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                xDocument.Save(xmlWriter);
            }

            return true;
        }
    }
}
