using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public class NupkgInstallUnitDescriptorFactory : IInstallUnitDescriptorFactory
    {
        internal static readonly Guid FactoryId = new Guid("AC33C6A1-52CA-4215-B72D-2DCE7F6A1D2F");

        public Guid Id => FactoryId;

        public bool TryParse(JObject jobject, out IInstallUnitDescriptor descriptor)
        {
            if (!jobject.TryGetValue("FactoryId", StringComparison.OrdinalIgnoreCase, out JToken factoryIdToken)
                    || (factoryIdToken == null)
                    || (factoryIdToken.Type != JTokenType.String)
                    || !Guid.TryParse(factoryIdToken.ToString(), out Guid factoryId)
                    || factoryId != FactoryId)
            {
                descriptor = null;
                return false;
            }

            if (!jobject.TryGetValue("MountPointId", StringComparison.OrdinalIgnoreCase, out JToken mountPointIdToken)
                    || (mountPointIdToken == null)
                    || (mountPointIdToken.Type != JTokenType.String)
                    || !Guid.TryParse(mountPointIdToken.ToString(), out Guid mountPointId))
            {
                descriptor = null;
                return false;
            }

            if (!jobject.TryGetValue("PackageName", StringComparison.OrdinalIgnoreCase, out JToken packageNameToken) || (packageNameToken == null) || (packageNameToken.Type != JTokenType.String))
            {
                descriptor = null;
                return false;
            }

            if (!jobject.TryGetValue("Version", StringComparison.OrdinalIgnoreCase, out JToken versionToken) || (versionToken == null) || (versionToken.Type != JTokenType.String))
            {
                descriptor = null;
                return false;
            }

            string packageName = packageNameToken.ToString();
            string version = versionToken.ToString();
            descriptor = new NupkgInstallUnitDescriptor(mountPointId, packageName, version);
            return true;
        }

        public bool TryCreateFromMountPoint(IMountPoint mountPoint, out IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            List<IInstallUnitDescriptor> allDescriptors = new List<IInstallUnitDescriptor>();
            descriptorList = allDescriptors;

            if (mountPoint.Info.Place != null && File.Exists(mountPoint.Info.Place) && TryGetPackageInfoFromNuspec(mountPoint, out string packageName, out string version))
            {
                IInstallUnitDescriptor descriptor = new NupkgInstallUnitDescriptor(mountPoint.Info.MountPointId, packageName, version);
                allDescriptors.Add(descriptor);
                return true;
            }

            return false;
        }

        private static bool TryGetPackageInfoFromNuspec(IMountPoint mountPoint, out string packageName, out string version)
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
