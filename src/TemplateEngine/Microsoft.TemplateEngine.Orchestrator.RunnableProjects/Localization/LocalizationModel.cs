using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    public class LocalizationModel
    {
        public string Author { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        // symbol name -> localized description
        public IReadOnlyDictionary<string, string> SymbolDescriptions { get; set; }

        public IReadOnlyDictionary<Guid, PostActionLocalizationModel> PostActions { get; set; }

        public IReadOnlyList<FileLocalizationModel> FileLocalizations { get; set; }
    }
}
