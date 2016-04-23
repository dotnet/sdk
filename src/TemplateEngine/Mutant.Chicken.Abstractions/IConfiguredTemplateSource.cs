using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface IConfiguredTemplateSource
    {
        ITemplateSource Source { get; }

        IDisposable<ITemplateSourceFolder> Root { get; }

        string Alias { get; }

        string Location { get; }
    }
}