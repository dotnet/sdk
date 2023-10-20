// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    public class CheckForPoison : Task
    {
        /// <summary>
        /// The files to check for poison and/or hash matches.  Zips and
        /// nupkgs will be extracted and checked recursively.
        /// %(Identity): Path to the initial set of files.
        /// </summary>
        [Required]
        public ITaskItem[] FilesToCheck { get; set; }

        /// <summary>
        /// The path of the project directory to the FilesToCheck.
        /// </summary>
        [Required]
        public string ProjectDirPath { get; set; }

        /// <summary>
        /// The output path for an XML poison report, if desired.
        /// </summary>
        public string PoisonReportOutputFilePath { get; set; }

        /// <summary>
        /// The path of a previously-generated file hash catalog, if
        /// hash checked is desired.  If not, only assembly attributes
        /// and package marker file checked will be done.
        /// </summary>
        public string HashCatalogFilePath { get; set; }

        /// <summary>
        /// The marker file name to check for in poisoned nupkg files.
        /// </summary>
        public string MarkerFileName { get; set; }

        /// <summary>
        /// If true, fails the build if any poisoned files are found.
        /// </summary>
        public bool FailOnPoisonFound { get; set; }

        /// <summary>
        /// Use this directory instead of the system temp directory for staging.
        /// Intended for Linux systems with limited /tmp space, like Azure VMs.
        /// </summary>
        public string OverrideTempPath { get; set; }

        /// <summary>
        /// Array of files containing lists of non-shipping packages
        /// </summary>
        public ITaskItem[] NonShippingPackagesListFiles { get; set; }

        private static readonly string[] ZipFileExtensions =
        {
            ".zip",
            ".nupkg",
        };

        private static readonly string[] TarFileExtensions =
        {
            ".tar",
        };

        private static readonly string[] TarGzFileExtensions =
        {
            ".tgz",
            ".tar.gz",
        };

        private static readonly string[] FileNamesToSkip =
        {
            "_._",
            "-.-",
            ".bowerrc",
            ".gitignore",
            ".gitkeep",
            ".rels",
            "LICENSE",
            "prefercliruntime",
            "RunCsc",
            "RunVbc",
        };

        private static readonly string[] FileExtensionsToSkip =
        {
            ".config",
            ".cs",
            ".cshtml",
            ".csproj",
            ".css",
            ".db",
            ".editorconfig",
            ".eot",
            ".fs",
            ".fsproj",
            ".h",
            ".html",
            ".ico",
            ".js",
            ".json",
            ".map",
            ".md",
            ".nuspec",
            ".otf",
            ".png",
            ".props",
            ".proto",
            ".proj",
            ".psmdcp",
            ".pubxml",
            ".razor",
            ".rtf",
            ".scss",
            ".sln",
            ".svg",
            ".targets",
            ".transform",
            ".ts",
            ".ttf",
            ".txt",
            ".vb",
            ".vbproj",
            ".win32manifest",
            ".woff",
            ".woff2",
            ".xaml",
            ".xml",
        };

        private const string PoisonMarker = "POISONED";

        private record CandidateFileEntry(string ExtractedPath, string DisplayPath);

        public override bool Execute()
        {
            IEnumerable<PoisonedFileEntry> poisons = GetPoisonedFiles(FilesToCheck.Select(f => f.ItemSpec), HashCatalogFilePath, MarkerFileName);

            // if we should write out the poison report, do that
            if (!string.IsNullOrWhiteSpace(PoisonReportOutputFilePath))
            {
                File.WriteAllText(PoisonReportOutputFilePath, (new XElement("PrebuiltLeakReport", poisons.OrderBy(p => p.Path).Select(p => p.ToXml()))).ToString());
            }

            if (FailOnPoisonFound && poisons.Count() > 0)
            {
                Log.LogError($"Forced build error: {poisons.Count()} marked files leaked to output.  See complete report '{PoisonReportOutputFilePath}' for details.");
                return false;
            }
            else if (poisons.Count() > 0)
            {
                Log.LogWarning($"{poisons.Count()} marked files leaked to output.  See complete report '{PoisonReportOutputFilePath}' for details.");
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Internal helper to allow other tasks to check for poisoned files.
        /// </summary>
        /// <param name="initialCandidates">Initial queue of candidate files (will be cleared when done)</param>
        /// <param name="catalogedPackagesFilePath">File path to the file hash catalog</param>
        /// <param name="markerFileName">Marker file name to check for in poisoned nupkgs</param>
        /// <returns>List of poisoned packages and files found and reasons for each</returns>
        internal IEnumerable<PoisonedFileEntry> GetPoisonedFiles(IEnumerable<string> initialCandidates, string catalogedPackagesFilePath, string markerFileName)
        {
            IEnumerable<string> nonShippingPackages = GetAllNonShippingPackages();
            IEnumerable<CatalogPackageEntry> catalogedPackages = ReadCatalog(catalogedPackagesFilePath);
            var poisons = new List<PoisonedFileEntry>();
            var candidateQueue = new Queue<CandidateFileEntry>(initialCandidates.Select(candidate =>
                new CandidateFileEntry(candidate, Utility.MakeRelativePath(candidate, ProjectDirPath))));

            if (!string.IsNullOrWhiteSpace(OverrideTempPath))
            {
                Directory.CreateDirectory(OverrideTempPath);
            }
            var tempDirName = Path.GetRandomFileName();
            var tempDir = Directory.CreateDirectory(Path.Combine(OverrideTempPath ?? Path.GetTempPath(), tempDirName));

            while (candidateQueue.Any())
            {
                var candidate = candidateQueue.Dequeue();

                // if this is a zip or NuPkg, extract it, check for the poison marker, and
                // add its contents to the list to be checked.
                if (ZipFileExtensions.Concat(TarFileExtensions).Concat(TarGzFileExtensions).Any(e => candidate.ExtractedPath.ToLowerInvariant().EndsWith(e)))
                {
                    Log.LogMessage($"Zip or NuPkg file to check: {candidate.ExtractedPath}");

                    // Skip non-shipping packages
                    if (nonShippingPackages.Contains(Path.GetFileName(candidate.ExtractedPath), StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var tempCheckingDir = Path.Combine(tempDir.FullName, Path.GetFileNameWithoutExtension(candidate.ExtractedPath));
                    PoisonedFileEntry result = ExtractAndCheckZipFileOnly(catalogedPackages, candidate, markerFileName, tempCheckingDir, candidateQueue);
                    if (result != null)
                    {
                        poisons.Add(result);
                    }
                }
                else
                {
                    PoisonedFileEntry result = CheckSingleFile(catalogedPackages, candidate);
                    if (result != null)
                    {
                        poisons.Add(result);
                    }
                }
            }

            tempDir.Delete(true);

            return poisons;
        }

        private IEnumerable<string> GetAllNonShippingPackages()
        {
            if (NonShippingPackagesListFiles != null)
            {
                return NonShippingPackagesListFiles
                    .SelectMany(item => File.ReadAllLines(item.ItemSpec))
                    .Distinct()
                    .ToList();
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        private static PoisonedFileEntry CheckSingleFile(IEnumerable<CatalogPackageEntry> catalogedPackages, CandidateFileEntry candidate)
        {
            // skip some common files that get copied verbatim from nupkgs - LICENSE, _._, etc as well as
            // file types that we never care about - text files, .gitconfig, etc.
            var fileToCheck = candidate.ExtractedPath;

            if (FileNamesToSkip.Any(f => Path.GetFileName(fileToCheck).ToLowerInvariant() == f.ToLowerInvariant()) ||
                FileExtensionsToSkip.Any(e => Path.GetExtension(fileToCheck).ToLowerInvariant() == e.ToLowerInvariant()) ||
                (new FileInfo(fileToCheck).Length == 0))
            {
                return null;
            }

            var poisonEntry = new PoisonedFileEntry();
            poisonEntry.Path = candidate.DisplayPath;

            // There seems to be some weird issues with using file streams both for hashing and assembly loading.
            // Copy everything into a memory stream to avoid these problems.
            var memStream = new MemoryStream();
            using (var stream = File.OpenRead(fileToCheck))
            {
                stream.CopyTo(memStream);
            }

            memStream.Seek(0, SeekOrigin.Begin);
            using (var sha = SHA256.Create())
            {
                poisonEntry.Hash = sha.ComputeHash(memStream);
            }

            foreach (var p in catalogedPackages)
            {
                // This hash can match either the original hash (we couldn't poison the file, or redownloaded it) or
                // the poisoned hash (the obvious failure case of a poisoned file leaked).
                foreach (var matchingCatalogedFile in p.Files.Where(f => f.OriginalHash.SequenceEqual(poisonEntry.Hash) || (f.PoisonedHash?.SequenceEqual(poisonEntry.Hash) ?? false)))
                {
                    poisonEntry.Type |= PoisonType.Hash;
                    var match = new PoisonMatch
                    {
                        File = matchingCatalogedFile.Path,
                        Package = p.Path,
                        PackageId = p.Id,
                        PackageVersion = p.Version,
                    };
                    poisonEntry.Matches.Add(match);
                }
            }

            try
            {
                AssemblyName asm = AssemblyName.GetAssemblyName(fileToCheck);
                if (IsAssemblyPoisoned(fileToCheck))
                {
                    poisonEntry.Type |= PoisonType.AssemblyAttribute;
                }
            }
            catch
            {
                // this is fine, it's just not an assembly.
            }

            return poisonEntry.Type != PoisonType.None ? poisonEntry : null;
        }

        private static bool IsAssemblyPoisoned(string path)
        {
            byte[] buffer = File.ReadAllBytes(path);
            byte[] marker = Encoding.UTF8.GetBytes(PoisonMarker);

            // Start at end of file and go backwards
            // Marker is likely at the end and this saves time when
            // we encounter a poisoned file.
            for (int j = buffer.Length - marker.Length; j >= 0; j--)
            {
                int i;
                for (i = 0; i < marker.Length && buffer[j + i] == marker[i]; i++) ;
                if (i == marker.Length)
                {
                    return true;
                }
            }

            return false;
        }

        private static PoisonedFileEntry ExtractAndCheckZipFileOnly(IEnumerable<CatalogPackageEntry> catalogedPackages, CandidateFileEntry candidate, string markerFileName, string tempDir, Queue<CandidateFileEntry> futureFilesToCheck)
        {
            var poisonEntry = new PoisonedFileEntry();
            var zipToCheck = candidate.ExtractedPath;
            poisonEntry.Path = zipToCheck;

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(zipToCheck))
            {
                poisonEntry.Hash = sha.ComputeHash(stream);
            }

            // first check for a matching poisoned or non-poisoned hash match:
            // - non-poisoned is a potential error where the package was redownloaded.
            // - poisoned is a use of a local package we were not expecting.
            foreach (var matchingCatalogedPackage in catalogedPackages.Where(c => c.OriginalHash.SequenceEqual(poisonEntry.Hash) || (c.PoisonedHash?.SequenceEqual(poisonEntry.Hash) ?? false)))
            {
                poisonEntry.Type |= PoisonType.Hash;
                var match = new PoisonMatch
                {
                    Package = matchingCatalogedPackage.Path,
                    PackageId = matchingCatalogedPackage.Id,
                    PackageVersion = matchingCatalogedPackage.Version,
                };
                poisonEntry.Matches.Add(match);
            }

            // now extract and look for the marker file
            if (ZipFileExtensions.Any(e => zipToCheck.ToLowerInvariant().EndsWith(e)))
            {
                ZipFile.ExtractToDirectory(zipToCheck, tempDir, true);
            }
            else if (TarFileExtensions.Any(e => zipToCheck.ToLowerInvariant().EndsWith(e)))
            {
                Directory.CreateDirectory(tempDir);
                var psi = new ProcessStartInfo("tar", $"xf {zipToCheck} -C {tempDir}");
                Process.Start(psi).WaitForExit();
            }
            else if (TarGzFileExtensions.Any(e => zipToCheck.ToLowerInvariant().EndsWith(e)))
            {
                Directory.CreateDirectory(tempDir);
                var psi = new ProcessStartInfo("tar", $"xzf {zipToCheck} -C {tempDir}");
                Process.Start(psi).WaitForExit();
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Don't know how to decompress {zipToCheck}");
            }

            if (!string.IsNullOrWhiteSpace(markerFileName) && File.Exists(Path.Combine(tempDir, markerFileName)))
            {
                poisonEntry.Type |= PoisonType.NupkgFile;
            }

            foreach (var child in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                string displayPath = $"{candidate.DisplayPath}/{child.Replace(tempDir, string.Empty).TrimStart(Path.DirectorySeparatorChar)}";

                futureFilesToCheck.Enqueue(new CandidateFileEntry(child, displayPath));
            }

            return poisonEntry.Type != PoisonType.None ? poisonEntry : null;
        }

        private static IEnumerable<CatalogPackageEntry> ReadCatalog(string hashCatalogFilePath)
        {
            // catalog is optional, we can also just check assembly properties or nupkg marker files
            if (string.IsNullOrWhiteSpace(hashCatalogFilePath))
            {
                return Enumerable.Empty<CatalogPackageEntry>();
            }

            var doc = new XmlDocument();
            using (var stream = File.OpenRead(hashCatalogFilePath))
            {
                doc.Load(stream);
            }
            var packages = new List<CatalogPackageEntry>();
            var catalog = doc.FirstChild;
            foreach (XmlElement p in catalog.ChildNodes)
            {
                var package = new CatalogPackageEntry
                {
                    Id = p.Attributes["Id"].Value,
                    Version = p.Attributes["Version"].Value,
                    OriginalHash = p.Attributes["OriginalHash"].Value.ToBytes(),
                    PoisonedHash = p.Attributes["PoisonedHash"]?.Value?.ToBytes(),
                    Path = p.Attributes["Path"].Value,
                };
                packages.Add(package);
                foreach (XmlNode f in p.ChildNodes)
                {
                    var fEntry = new CatalogFileEntry
                    {
                        OriginalHash = f.Attributes["OriginalHash"].Value.ToBytes(),
                        PoisonedHash = f.Attributes["PoisonedHash"]?.Value?.ToBytes(),
                        Path = f.Attributes["Path"].Value,
                    };
                    package.Files.Add(fEntry);
                }
            }
            return packages;
        }
    }
}
