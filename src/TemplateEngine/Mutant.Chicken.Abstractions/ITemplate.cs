using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface ITemplate
    {
        string Name { get; }

        IGenerator Generator { get; }

        IConfiguredTemplateSource Source { get; }

        bool TryGetProperty(string name, out string value);
    }
}