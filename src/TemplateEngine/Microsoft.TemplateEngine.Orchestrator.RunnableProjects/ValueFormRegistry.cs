// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal static class ValueFormRegistry
    {
        private static readonly IReadOnlyList<IValueFormFactory> s_allForms =
            new IValueFormFactory[]
            {
                new ReplacementValueFormFactory(),
                new ChainValueFormFactory(),
                new XmlEncodeValueFormFactory(),
                new JsonEncodeValueFormFactory(),
                new IdentityValueFormFactory(),
                new DefaultSafeNameValueFormFactory(),
                new DefaultLowerSafeNameValueFormFactory(),
                new DefaultSafeNamespaceValueFormFactory(),
                new DefaultLowerSafeNamespaceValueFormFactory(),
                new LowerCaseValueFormFactory(),
                new LowerCaseInvariantValueFormFactory(),
                new UpperCaseValueFormFactory(),
                new UpperCaseInvariantValueFormFactory(),
                new FirstLowerCaseValueFormFactory(),
                new FirstUpperCaseValueFormFactory(),
                new FirstUpperCaseInvariantValueFormFactory(),
                new FirstLowerCaseInvariantValueFormFactory(),
                new KebabCaseValueFormFactory(),
                new TitleCaseValueFormFactory(),
            };

        private static readonly IValueFormFactory s_defaultForm = new IdentityValueFormFactory();

        internal static IReadOnlyDictionary<string, IValueFormFactory> FormLookup => s_allForms.ToDictionary(ff => ff.Identifier, ff => ff, StringComparer.OrdinalIgnoreCase);

        internal static IValueForm GetForm(string name, JObject? obj)
        {
            string? identifier = obj.ToString("identifier");

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return s_defaultForm.FromJObject(name, obj);
            }

            if (!FormLookup.TryGetValue(identifier!, out IValueFormFactory? value))
            {
                return s_defaultForm.FromJObject(name, obj);
            }

            return value.FromJObject(name, obj);
        }
    }
}
