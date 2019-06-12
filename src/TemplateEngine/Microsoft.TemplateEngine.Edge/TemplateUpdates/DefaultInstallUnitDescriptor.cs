using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public sealed class DefaultInstallUnitDescriptor : IInstallUnitDescriptor
    {
        public DefaultInstallUnitDescriptor(Guid descriptorId, Guid mountPointId, string identifier)
        {
            DescriptorId = descriptorId;
            MountPointId = mountPointId;
            Identifier = identifier;
            Details = _details;
            DetailKeysDisplayOrder = Empty<string>.List.Value;
        }

        private static readonly IReadOnlyDictionary<string, string> _details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Guid DescriptorId { get; }

        public string Identifier { get; }

        public Guid FactoryId => DefaultInstallUnitDescriptorFactory.FactoryId;

        public Guid MountPointId { get; }

        public IReadOnlyDictionary<string, string> Details { get; }

        public string UninstallString => Identifier;

        public IReadOnlyList<string> DetailKeysDisplayOrder { get; }
    }
}
