using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    // Base interface for identifying a template or set of templates.
    // Concretes could be for template packs (nupkgs, zips, etc.), or for individual templates.
    // Basically anything which could be a logical unit of templates.
    public interface IInstallUnitDescriptor
    {
        string Identifier { get; }

        Guid FactoryId { get; }

        Guid MountPointId { get; }

        IReadOnlyDictionary<string, string> Details { get; }

        // tentative - may not need anything user facing, but also may be good to have for debugging.
        string UserReadableIdentifier { get; }
    }
}
