using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface IBroker
    {
        IComponentRegistry ComponentRegistry { get; }

        IEnumerable<IConfiguredTemplateSource> GetConfiguredSources();

        void AddConfiguredSource(string alias, string sourceName, string location);

        void RemoveConfiguredSource(string alias);
    }
}