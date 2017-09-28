using System;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    internal class NupkgInstallUnitDescriptor : IInstallUnitDescriptor
    {
        public NupkgInstallUnitDescriptor(Guid mountPointId, string packageName, string version)
        {
            MountPointId = mountPointId;
            PackageName = packageName;
            Version = version;
        }

        [JsonIgnore]
        public string Identifier => PackageName;

        [JsonProperty]
        public Guid FactoryId => NupkgInstallUnitDescriptorFactory.FactoryId;

        [JsonProperty]
        public Guid MountPointId { get; }

        [JsonProperty]
        public string PackageName { get; }

        [JsonProperty]
        public string Version { get; }

        [JsonIgnore]
        public string UserReadableIdentifier => string.Join(".", PackageName, Version);
    }
}
