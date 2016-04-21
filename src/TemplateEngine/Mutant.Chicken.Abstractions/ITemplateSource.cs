using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface ITemplateSource : IComponent
    {
        IEnumerable<ITemplateSourceEntry> EntriesIn(string location);

        bool CanHandle(string location);
    }
}
