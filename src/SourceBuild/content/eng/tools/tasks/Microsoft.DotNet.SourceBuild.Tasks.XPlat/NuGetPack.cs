// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet;
using NuGet.Versioning;
using NuGet.Packaging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class NuGetPack : PackagingTask
    {
        /// <summary>
        /// Target file paths to exclude when building the lib package for symbol server scenario
        /// Copied from https://github.com/NuGet/NuGet.Client/blob/59433c7bacaae435a2cfe343cd441ea710579304/src/NuGet.Core/NuGet.Commands/PackCommandRunner.cs#L48
        /// </summary>
        private static readonly string[] _libPackageExcludes = new[] {
            @"**\*.pdb".Replace('\\', Path.DirectorySeparatorChar),
            @"src\**\*".Replace('\\', Path.DirectorySeparatorChar)
        };

        /// <summary>
        /// Target file paths to exclude when building the symbols package for symbol server scenario
        /// </summary>
        private static readonly string[] _symbolPackageExcludes = new[] {
            @"content\**\*".Replace('\\', Path.DirectorySeparatorChar),
            @"tools\**\*.ps1".Replace('\\', Path.DirectorySeparatorChar)
        };

        private static readonly string _defaultPackedPackagePrefix = "transport";
        private static readonly string _symbolsPackageExtension = ".symbols.nupkg";
        private static readonly string _packageExtension = ".nupkg";

        [Required]
        public ITaskItem[] Nuspecs
        {
            get;
            set;
        }

        [Required]
        public string OutputDirectory
        {
            get;
            set;
        }

        public string BaseDirectory
        {
            get;
            set;
        }

        public string PackageVersion
        {
            get;
            set;
        }

        public bool ExcludeEmptyDirectories
        {
            get;
            set;
        }
        // Create an additional ".symbols.nupkg" package
        public bool CreateSymbolPackage
        {
            get;
            set;
        }
        // Include symbols in standard package
        public bool IncludeSymbolsInPackage
        {
            get;
            set;
        }
        // Create an additional "packed package" that includes lib and src / symbols
        public bool CreatePackedPackage
        {
            get;
            set;
        }
        /// <summary>
        /// Nuspec files can contain properties that are substituted with values at pack time
        /// This task property passes through the nuspect properties.
        /// Each item is a string with the syntax <key>=<value>
        /// String validation for <key> and <value> is deffered to the Nuget APIs
        /// </summary>
        public ITaskItem[] NuspecProperties
        {
            get;
            set;
        }

        public ITaskItem[] AdditionalLibPackageExcludes
        {
            get;
            set;
        }

        public ITaskItem[] AdditionalSymbolPackageExcludes
        {
            get;
            set;
        }

        /// <summary>
        /// If set, the symbol package is placed in the given directory. Otherwise OutputDirectory is used.
        /// </summary>
        public string SymbolPackageOutputDirectory
        {
            get;
            set;
        }

        public string PackedPackageNamePrefix
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (Nuspecs == null || Nuspecs.Length == 0)
            {
                Log.LogError("Nuspecs argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(OutputDirectory))
            {
                Log.LogError("OuputDirectory argument must be specified");
                return false;
            }

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            Func<string, string> nuspecPropertyProvider = GetNuspecPropertyProviderFunction(NuspecProperties);

            foreach (var nuspec in Nuspecs)
            {
                string nuspecPath = nuspec.GetMetadata("FullPath");

                if (!File.Exists(nuspecPath))
                {
                    Log.LogError($"Nuspec {nuspecPath} does not exist");
                    continue;
                }

                Manifest manifest = GetManifest(nuspecPath, nuspecPropertyProvider, false);
                string nupkgPath = GetPackageOutputPath(nuspecPath, manifest, false, false);
                Pack(nuspecPath, nupkgPath, manifest, IncludeSymbolsInPackage);

                bool packSymbols = CreateSymbolPackage || CreatePackedPackage;
                if (CreateSymbolPackage)
                {
                    Manifest symbolsManifest = GetManifest(nuspecPath, nuspecPropertyProvider, false);
                    nupkgPath = GetPackageOutputPath(nuspecPath, symbolsManifest, true, false);
                    Pack(nuspecPath, nupkgPath, symbolsManifest, packSymbols);
                }

                if (CreatePackedPackage)
                {
                    Manifest packedManifest = GetManifest(nuspecPath, nuspecPropertyProvider, true);
                    nupkgPath = GetPackageOutputPath(nuspecPath, packedManifest, false, true);
                    Pack(nuspecPath, nupkgPath, packedManifest, packSymbols);
                }
            }

            return !Log.HasLoggedErrors;
        }

        private static Func<string, string> GetNuspecPropertyProviderFunction(ITaskItem[] nuspecProperties)
        {
            return nuspecProperties == null ? null : NuspecPropertyStringProvider.GetNuspecPropertyProviderFunction(nuspecProperties.Select(p => p.ItemSpec).ToArray());
        }

        private Manifest GetManifest(string nuspecPath, Func<string, string> nuspecPropertyProvider, bool isPackedPackage)
        {
            using (var nuspecFile = File.Open(nuspecPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                string baseDirectoryPath = (string.IsNullOrEmpty(BaseDirectory)) ? Path.GetDirectoryName(nuspecPath) : BaseDirectory;
                Manifest manifest = Manifest.ReadFrom(nuspecFile, nuspecPropertyProvider, false);

                if (isPackedPackage)
                {
                    manifest = TransformManifestToPackedPackageManifest(manifest);
                }
                return manifest;
            }
        }

        private string GetPackageOutputPath(string nuspecPath, Manifest manifest, bool isSymbolsPackage, bool applyPrefix)
        {
            string id = manifest.Metadata.Id;

            if (String.IsNullOrEmpty(id))
            {
                Log.LogError($"Nuspec {nuspecPath} does not contain a valid Id");
                return string.Empty;
            }

            // Overriding the Version from the Metadata if one gets passed in.
            if (!string.IsNullOrEmpty(PackageVersion))
            {
                NuGetVersion overrideVersion;
                if (NuGetVersion.TryParse(PackageVersion, out overrideVersion))
                {
                    manifest.Metadata.Version = overrideVersion;
                }
                else
                {
                    Log.LogError($"Failed to parse Package Version: '{PackageVersion}' is not a valid version.");
                }
            }

            string version = manifest.Metadata.Version.ToString();

            if (String.IsNullOrEmpty(version))
            {
                Log.LogError($"Nuspec {nuspecPath} does not contain a valid version");
                return string.Empty;
            }

            string nupkgOutputDirectory = OutputDirectory;

            if (isSymbolsPackage && !string.IsNullOrEmpty(SymbolPackageOutputDirectory))
            {
                nupkgOutputDirectory = SymbolPackageOutputDirectory;
            }

            string nupkgExtension = isSymbolsPackage ? _symbolsPackageExtension : _packageExtension;
            return Path.Combine(nupkgOutputDirectory, $"{id}.{version}{nupkgExtension}");
        }

        public void Pack(string nuspecPath, string nupkgPath, Manifest manifest, bool packSymbols)
        {
            bool creatingSymbolsPackage = packSymbols && (Path.GetExtension(nupkgPath) == _symbolsPackageExtension);
            try
            {
                PackageBuilder builder = new PackageBuilder();

                string baseDirectoryPath = (string.IsNullOrEmpty(BaseDirectory)) ? Path.GetDirectoryName(nuspecPath) : BaseDirectory;
                builder.Populate(manifest.Metadata);
                builder.PopulateFiles(baseDirectoryPath, manifest.Files);

                if (creatingSymbolsPackage)
                {
                    // For symbols packages, filter out excludes
                    PathResolver.FilterPackageFiles(
                        builder.Files,
                        file => file.Path,
                        SymbolPackageExcludes);

                    // Symbol packages are only valid if they contain both symbols and sources.
                    Dictionary<string, bool> pathHasMatches = LibPackageExcludes.ToDictionary(
                        path => path,
                        path => PathResolver.GetMatches(builder.Files, file => file.Path, new[] { path }).Any());

                    if (!pathHasMatches.Values.Any(i => i))
                    {
                        Log.LogMessage(LogImportance.Low, $"Nuspec {nuspecPath} does not contain symbol or source files. Not creating symbol package.");
                        return;
                    }
                    foreach (var pathPair in pathHasMatches.Where(pathMatchPair => !pathMatchPair.Value))
                    {
                        Log.LogMessage(LogImportance.Low, $"Nuspec {nuspecPath} does not contain any files matching {pathPair.Key}. Not creating symbol package.");
                        return;
                    }
                }
                else if(!packSymbols)
                {
                    // for packages which do not include symbols (not symbols or packed packages), filter lib excludes
                    PathResolver.FilterPackageFiles(
                        builder.Files,
                        file => file.Path,
                        LibPackageExcludes);
                }

                var directory = Path.GetDirectoryName(nupkgPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var fileStream = File.Create(nupkgPath))
                {
                    builder.Save(fileStream);
                }

                Log.LogMessage($"Created '{nupkgPath}'");
            }
            catch (Exception e)
            {
                string packageType = "lib";
                if (creatingSymbolsPackage)
                {
                    packageType = "symbol";
                }
                else if (packSymbols)
                {
                    packageType = "packed";
                }
                Log.LogError($"Error when creating nuget {packageType} package from {nuspecPath}. {e}");
            }
        }

        private Manifest TransformManifestToPackedPackageManifest(Manifest manifest)
        {
            ManifestMetadata manifestMetadata = manifest.Metadata;

            // Update Id
            string _packageNamePrefix = PackedPackageNamePrefix != null ? PackedPackageNamePrefix : _defaultPackedPackagePrefix;
            manifestMetadata.Id = $"{_packageNamePrefix}.{manifestMetadata.Id}";

            // Update dependencies
            List<PackageDependencyGroup> packedPackageDependencyGroups = new List<PackageDependencyGroup>();
            foreach(var dependencyGroup in manifestMetadata.DependencyGroups)
            {
                List<NuGet.Packaging.Core.PackageDependency> packages = new List<NuGet.Packaging.Core.PackageDependency>();
                foreach(var dependency in dependencyGroup.Packages)
                {
                    NuGet.Packaging.Core.PackageDependency package = new NuGet.Packaging.Core.PackageDependency($"{_packageNamePrefix}.{dependency.Id}", dependency.VersionRange, dependency.Include, dependency.Exclude);
                    packages.Add(package);
                }
                PackageDependencyGroup packageDependencyGroup = new PackageDependencyGroup(dependencyGroup.TargetFramework, packages);
                packedPackageDependencyGroups.Add(packageDependencyGroup);
            }
            manifestMetadata.DependencyGroups = packedPackageDependencyGroups;

            // Update runtime.json
            List<ManifestFile> manifestFiles = new List<ManifestFile>();

            foreach(ManifestFile file in manifest.Files)
            {
                string fileName = file.Source;
                if(Path.GetFileName(fileName) == "runtime.json" && file.Target == "")
                {
                    string packedPackageSourcePath = Path.Combine(Path.GetDirectoryName(fileName), string.Join(".", _packageNamePrefix, Path.GetFileName(fileName)));
                    file.Source = File.Exists(packedPackageSourcePath) ? packedPackageSourcePath : fileName;
                    file.Target = "runtime.json";
                }
                manifestFiles.Add(file);
            }
            Manifest packedPackageManifest = new Manifest(manifestMetadata, manifestFiles);
            return manifest;
        }

        private IEnumerable<string> LibPackageExcludes
        {
            get
            {
                return _libPackageExcludes
                    .Concat(AdditionalLibPackageExcludes?.Select(item => item.ItemSpec) ?? Enumerable.Empty<string>());
            }
        }

        private IEnumerable<string> SymbolPackageExcludes
        {
            get
            {
                return _symbolPackageExcludes
                    .Concat(AdditionalSymbolPackageExcludes?.Select(item => item.ItemSpec) ?? Enumerable.Empty<string>());
            }
        }
    }
}
