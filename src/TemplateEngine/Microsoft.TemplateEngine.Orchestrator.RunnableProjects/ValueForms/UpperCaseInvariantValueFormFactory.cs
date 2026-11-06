// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class UpperCaseInvariantValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "upperCaseInvariant";

        internal UpperCaseInvariantValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return value.ToUpperInvariant();
        }
    }
}
