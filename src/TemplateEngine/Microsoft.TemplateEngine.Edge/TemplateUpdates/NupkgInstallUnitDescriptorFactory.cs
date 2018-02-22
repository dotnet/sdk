using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public class NupkgInstallUnitDescriptorFactory : IInstallUnitDescriptorFactory
    {
        public static readonly Guid FactoryId = new Guid("AC33C6A1-52CA-4215-B72D-2DCE7F6A1D2F");

        public Guid Id => FactoryId;

        public bool TryCreateFromDetails(IReadOnlyDictionary<string, string> details, out IInstallUnitDescriptor descriptor)
        {
            if (!details.TryGetValue(nameof(NupkgInstallUnitDescriptor.MountPointId), out string mountPointValue)
                || string.IsNullOrEmpty(mountPointValue)
                || !Guid.TryParse(mountPointValue, out Guid mountPointId))
            {
                descriptor = null;
                return false;
            }

            if (!details.TryGetValue(nameof(NupkgInstallUnitDescriptor.PackageName), out string packageName)
                || string.IsNullOrEmpty(packageName))
            {
                descriptor = null;
                return false;
            }

            if (!details.TryGetValue(nameof(NupkgInstallUnitDescriptor.Version), out string version)
                || string.IsNullOrEmpty(version))
            {
                descriptor = null;
                return false;
            }

            descriptor = new NupkgInstallUnitDescriptor(mountPointId, packageName, version);
            return true;
        }

        public bool TryCreateFromMountPoint(IMountPoint mountPoint, out IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            List<IInstallUnitDescriptor> allDescriptors = new List<IInstallUnitDescriptor>();
            descriptorList = allDescriptors;

            if (mountPoint.Info.Place != null
                && mountPoint.EnvironmentSettings.Host.FileSystem.FileExists(mountPoint.Info.Place)
                && TryGetPackageInfoFromNuspec(mountPoint, out string packageName, out string version))
            {
                IInstallUnitDescriptor descriptor = new NupkgInstallUnitDescriptor(mountPoint.Info.MountPointId, packageName, version);
                allDescriptors.Add(descriptor);
                return true;
            }

            return false;
        }

        internal static bool TryGetPackageInfoFromNuspec(IMountPoint mountPoint, out string packageName, out string version)
        {
            IList<IFile> nuspecFiles = mountPoint.Root.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).ToList();

            if (nuspecFiles.Count != 1)
            {
                packageName = null;
                version = null;
                return false;
            }

            using (Stream nuspecStream = nuspecFiles[0].OpenRead())
            {
                XDocument content = XDocument.Load(nuspecStream);
                XElement metadata = content.Root.Elements().FirstOrDefault(x => x.Name.LocalName == "metadata");

                if (metadata == null)
                {
                    packageName = null;
                    version = null;
                    return false;
                }

                packageName = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "id")?.Value;
                version = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "version")?.Value;

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version))
                {
                    packageName = null;
                    version = null;
                    return false;
                }
            }

            return true;
        }
    }
}
