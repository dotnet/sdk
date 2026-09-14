// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class LowerCaseValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "lowerCase";

        internal LowerCaseValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return value.ToLower();
        }
    }
}
