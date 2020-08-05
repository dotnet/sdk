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

        public bool TryCreateFromDetails(Guid descriptorId, string identifier, Guid mountPointId, bool isPartOfAnOptionalWorkload,
            IReadOnlyDictionary<string, string> details, out IInstallUnitDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(identifier) || mountPointId == Guid.Empty)
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

            if (!details.TryGetValue(nameof(NupkgInstallUnitDescriptor.Author), out string author)
                || string.IsNullOrEmpty(author))
            {
                descriptor = null;
                return false;
            }

            descriptor = new NupkgInstallUnitDescriptor(descriptorId, mountPointId, identifier, isPartOfAnOptionalWorkload, version, author);
            return true;
        }

        public bool TryCreateFromMountPoint(IMountPoint mountPoint, bool isPartOfAnOptionalWorkload, out IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            List<IInstallUnitDescriptor> allDescriptors = new List<IInstallUnitDescriptor>();
            descriptorList = allDescriptors;

            if (mountPoint.Info.Place != null
                && mountPoint.EnvironmentSettings.Host.FileSystem.FileExists(mountPoint.Info.Place)
                && TryGetPackageInfoFromNuspec(mountPoint, out string packageName, out string version, out string author))
            {
                Guid descriptorId = Guid.NewGuid();
                IInstallUnitDescriptor descriptor = new NupkgInstallUnitDescriptor(
                    descriptorId,
                    mountPoint.Info.MountPointId,
                    packageName,
                    isPartOfAnOptionalWorkload,
                    version,
                    author);
                allDescriptors.Add(descriptor);
                return true;
            }

            return false;
        }

        internal static bool TryGetPackageInfoFromNuspec(IMountPoint mountPoint, out string packageName, out string version, out string author)
        {
            IList<IFile> nuspecFiles = mountPoint.Root.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).ToList();

            if (nuspecFiles.Count != 1)
            {
                packageName = null;
                version = null;
                author = null;
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
                    author = null;
                    return false;
                }

                packageName = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "id")?.Value;
                version = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "version")?.Value;
                author = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "authors")?.Value;

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version))
                {
                    packageName = null;
                    version = null;
                    author = null;
                    return false;
                }
            }

            return true;
        }
    }
}
