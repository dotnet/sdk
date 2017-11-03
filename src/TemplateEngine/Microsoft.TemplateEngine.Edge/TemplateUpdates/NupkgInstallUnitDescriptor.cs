using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public class NupkgInstallUnitDescriptor : IInstallUnitDescriptor
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

        [JsonIgnore]
        public Guid MountPointId { get; }

        [JsonIgnore]
        public string PackageName { get; }

        [JsonIgnore]
        public string Version { get; }

        [JsonProperty]
        public IReadOnlyDictionary<string, string> Details
        {
            get
            {
                Dictionary<string, string> detailsInfo = new Dictionary<string, string>()
                {
                    { nameof(MountPointId), MountPointId.ToString() },
                    { nameof(PackageName), PackageName.ToString() },
                    { nameof(Version), Version }
                };

                return detailsInfo;
            }
        }

        [JsonIgnore]
        public string UserReadableIdentifier => string.Join(".", PackageName, Version);
    }
}
