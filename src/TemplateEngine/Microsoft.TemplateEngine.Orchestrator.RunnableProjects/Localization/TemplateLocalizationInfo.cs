// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    internal class TemplateLocalizationInfo : ILocalizationLocator
    {
        private readonly List<IValidationEntry> _validationEntries = new();

        public TemplateLocalizationInfo(CultureInfo locale, LocalizationModel model, IFile file)
        {
            Locale = locale;
            Model = model;
            File = file;
        }

        string ILocalizationLocator.Locale => Locale.Name;

        string ILocalizationLocator.ConfigPlace => File.FullPath;

        string ILocalizationLocator.Identity => throw new NotImplementedException();

        string? ILocalizationLocator.Author => Model.Author;

        string? ILocalizationLocator.Name => Model.Name;

        string? ILocalizationLocator.Description => Model.Description;

        IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ILocalizationLocator.ParameterSymbols => Model.ParameterSymbols;

        public bool IsValid => !ValidationErrors.Any(e => e.Severity == IValidationEntry.SeverityLevel.Error);

        public IReadOnlyList<IValidationEntry> ValidationErrors => _validationEntries;

        internal CultureInfo Locale { get; }

        internal LocalizationModel Model { get; }

        internal IFile File { get; }

        internal void AddValidationEntry(IValidationEntry entry)
        {
            _validationEntries.Add(entry);
        }

    }
}
