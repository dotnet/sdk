using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IBroker
    {
        IComponentRegistry ComponentRegistry { get; }

        IEnumerable<IMountPoint> GetConfiguredSources();

        bool AddConfiguredSource(string alias, string sourceName, string location);

        bool RemoveConfiguredSource(string alias);
    }
}