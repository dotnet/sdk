using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public class NupkgInstallUnitDescriptor : IInstallUnitDescriptor
    {
        public NupkgInstallUnitDescriptor(Guid descriptorId, Guid mountPointId, string identifier, string version, string author)
        {
            DescriptorId = descriptorId;
            MountPointId = mountPointId;
            Identifier = identifier;
            Version = version;
            Author = author;
        }

        private static readonly IReadOnlyList<string> _detailKeysDisplayOrder = new List<string>()
        {
            nameof(NuGetPackageId),
            nameof(Version),
            nameof(Author)
        };

        [JsonProperty]
        public Guid DescriptorId { get; }

        [JsonProperty]
        public string Identifier { get; }

        [JsonProperty]
        public Guid FactoryId => NupkgInstallUnitDescriptorFactory.FactoryId;

        [JsonIgnore]
        public string NuGetPackageId => Identifier;

        [JsonProperty]
        public Guid MountPointId { get; }

        [JsonIgnore]
        public string Version { get; }

        [JsonIgnore]
        public string Author { get; }

        [JsonProperty]
        public IReadOnlyDictionary<string, string> Details
        {
            get
            {
                Dictionary<string, string> detailsInfo = new Dictionary<string, string>()
                {
                    { nameof(NuGetPackageId), NuGetPackageId },
                    { nameof(Version), Version },
                    { nameof(Author), Author }
                };

                return detailsInfo;
            }
        }

        [JsonIgnore]
        public string UninstallString => Identifier;

        [JsonIgnore]
        public IReadOnlyList<string> DetailKeysDisplayOrder => _detailKeysDisplayOrder;
    }
}
