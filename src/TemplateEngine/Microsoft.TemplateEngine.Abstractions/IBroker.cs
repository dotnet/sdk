using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IBroker
    {
        IComponentRegistry ComponentRegistry { get; }

        IEnumerable<IConfiguredTemplateSource> GetConfiguredSources();

        bool AddConfiguredSource(string alias, string sourceName, string location);

        bool RemoveConfiguredSource(string alias);
    }
}