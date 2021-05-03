// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class LocalizationLocator : ILocalizationLocator
    {
        public LocalizationLocator(
            string locale,
            string configPlace,
            string identity,
            string author,
            string name,
            string description,
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> parameterSymbols)
        {
            Locale = locale;
            ConfigPlace = configPlace;
            Identity = identity;
            Author = author;
            Name = name;
            Description = description;
            ParameterSymbols = parameterSymbols;
        }

        public string Locale { get; }

        public string ConfigPlace { get; }

        public string Identity { get; }

        public string Author { get; }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }
    }
}
