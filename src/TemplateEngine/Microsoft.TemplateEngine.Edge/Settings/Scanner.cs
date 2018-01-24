using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class Scanner
    {
        public Scanner(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public ScanResult Scan(string baseDir)
        {
            return Scan(baseDir, false);
        }

        public ScanResult Scan(string baseDir, bool allowDevInstall)
        {
            IReadOnlyList<string> directoriesToScan = DetermineDirectoriesToScan(baseDir);
            IReadOnlyList<MountPointScanSource> sourceList = SetupMountPointScanInfoForDirectories(directoriesToScan);

            ScanSourcesForComponents(sourceList, allowDevInstall);
            ScanResult scanResult = ScanSourcesForTemplatesAndLangPacks(sourceList);

            // cleanup
            foreach (MountPointScanSource source in sourceList)
            {
                if (source.AnythingFound)
                {
                    scanResult.AddInstalledMountPointId(source.MountPoint.Info.MountPointId);
                }

                if (source.MountPoint != null)
                {   // should never be null, but just in case.
                    _environmentSettings.SettingsLoader.ReleaseMountPoint(source.MountPoint);

                    if (source.IsNewCandidateMountPoint)
                    {
                        if (!source.AnythingFound)
                        {
                            _environmentSettings.SettingsLoader.RemoveMountPoints(new[] { source.MountPoint.Info.MountPointId });
                        }

                        if (!source.FoundTemplates && source.IsLocationACopyIntoPackages)
                        {
                            try
                            {
                                _environmentSettings.Host.FileSystem.FileDelete(source.MountPoint.Info.Place);
                            }
                            catch
                            {
                                _environmentSettings.Host.LogDiagnosticMessage($"During scan cleanup, couldn't delete the copied place: {source.MountPoint.Info.Place}", "Install");
                            }
                        }
                    }
                }
            }

            return scanResult;
        }

        private IReadOnlyList<string> DetermineDirectoriesToScan(string baseDir)
        {
            List<string> directoriesToScan = new List<string>();

            if (baseDir[baseDir.Length - 1] == '/' || baseDir[baseDir.Length - 1] == '\\')
            {
                baseDir = baseDir.Substring(0, baseDir.Length - 1);
            }

            string searchTarget = Path.Combine(_environmentSettings.Host.FileSystem.GetCurrentDirectory(), baseDir.Trim());
            List<string> matches = _environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(Path.GetDirectoryName(searchTarget), Path.GetFileName(searchTarget), SearchOption.TopDirectoryOnly)
                                                                                                .Where(x => !x.EndsWith("symbols.nupkg", StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 1)
            {
                directoriesToScan.Add(matches[0]);
            }
            else
            {
                foreach (string match in matches)
                {
                    IReadOnlyList<string> subDirMatches = DetermineDirectoriesToScan(match);
                    directoriesToScan.AddRange(subDirMatches);
                }
            }

            return directoriesToScan;
        }

        private IReadOnlyList<MountPointScanSource> SetupMountPointScanInfoForDirectories(IReadOnlyList<string> directoriesToScan)
        {
            List<MountPointScanSource> locationList = new List<MountPointScanSource>();

            foreach (string directory in directoriesToScan)
            {
                MountPointScanSource locationInfo = GetOrCreateMountPointScanInfoForDirectory(directory);
                if (locationInfo != null)
                {
                    locationList.Add(locationInfo);
                }
            }

            return locationList;
        }

        private MountPointScanSource GetOrCreateMountPointScanInfoForDirectory(string originalLocation)
        {
            if (_environmentSettings.SettingsLoader.TryGetMountPointFromPlace(originalLocation, out IMountPoint existingMountPoint))
            {
                return new MountPointScanSource()
                {
                    Location = originalLocation,
                    IsLocationACopyIntoPackages = false,
                    MountPoint = existingMountPoint,
                    IsNewCandidateMountPoint = false,
                    FoundTemplates = false,
                    FoundComponents = false
                };
            }
            else
            {
                foreach (IMountPointFactory factory in _environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>().ToList())
                {
                    if (factory.TryMount(_environmentSettings, null, originalLocation, out IMountPoint originalLocationMountPoint))
                    {
                        string mountPointLocation = originalLocation;
                        IMountPoint effectiveMountPoint = originalLocationMountPoint;
                        bool isLocationACopyIntoPackages = false;

                        if (originalLocationMountPoint.Info.MountPointFactoryId != FileSystemMountPointFactory.FactoryId)
                        {
                            string targetLocation = Path.Combine(_paths.User.Packages, Path.GetFileName(originalLocation));

                            if (!string.Equals(targetLocation, originalLocation))
                            {
                                isLocationACopyIntoPackages = true;

                                _paths.CreateDirectory(_paths.User.Packages);
                                _paths.Copy(originalLocation, targetLocation);

                                var attributes = _environmentSettings.Host.FileSystem.GetFileAttributes(targetLocation);
                                if (attributes.HasFlag(FileAttributes.ReadOnly))
                                {
                                    attributes &= ~FileAttributes.ReadOnly;
                                    _environmentSettings.Host.FileSystem.SetFileAttributes(targetLocation, attributes);
                                }

                                if (_environmentSettings.SettingsLoader.TryGetMountPointFromPlace(targetLocation, out IMountPoint targetLocationMountPoint)
                                        || factory.TryMount(_environmentSettings, null, targetLocation, out targetLocationMountPoint))
                                {
                                    // the original location will not be used, release its MP without adding it.
                                    _environmentSettings.SettingsLoader.ReleaseMountPoint(originalLocationMountPoint);
                                    mountPointLocation = targetLocation;
                                    effectiveMountPoint = targetLocationMountPoint;
                                }
                            }
                        }

                        // Add the effective MP.
                        // If the original isn't being used, it got released when the target MP was created, so doesn't need a special release here.
                        // The effective is the only one that needs action here.
                        _environmentSettings.SettingsLoader.AddMountPoint(effectiveMountPoint);

                        return new MountPointScanSource()
                        {
                            Location = mountPointLocation,
                            IsLocationACopyIntoPackages = isLocationACopyIntoPackages,
                            IsNewCandidateMountPoint = true,
                            MountPoint = effectiveMountPoint,
                            FoundTemplates = false,
                            FoundComponents = false,
                        };
                    }
                }
            }

            return null;
        }

        // Iterate over the sourceList, scanning for components.
        // Track the sources that didn't find any components.
        // If any source found components, re-try the remaining sources that didn't find any components.
        // Repeat until all sources have found components, or none of the remaining sources find anything.
        // This is to avoid problems with the load order - an assembly that depends on another assembly can't be loaded until the dependent assembly has been loaded.
        private void ScanSourcesForComponents(IReadOnlyList<MountPointScanSource> sourceList, bool allowDevInstall)
        {
            List<MountPointScanSource> workingSet = new List<MountPointScanSource>(sourceList);

            bool anythingFoundThisRound;
            do
            {
                anythingFoundThisRound = false;
                List<MountPointScanSource> unhandledSources = new List<MountPointScanSource>();

                foreach (MountPointScanSource source in workingSet)
                {
                    IMountPoint mountPoint = source.MountPoint;

                    // note: if we can't get the mount point, don't put this source in the unhandled sources - it'll never succeed.
                    bool hasComponents = ScanForComponents(mountPoint, source.Location, allowDevInstall);
                    source.FoundComponents |= hasComponents;
                    anythingFoundThisRound |= hasComponents;

                    if (!hasComponents)
                    {
                        unhandledSources.Add(source);
                    }
                }

                workingSet = unhandledSources;
            } while (anythingFoundThisRound && workingSet.Count > 0);
        }

        private bool ScanForComponents(IMountPoint mountPoint, string scanLocation, bool allowDevInstall)
        {
            bool anythingFound = false;
            bool isCopiedIntoContentDirectory;

            if (!mountPoint.Root.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any())
            {
                return false;
            }

            string diskPath;
            if (mountPoint.Info.MountPointFactoryId != FileSystemMountPointFactory.FactoryId)
            {
                if (!TryCopyIntoContentForNonFileSystemBasedMountPoints(mountPoint, scanLocation, out diskPath))
                {
                    return false;
                }

                isCopiedIntoContentDirectory = true;
            }
            else
            {
                if (!allowDevInstall)
                {
                    // TODO: localize this message
                    _environmentSettings.Host.LogDiagnosticMessage("Installing local .dll files is not a standard supported scenario. Either package it in a .zip or .nupkg, or install it using '--dev:install'.", "Install");
                    // dont allow .dlls to be installed from a file unless the debugging flag is set.
                    return false;
                }

                diskPath = scanLocation;
                isCopiedIntoContentDirectory = false;
            }

            foreach (KeyValuePair<string, Assembly> asm in AssemblyLoader.LoadAllFromPath(_paths, out IEnumerable<string> failures, diskPath))
            {
                try
                {
                    IReadOnlyList<Type> typeList = asm.Value.GetTypes();

                    if (typeList.Count > 0)
                    {
                        // TODO: figure out what to do with probing path registration when components are not found.
                        // They need to be registered for dependent assemblies, not just when an assembly can be loaded.
                        // We'll need to figure out how to know when that is.
                        _environmentSettings.SettingsLoader.Components.RegisterMany(typeList);
                        _environmentSettings.SettingsLoader.AddProbingPath(Path.GetDirectoryName(asm.Key));
                        anythingFound = true;
                    }
                }
                catch
                {
                    // exceptions here are ok, due to dependency errors, etc.
                }
            }

            if (!anythingFound && isCopiedIntoContentDirectory)
            {
                try
                {
                    // The source was copied to content and then scanned for components.
                    // Nothing was found, and this is a copy that now has no use, so delete it.
                    // Note: no mount point was created for this copy, so no need to release it.
                    _environmentSettings.Host.FileSystem.DirectoryDelete(diskPath, true);
                }
                catch (Exception ex)
                {
                    _environmentSettings.Host.LogDiagnosticMessage($"During ScanForComponents() cleanup, couldn't delete source copied into the content dir: {diskPath}", "Install");
                    _environmentSettings.Host.LogDiagnosticMessage($"\tError: {ex.Message}", "Install");
                }
            }

            return anythingFound;
        }

        private bool TryCopyIntoContentForNonFileSystemBasedMountPoints(IMountPoint mountPoint, string scanLocation, out string diskPath)
        {
            string path = Path.Combine(_paths.User.Content, Path.GetFileName(scanLocation));

            try
            {
                mountPoint.Root.CopyTo(path);
            }
            catch (IOException)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"Error copying scanLocation: {scanLocation} into the content dir: {path}", "Install");
                diskPath = null;
                return false;
            }

            diskPath = path;
            return true;
        }

        private ScanResult ScanSourcesForTemplatesAndLangPacks(IReadOnlyList<MountPointScanSource> sourceList)
        {
            ScanResult foundTemplatesAndLangPacks = new ScanResult();

            // only scan for templates & langpacks if no components were found.
            foreach (MountPointScanSource source in sourceList.Where(x => !x.AnythingFound))
            {
                IMountPoint mountPoint = source.MountPoint;
                bool hasTemplates = ScanMountPointForTemplatesAndLangpacks(mountPoint, source.Location, foundTemplatesAndLangPacks);
                source.FoundTemplates |= hasTemplates;
            }

            return foundTemplatesAndLangPacks;
        }

        private bool ScanMountPointForTemplatesAndLangpacks(IMountPoint mountPoint, string templateDir, ScanResult foundTemplatesAndLangPacks)
        {
            bool anythingFound = false;

            foreach (IGenerator generator in _environmentSettings.SettingsLoader.Components.OfType<IGenerator>())
            {
                IList<ITemplate> templateList = generator.GetTemplatesAndLangpacksFromDir(mountPoint, out IList<ILocalizationLocator> localizationInfo);

                foreach (ILocalizationLocator locator in localizationInfo)
                {
                    foundTemplatesAndLangPacks.AddLocalization(locator);
                }

                foreach (ITemplate template in templateList)
                {
                    foundTemplatesAndLangPacks.AddTemplate(template);
                }

                anythingFound |= templateList.Count > 0 || localizationInfo.Count > 0;
            }

            return anythingFound;
        }

        private class MountPointScanSource
        {
            public string Location { get; set; }
            public bool IsLocationACopyIntoPackages { get; set; }
            public IMountPoint MountPoint { get; set; }
            // True if the mount point didn't exist prior to the scan, false otherwise.
            public bool IsNewCandidateMountPoint { get; set; }
            public bool FoundComponents { get; set; }
            public bool FoundTemplates { get; set; }
            public bool AnythingFound
            {
                get
                {
                    return FoundTemplates || FoundComponents;
                }
            }
        }
    }
}
