// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class TitleCaseValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "titleCase";

        internal TitleCaseValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return value switch
            {
                "" => value,
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
            };
        }
    }
}
