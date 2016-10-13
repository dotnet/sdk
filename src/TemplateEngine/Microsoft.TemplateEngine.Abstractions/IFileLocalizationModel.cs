using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IFileLocalizationModel
    {
        string File { get; }

        // original -> localized
        IReadOnlyDictionary<string, string> Localizations { get; }
    }
}
