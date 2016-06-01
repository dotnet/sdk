using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplate
    {
        string Author { get; }

        IReadOnlyList<string> Classifications { get; }

        string DefaultName { get; }

        string Identity { get; }

        IGenerator Generator { get; }

        string GroupIdentity { get; }

        string Name { get; }

        string ShortName { get; }

        IConfiguredTemplateSource Source { get; }

        IReadOnlyDictionary<string, string> Tags { get; }

        bool TryGetProperty(string name, out string value);
    }
}