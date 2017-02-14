using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ILocalizationLocator
    {
        string Locale { get; }

        Guid MountPointId { get; }

        string ConfigPlace { get; }

        string Identity { get; }

        string Author { get; }

        string Name { get; }

        string Description { get; }

        IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }
    }
}
