// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    /*
     * This task updates the package source mappings in the NuGet.Config using the following logic:
     * Add all packages from current source-build sources, i.e. source-built-*, reference-packages.
     * For previously source-built sources (PSB), add only the packages that do not exist in any of the current source-built sources.
     * Also add PSB packages if that package version does not exist in current package sources.
     * In offline build, remove all existing package source mappings for online sources.
     * In online build, add online source mappings for all discovered packages from local sources.
     * In online build, if NuGet.config didn't originally have any mappings, additionally,
     * add default "*" pattern to all online source mappings.
     */
    public class UpdateNuGetConfigPackageSourcesMappings : Task
    {
        [Required]
        public string NuGetConfigFile { get; set; }

        /// <summary>
        /// Whether to work in offline mode (remove all internet sources) or online mode (remove only authenticated sources)
        /// </summary>
        public bool BuildWithOnlineFeeds { get; set; }

        /// <summary>
        /// A list of all source-build specific NuGet sources.
        /// </summary>
        public string[] SourceBuildSources { get; set; }

        [Required]
        public string SbrpRepoSrcPath { get; set; }

        [Required]
        public string SourceBuiltSourceNamePrefix { get; set; }

        public string SbrpCacheSourceName { get; set; }

        public string ReferencePackagesSourceName { get; set; }

        public string PreviouslySourceBuiltSourceName { get; set; }

        public string PrebuiltSourceName { get; set; }

        public string[] CustomSources { get; set; }

        // allSourcesPackages and oldSourceMappingPatterns contain 'package source', 'list of packages' mappings
        private Dictionary<string, List<string>> allSourcesPackages = [];
        private Dictionary<string, List<string>> oldSourceMappingPatterns = [];

        // allOldSourceMappingPatterns is a union of all patterns from oldSourceMappingPatterns
        List<string> allOldSourceMappingPatterns = [];

        // All other dictionaries are: 'package id', 'list of package versions'
        private Dictionary<string, List<string>> currentPackages = [];
        private Dictionary<string, List<string>> referencePackages = [];
        private Dictionary<string, List<string>> previouslySourceBuiltPackages = [];
        private Dictionary<string, List<string>> prebuiltPackages = [];

        public override bool Execute()
        {
            string xml = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(xml);
            XDocument document = XDocument.Parse(xml);
            XElement pkgSourcesElement = document.Root.Descendants().FirstOrDefault(e => e.Name == "packageSources");
            if (pkgSourcesElement == null)
            {
                Log.LogMessage(MessageImportance.Low, "Package sources are missing.");

                return true;
            }

            XElement pkgSrcMappingElement = document.Root.Descendants().FirstOrDefault(e => e.Name == "packageSourceMapping");
            if (pkgSrcMappingElement == null)
            {
                pkgSrcMappingElement = new XElement("packageSourceMapping");
                document.Root.Add(pkgSrcMappingElement);
            }

            DiscoverPackagesFromAllSourceBuildSources(pkgSourcesElement);

            // Discover all SBRP packages if source-build-reference-package-cache source is present in NuGet.config
            XElement sbrpCacheSourceElement = GetElement(pkgSourcesElement, "add", SbrpCacheSourceName);
            if (sbrpCacheSourceElement != null)
            {
                DiscoverPackagesFromSbrpCacheSource();
            }

            // If building online, enumerate any existing package source mappings and filter
            // to remove packages that are present in any local source-build source
            if (BuildWithOnlineFeeds && pkgSrcMappingElement != null)
            {
                GetExistingFilteredSourceMappings(pkgSrcMappingElement);
            }

            // Remove all packageSourceMappings
            pkgSrcMappingElement.ReplaceNodes(new XElement("clear"));

            XElement pkgSrcMappingClearElement = pkgSrcMappingElement.Descendants().FirstOrDefault(e => e.Name == "clear");

            // Add package source mappings for local package sources
            foreach (string packageSource in allSourcesPackages.Keys)
            {
                // Skip sources with zero package patterns
                if (allSourcesPackages[packageSource]?.Count > 0)
                {
                    var pkgSrc = GetPackageMappingsElementForSource(packageSource);
                    if (pkgSrc.Elements().Any())
                    {
                        pkgSrcMappingClearElement.AddAfterSelf(pkgSrc);
                    }
                }
            }

            // When building online add the filtered mappings from original online sources.
            // If there are none, add default mappings for all online sources.
            if (BuildWithOnlineFeeds)
            {
                foreach (var entry in oldSourceMappingPatterns)
                {
                    // Skip sources with zero package patterns
                    if (entry.Value?.Count > 0)
                    {
                        pkgSrcMappingElement.Add(GetPackageMappingsElementForSource(entry.Key, entry.Value));
                    }
                }

                // Union all package sources to get the distinct list. Remove all original patterns
                // from online feeds that were unique to those feeds.
                //
                // These will get added to
                // all custom sources and all online sources based on the following logic:
                // If there were existing mappings for online feeds, add cummulative mappings
                // from all feeds to these two.
                // If there were no existing mappings, add default mappings for all online feeds.
                List<string> packagePatterns = pkgSrcMappingElement.Descendants()
                    .Where(e => e.Name == "packageSource")
                    .SelectMany(e => e.Descendants().Where(e => e.Name == "package" && !allOldSourceMappingPatterns.Contains(e.Attribute("pattern").Value)))
                    .Select(e => e.Attribute("pattern").Value)
                    .Distinct()
                    .ToList();

                if (oldSourceMappingPatterns.Count == 0)
                {
                    packagePatterns.Add("*");
                }

                AddMappingsForCustomSources(pkgSrcMappingElement, pkgSourcesElement, packagePatterns);
                AddMappingsForOnlineSources(pkgSrcMappingElement, pkgSourcesElement, packagePatterns);
            }

            using (var writer = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                document.Save(writer);
            }

            return true;
        }

        private void AddMappingsForCustomSources(XElement pkgSrcMappingElement, XElement pkgSourcesElement, List<string> packagePatterns)
        {
            if (CustomSources == null)
            {
                return;
            }

            foreach (string sourceName in CustomSources)
            {
                if (null != GetElement(pkgSourcesElement, "add", sourceName))
                {
                    AddSourceMappings(pkgSrcMappingElement, sourceName, packagePatterns);

                    // Add all old source mapping patterns for custom sources.
                    // Unlike local sources, custom sources cannot be enumerated.
                    XElement pkgSrcElement = GetElement(pkgSrcMappingElement, "packageSource", sourceName);
                    if (pkgSrcElement != null)
                    {
                        foreach (string pattern in allOldSourceMappingPatterns)
                        {
                            pkgSrcElement.Add(new XElement("package", new XAttribute("pattern", pattern)));
                        }
                    }
                }
            }
        }

        private void AddSourceMappings(XElement pkgSrcMappingElement, string sourceName, List<string> packagePatterns)
        {
            XElement pkgSrc;

            XElement existingPkgSrcElement = GetElement(pkgSrcMappingElement, "packageSource", sourceName);
            if (existingPkgSrcElement != null)
            {
                pkgSrc = existingPkgSrcElement;
            }
            else if (packagePatterns.Count > 0)
            {
                pkgSrc = new XElement("packageSource", new XAttribute("key", sourceName));
                pkgSrcMappingElement.Add(pkgSrc);
            }
            else
            {
                return;
            }

            foreach (string packagePattern in packagePatterns)
            {
                pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
            }
        }

        private void AddMappingsForOnlineSources(XElement pkgSrcMappingElement, XElement pkgSourcesElement, List<string> packagePatterns)
        {
            foreach (string sourceName in pkgSourcesElement
                .Descendants()
                .Where(e => e.Name == "add" &&
                        !SourceBuildSources.Contains(e.Attribute("key").Value) &&
                        // SBRP Cache source is not in SourceBuildSources, skip it as it's not an online source
                        !(e.Attribute("key").Value == SbrpCacheSourceName))
                .Select(e => e.Attribute("key").Value)
                .Distinct())
            {
                AddSourceMappings(pkgSrcMappingElement, sourceName, packagePatterns);
            }
        }

        private XElement GetPackageMappingsElementForSource(string key, List<string> value)
        {
            XElement pkgSrc = new XElement("packageSource", new XAttribute("key", key));
            foreach (string pattern in value)
            {
                pkgSrc.Add(new XElement("package", new XAttribute("pattern", pattern)));
            }

            return pkgSrc;
        }

        private XElement GetPackageMappingsElementForSource(string packageSource)
        {
            bool isCurrentSourceBuiltSource =
                packageSource.StartsWith(SourceBuiltSourceNamePrefix) ||
                packageSource.Equals(SbrpCacheSourceName) ||
                packageSource.Equals(ReferencePackagesSourceName);

            XElement pkgSrc = new XElement("packageSource", new XAttribute("key", packageSource));
            foreach (string packagePattern in allSourcesPackages[packageSource])
            {
                // Add all packages from current source-built sources.
                // For previously source-built and prebuilt sources add only packages
                // where version does not exist in current source-built sources.
                if (isCurrentSourceBuiltSource || !currentPackages.ContainsKey(packagePattern))
                {
                    pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
                }
                else if (packageSource.Equals(PreviouslySourceBuiltSourceName))
                {
                    AddPackageSourceMappingIfPackageVersionsNotInCurrentPackages(pkgSrc, packagePattern, previouslySourceBuiltPackages);
                }
                else if (packageSource.Equals(PrebuiltSourceName))
                {
                    AddPackageSourceMappingIfPackageVersionsNotInCurrentPackages(pkgSrc, packagePattern, prebuiltPackages);
                }
                else // unknown/unexpected source
                {
                    throw new UnreachableException($"Unexpected package source name: {packageSource}");
                }
            }

            return pkgSrc;
        }

        private void AddPackageSourceMappingIfPackageVersionsNotInCurrentPackages(XElement pkgSrc, string packagePattern, Dictionary<string, List<string>> packages)
        {
            foreach (string version in packages[packagePattern])
            {
                // If any package version is in current packages, skip this package pattern
                if (currentPackages[packagePattern].Contains(version))
                {
                    return;
                }
            }

            pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
        }

        private void DiscoverPackagesFromAllSourceBuildSources(XElement pkgSourcesElement)
        {
            foreach (string packageSource in SourceBuildSources)
            {
                XElement sourceElement = GetElement(pkgSourcesElement, "add", packageSource);
                if (sourceElement == null)
                {
                    continue;
                }

                string path = sourceElement.Attribute("value").Value;
                if (!Directory.Exists(path))
                {
                    continue;
                }

                string[] packages = Directory.GetFiles(path, "*.nupkg", SearchOption.AllDirectories);
                Array.Sort(packages);
                foreach (string package in packages)
                {
                    NupkgInfo info = GetNupkgInfo(package);
                    string id = info.Id.ToLower();
                    string version = info.Version.ToLower();

                    // Add package with version to appropriate hashtable
                    if (packageSource.StartsWith(SourceBuiltSourceNamePrefix))
                    {
                        AddToDictionary(currentPackages, id, version);
                    }
                    else if (packageSource.Equals(ReferencePackagesSourceName))
                    {
                        AddToDictionary(referencePackages, id, version);
                    }
                    else if (packageSource.Equals(PreviouslySourceBuiltSourceName))
                    {
                        AddToDictionary(previouslySourceBuiltPackages, id, version);
                    }
                    else if (packageSource.Equals(PrebuiltSourceName))
                    {
                        AddToDictionary(prebuiltPackages, id, version);
                    }
                    else // unknown/unexpected source
                    {
                        throw new UnreachableException($"Unexpected package source name: {packageSource}");
                    }

                    AddToDictionary(allSourcesPackages, packageSource, id);
                }
            }
        }

        private void DiscoverPackagesFromSbrpCacheSource()
        {
            // 'source-build-reference-package-cache' is a dynamic source, populated by SBRP build.
            // Discover all SBRP packages from checked in nuspec files.

            if (!Directory.Exists(SbrpRepoSrcPath))
            {
                throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "SBRP repo root does not exist in expected path: {0}", SbrpRepoSrcPath));
            }

            string[] nuspecFiles = Directory.GetFiles(SbrpRepoSrcPath, "*.nuspec", SearchOption.AllDirectories);
            Array.Sort(nuspecFiles);
            foreach (string nuspecFile in nuspecFiles)
            {
                try
                {
                    using Stream stream = File.OpenRead(nuspecFile);
                    NupkgInfo info = GetNupkgInfo(stream);
                    string id = info.Id.ToLower();
                    string version = info.Version.ToLower();

                    AddToDictionary(currentPackages, id, version);
                    AddToDictionary(allSourcesPackages, SbrpCacheSourceName, id);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "Invalid nuspec file: {0}", nuspecFile), ex);
                }
            }
        }

        private XElement GetElement(XElement pkgSourcesElement, string name, string key)
        {
            return pkgSourcesElement.Descendants().FirstOrDefault(e => e.Name == name && e.Attribute("key").Value == key);
        }

        private void GetExistingFilteredSourceMappings(XElement pkgSrcMappingElement)
        {
            foreach (XElement packageSource in pkgSrcMappingElement.Descendants().Where(e => e.Name == "packageSource"))
            {
                List<string> filteredPatterns = new List<string>();
                foreach (XElement package in packageSource.Descendants().Where(e => e.Name == "package"))
                {
                    string pattern = package.Attribute("pattern").Value.ToLower();
                    if (!currentPackages.ContainsKey(pattern) &&
                        !referencePackages.ContainsKey(pattern) &&
                        !previouslySourceBuiltPackages.ContainsKey(pattern) &&
                        !prebuiltPackages.ContainsKey(pattern))
                    {
                        filteredPatterns.Add(pattern);
                        if (!allOldSourceMappingPatterns.Contains(pattern))
                        {
                            allOldSourceMappingPatterns.Add(pattern);
                        }
                    }
                }

                oldSourceMappingPatterns.Add(packageSource.Attribute("key").Value, filteredPatterns);
            }
        }

        private void AddToDictionary(Dictionary<string, List<string>> dictionary, string key, string value)
        {
            if (dictionary.TryGetValue(key, out List<string> values))
            {
                if (!values.Contains(value))
                {
                    values.Add(value);
                }
            }
            else
            {
                dictionary.Add(key, [value]);
            }
        }

        /// <summary>
        /// Get nupkg info, id and version, from nupkg file.
        /// </summary>
        private NupkgInfo GetNupkgInfo(string path)
        {
            try
            {
                using Stream stream = File.OpenRead(path);
                ZipArchive zipArchive = new(stream, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in zipArchive.Entries)
                {
                    if (entry.Name.EndsWith(".nuspec"))
                    {
                        using Stream nuspecFileStream = entry.Open();
                        return GetNupkgInfo(nuspecFileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "Invalid package: {0}", path), ex);
            }

            throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "Did not extract nuspec file from package: {0}", path));
        }

        /// <summary>
        /// Get nupkg info, id and version, from nuspec stream.
        /// </summary>
        private NupkgInfo GetNupkgInfo(Stream nuspecFileStream)
        {
            XDocument doc = XDocument.Load(nuspecFileStream, LoadOptions.PreserveWhitespace);
            XElement metadataElement = doc.Descendants().First(c => c.Name.LocalName.ToString() == "metadata");
            return new NupkgInfo(
                    metadataElement.Descendants().First(c => c.Name.LocalName.ToString() == "id").Value,
                    metadataElement.Descendants().First(c => c.Name.LocalName.ToString() == "version").Value);
        }

        private class NupkgInfo
        {
            public NupkgInfo(string id, string version)
            {
                Id = id;
                Version = version;
            }

            public string Id { get; }
            public string Version { get; }
        }
    }
}
