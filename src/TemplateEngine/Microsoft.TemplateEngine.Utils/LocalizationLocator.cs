using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class LocalizationLocator : ILocalizationLocator
    {
        public string Locale { get; set; }

        public string MountPointUri { get; set; }

        public string ConfigPlace { get; set; }

        public string Identity { get; set; }

        public string Author { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; set; }
    }
}
