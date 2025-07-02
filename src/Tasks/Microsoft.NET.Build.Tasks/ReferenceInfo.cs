// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal class ReferenceInfo
    {
        public string Name { get; }
        public string Version { get; }
        public string FullPath { get; }
        public string FileName => Path.GetFileName(FullPath);

        public string PackageName { get; }
        public string PackageVersion { get; }
        public string PathInPackage { get; }

        private List<ResourceAssemblyInfo> _resourceAssemblies;
        public List<ResourceAssemblyInfo> ResourceAssemblies
        {
            get { return _resourceAssemblies; }
        }

        private ReferenceInfo(string name, string version, string fullPath,
            string packageName, string packageVersion, string pathInPackage)
        {
            Name = name;
            Version = version;
            FullPath = fullPath;
            PackageName = packageName;
            PackageVersion = packageVersion;
            PathInPackage = pathInPackage;

            _resourceAssemblies = new List<ResourceAssemblyInfo>();
        }

        public static ReferenceInfo[] CreateReferenceInfos(ITaskItem[] referencePaths)
        {
            ReferenceInfo[] referenceInfos = new ReferenceInfo[referencePaths.Length];
            for (int i = 0; i < referencePaths.Length; i++)
            {
                referenceInfos[i] = CreateReferenceInfo(referencePaths[i]);
            }

            return referenceInfos;
        }

        public static ReferenceInfo[] CreateDirectReferenceInfos(
            ITaskItem[] referencePaths,
            ITaskItem[] referenceSatellitePaths,
            LockFileLookup lockFileLookup,
            Func<ITaskItem, bool> isRuntimeAssembly,
            bool includeProjectsNotInAssetsFile)
        {
            bool lockFileContainsProject(ITaskItem referencePath)
            {
                if (lockFileLookup == null)
                {
                    return false;
                }

                if (!IsProjectReference(referencePath))
                {
                    return false;
                }

                if (!includeProjectsNotInAssetsFile)
                {
                    return true;
                }

                string projectName;
                string projectFilePath = referencePath.GetMetadata(MetadataKeys.MSBuildSourceProjectFile);
                if (!string.IsNullOrEmpty(projectFilePath))
                {
                    projectName = Path.GetFileNameWithoutExtension(projectFilePath);
                }
                else
                {
                    // fall back to using the path to the output DLL
                    projectName = Path.GetFileNameWithoutExtension(referencePath.ItemSpec);
                    if (string.IsNullOrEmpty(projectName))
                    {
                        // unexpected - let's assume this project was already included in the assets file.
                        return true;
                    }
                }

                return lockFileLookup.GetProject(projectName) != null;
            }

            ITaskItem[] directReferencePaths = referencePaths
                .Where(r => !lockFileContainsProject(r) && !IsNuGetReference(r) && isRuntimeAssembly(r)).ToArray();

            return CreateFilteredReferenceInfos(directReferencePaths, referenceSatellitePaths);
        }

        private static bool IsNuGetReference(ITaskItem reference)
        {
            return reference.HasMetadataValue("NuGetSourceType")
                && !reference.HasMetadataValue("NuGetIsFrameworkReference", "true");
        }

        public static bool IsProjectReference(ITaskItem reference)
        {
            return reference.HasMetadataValue(MetadataKeys.ReferenceSourceTarget, "ProjectReference");
        }

        public static ReferenceInfo[] CreateDependencyReferenceInfos(
            ITaskItem[] referenceDependencyPaths,
            ITaskItem[] referenceSatellitePaths,
            Func<ITaskItem, bool> isRuntimeAssembly)
        {

            ITaskItem[] indirectReferencePaths = referenceDependencyPaths
                .Where(r => !IsNuGetReference(r) && isRuntimeAssembly(r)).ToArray();

            return CreateFilteredReferenceInfos(indirectReferencePaths, referenceSatellitePaths);
        }

        private static ReferenceInfo[] CreateFilteredReferenceInfos(
            ITaskItem[] referencePaths,
            ITaskItem[] referenceSatellitePaths)
        {
            Dictionary<string, ReferenceInfo> directReferences = new(referencePaths.Length);

            foreach (ITaskItem referencePath in referencePaths)
            {
                ReferenceInfo referenceInfo = CreateReferenceInfo(referencePath);
                directReferences.Add(referenceInfo.FullPath, referenceInfo);
            }

            foreach (ITaskItem referenceSatellitePath in referenceSatellitePaths)
            {
                string originalItemSpec = referenceSatellitePath.GetMetadata("OriginalItemSpec");
                if (!string.IsNullOrEmpty(originalItemSpec))
                {
                    ReferenceInfo referenceInfo;
                    if (directReferences.TryGetValue(originalItemSpec, out referenceInfo))
                    {
                        ResourceAssemblyInfo resourceAssemblyInfo =
                            ResourceAssemblyInfo.CreateFromReferenceSatellitePath(referenceSatellitePath);
                        referenceInfo._resourceAssemblies.Add(resourceAssemblyInfo);
                    }
                }
            }

            return directReferences.Values.ToArray();
        }

        internal static ReferenceInfo CreateReferenceInfo(ITaskItem referencePath)
        {
            string fullPath = referencePath.ItemSpec;
            string name = Path.GetFileNameWithoutExtension(fullPath);
            string version = GetVersion(referencePath);

            var packageName = referencePath.GetMetadata(MetadataKeys.NuGetPackageId);

            var packageVersion = referencePath.GetMetadata(MetadataKeys.NuGetPackageVersion);

            var pathInPackage = referencePath.GetMetadata(MetadataKeys.PathInPackage);

            return new ReferenceInfo(name, version, fullPath,
                packageName, packageVersion, pathInPackage);
        }

        private static string GetVersion(ITaskItem referencePath)
        {
            string version = referencePath.GetMetadata("Version");

            if (string.IsNullOrEmpty(version))
            {
                string fusionName = referencePath.GetMetadata("FusionName");
                if (!string.IsNullOrEmpty(fusionName))
                {
                    AssemblyName assemblyName = new(fusionName);
                    version = assemblyName.Version?.ToString();
                }

                if (string.IsNullOrEmpty(version))
                {
                    // Use 0.0.0.0 as placeholder, if we can't find a version any
                    // other way
                    version = "0.0.0.0";
                }
            }

            return version;
        }
    }
}
