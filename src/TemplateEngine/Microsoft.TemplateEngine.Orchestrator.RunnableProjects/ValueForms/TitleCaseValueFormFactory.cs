// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Globalization;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class TitleCaseValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "titleCase";

        internal TitleCaseValueFormFactory() : base(FormIdentifier) { }

        protected override string? Process(string? value)
        {
            switch (value)
            {
                case null: return null;
                case "": return value;
                default: return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
            }
        }
    }
}
