// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public ScanResult Scan(string sourceLocation)
        {
            if (string.IsNullOrWhiteSpace(sourceLocation))
            {
                throw new ArgumentException($"{nameof(sourceLocation)} should not be null or empty");
            }
            MountPointScanSource source = GetOrCreateMountPointScanInfoForInstallSource(sourceLocation);

            ScanResult scanResult = new ScanResult();
            ScanForComponents(source);
            ScanMountPointForTemplatesAndLangpacks(source, scanResult);

            source.MountPoint.Dispose();

            return scanResult;
        }

        private MountPointScanSource GetOrCreateMountPointScanInfoForInstallSource(string sourceLocation)
        {
            foreach (IMountPointFactory factory in _environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>().ToList())
            {
                if (factory.TryMount(_environmentSettings, null, sourceLocation, out IMountPoint mountPoint))
                {
                    // file-based and not originating in the scratch dir.
                    bool isLocalFlatFileSource = mountPoint is FileSystemMountPoint
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
            throw new Exception($"source location {sourceLocation} is not supported, or doesn't exist.");
        }

        private void ScanForComponents(MountPointScanSource source)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));

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

        private void ScanMountPointForTemplatesAndLangpacks(MountPointScanSource source, ScanResult scanResult)
        {
            _ = source ?? throw new ArgumentNullException(nameof(source));
            // look for things to install
            foreach (IGenerator generator in _environmentSettings.SettingsLoader.Components.OfType<IGenerator>())
            {
                IList<ITemplate> templateList = generator.GetTemplatesAndLangpacksFromDir(source.MountPoint, out IList<ILocalizationLocator> localizationInfo);

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
