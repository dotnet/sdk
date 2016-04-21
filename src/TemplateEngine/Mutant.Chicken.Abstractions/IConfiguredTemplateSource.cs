using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface IConfiguredTemplateSource
    {
        ITemplateSource Source { get; }

        IEnumerable<ITemplateSourceEntry> Entries { get; }

        string Alias { get; }

        string Location { get; }
    }
}