// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ILocalizationLocator
    {
        string Locale { get; }

        string ConfigPlace { get; }

        string Identity { get; }

        string Author { get; }

        string Name { get; }

        string Description { get; }

        IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }
    }
}
