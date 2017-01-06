using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ILocalizationModel
    {
        string Author { get; }

        string Name { get; }

        string Description { get; }

        // Identifies the langpack association with an actual template. They'll both have the same identity.
        // This is not localized info, more like a key
        string Identity { get; }

        IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }

        IReadOnlyDictionary<Guid, IPostActionLocalizationModel> PostActions { get; }

        IReadOnlyList<IFileLocalizationModel> FileLocalizations { get; }

    }
}
