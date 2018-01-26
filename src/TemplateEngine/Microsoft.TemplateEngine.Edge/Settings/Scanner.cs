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
            IReadOnlyList<MountPointScanSource> sourceList = SetupMountPointScanInfoForInstallSourceList(directoriesToScan);

            ScanResult scanResult = new ScanResult();
            ScanSourcesForComponents(sourceList, allowDevInstall);
            ScanSourcesForTemplatesAndLangPacks(sourceList, scanResult);

            // Cleanup.
            // The source mount points are done being used, release them. The caller will delete the source files if needed.
            foreach (MountPointScanSource source in sourceList)
            {
                _environmentSettings.SettingsLoader.ReleaseMountPoint(source.MountPoint);
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

        private IReadOnlyList<MountPointScanSource> SetupMountPointScanInfoForInstallSourceList(IReadOnlyList<string> directoriesToScan)
        {
            List<MountPointScanSource> locationList = new List<MountPointScanSource>();

            foreach (string directory in directoriesToScan)
            {
                MountPointScanSource locationInfo = GetOrCreateMountPointScanInfoForInstallSource(directory);
                if (locationInfo != null)
                {
                    locationList.Add(locationInfo);
                }
            }

            return locationList;
        }

        private MountPointScanSource GetOrCreateMountPointScanInfoForInstallSource(string sourceLocation)
        {
            if (_environmentSettings.SettingsLoader.TryGetMountPointFromPlace(sourceLocation, out IMountPoint existingMountPoint))
            {
                return new MountPointScanSource()
                {
                    Location = sourceLocation,
                    MountPoint = existingMountPoint,
                    FoundTemplates = false,
                    FoundComponents = false,
                    ShouldStayInOriginalLocation = true,
                };
            }
            else
            {
                foreach (IMountPointFactory factory in _environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>().ToList())
                {
                    if (factory.TryMount(_environmentSettings, null, sourceLocation, out IMountPoint mountPoint))
                    {
                        // file-based and not originating in the scratch dir.
                        bool isLocalFlatFileSource = mountPoint.Info.MountPointFactoryId == FileSystemMountPointFactory.FactoryId
                                                    && !sourceLocation.StartsWith(_paths.User.ScratchDir);

                        return new MountPointScanSource()
                        {
                            Location = sourceLocation,
                            MountPoint = mountPoint,
                            ShouldStayInOriginalLocation = isLocalFlatFileSource,
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
                    ScanForComponents(source, allowDevInstall);
                    anythingFoundThisRound |= source.FoundComponents;

                    if (!source.FoundComponents)
                    {
                        unhandledSources.Add(source);
                    }
                }

                workingSet = unhandledSources;
            } while (anythingFoundThisRound && workingSet.Count > 0);
        }

        private void ScanForComponents(MountPointScanSource source, bool allowDevInstall)
        {
            bool isCopiedIntoContentDirectory;

            if (!source.MountPoint.Root.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any())
            {
                return;
            }

            string actualScanPath;
            if (!source.ShouldStayInOriginalLocation)
            {
                if (!TryCopyForNonFileSystemBasedMountPoints(source.MountPoint, source.Location, _paths.User.Content, true, out actualScanPath))
                {
                    return;
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
                    return;
                }

                actualScanPath = source.Location;
                isCopiedIntoContentDirectory = false;
            }

            foreach (KeyValuePair<string, Assembly> asm in AssemblyLoader.LoadAllFromPath(_paths, out IEnumerable<string> failures, actualScanPath))
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
                        source.FoundComponents = true;
                    }
                }
                catch
                {
                    // exceptions here are ok, due to dependency errors, etc.
                }
            }

            if (!source.FoundComponents && isCopiedIntoContentDirectory)
            {
                try
                {
                    // The source was copied to content and then scanned for components.
                    // Nothing was found, and this is a copy that now has no use, so delete it.
                    // Note: no mount point was created for this copy, so no need to release it.
                    _environmentSettings.Host.FileSystem.DirectoryDelete(actualScanPath, true);
                }
                catch (Exception ex)
                {
                    _environmentSettings.Host.LogDiagnosticMessage($"During ScanForComponents() cleanup, couldn't delete source copied into the content dir: {actualScanPath}", "Install");
                    _environmentSettings.Host.LogDiagnosticMessage($"\tError: {ex.Message}", "Install");
                }
            }
        }

        private bool TryCopyForNonFileSystemBasedMountPoints(IMountPoint mountPoint, string sourceLocation, string targetBasePath, bool expandIfArchive, out string diskPath)
        {
            string targetPath = Path.Combine(targetBasePath, Path.GetFileName(sourceLocation));

            try
            {
                if (expandIfArchive)
                {
                    mountPoint.Root.CopyTo(targetPath);
                }
                else
                {
                    _paths.CreateDirectory(targetBasePath); // creates Packages/ or Content/ if needed
                    _paths.Copy(sourceLocation, targetPath);
                }
            }
            catch (IOException)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"Error copying scanLocation: {sourceLocation} into the target dir: {targetPath}", "Install");
                diskPath = null;
                return false;
            }

            diskPath = targetPath;
            return true;
        }

        private void ScanSourcesForTemplatesAndLangPacks(IReadOnlyList<MountPointScanSource> sourceList, ScanResult scanResult)
        {
            // only scan for templates & langpacks if no components were found.
            foreach (MountPointScanSource source in sourceList.Where(x => !x.AnythingFound))
            {
                ScanMountPointForTemplatesAndLangpacks(source, scanResult);
            }
        }

        private void ScanMountPointForTemplatesAndLangpacks(MountPointScanSource source, ScanResult scanResult)
        {
            bool isCopiedIntoPackagesDirectory;
            string actualScanPath;
            IMountPoint scanMountPoint = null;

            if (!source.ShouldStayInOriginalLocation)
            {
                if (!TryCopyForNonFileSystemBasedMountPoints(source.MountPoint, source.Location, _paths.User.Packages, false, out actualScanPath))
                {
                    return;
                }

                foreach (IMountPointFactory factory in _environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>())
                {
                    if (factory.TryMount(_environmentSettings, null, actualScanPath, out scanMountPoint))
                    {
                        break;
                    }
                }

                if (scanMountPoint == null)
                {
                    _environmentSettings.Host.FileSystem.FileDelete(actualScanPath);
                    return;
                }

                isCopiedIntoPackagesDirectory = true;
            }
            else
            {
                actualScanPath = source.Location;
                scanMountPoint = source.MountPoint;
                isCopiedIntoPackagesDirectory = false;
            }

            // look for things to install
            foreach (IGenerator generator in _environmentSettings.SettingsLoader.Components.OfType<IGenerator>())
            {
                IList<ITemplate> templateList = generator.GetTemplatesAndLangpacksFromDir(scanMountPoint, out IList<ILocalizationLocator> localizationInfo);

                foreach (ILocalizationLocator locator in localizationInfo)
                {
                    scanResult.AddLocalization(locator);
                }

                foreach (ITemplate template in templateList)
                {
                    scanResult.AddTemplate(template);
                }

                source.FoundTemplates |= templateList.Count > 0 || localizationInfo.Count > 0;
            }

            // finalize the result
            if (source.FoundTemplates)
            {
                // add the MP
                _environmentSettings.SettingsLoader.AddMountPoint(scanMountPoint);
                // add the MP to the scan result
                scanResult.AddInstalledMountPointId(scanMountPoint.Info.MountPointId);
            }
            else
            {
                // delete the copy
                if (!source.FoundTemplates && isCopiedIntoPackagesDirectory)
                {
                    try
                    {
                        // The source was copied to packages and then scanned for templates.
                        // Nothing was found, and this is a copy that now has no use, so delete it.
                        _environmentSettings.SettingsLoader.ReleaseMountPoint(scanMountPoint);

                        // It's always copied as an archive, so it's a file delete, not a directory delete
                        _environmentSettings.Host.FileSystem.FileDelete(actualScanPath);
                    }
                    catch (Exception ex)
                    {
                        _environmentSettings.Host.LogDiagnosticMessage($"During ScanMountPointForTemplatesAndLangpacks() cleanup, couldn't delete source copied into the packages dir: {actualScanPath}", "Install");
                        _environmentSettings.Host.LogDiagnosticMessage($"\tError: {ex.Message}", "Install");
                    }
                }
            }
        }

        private class MountPointScanSource
        {
            public string Location { get; set; }
            public IMountPoint MountPoint { get; set; }
            public bool ShouldStayInOriginalLocation { get; set; }
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
