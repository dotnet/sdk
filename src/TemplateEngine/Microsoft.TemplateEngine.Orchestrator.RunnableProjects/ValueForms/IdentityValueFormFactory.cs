// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class IdentityValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "identity";

        internal IdentityValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value) => value;
    }
}
