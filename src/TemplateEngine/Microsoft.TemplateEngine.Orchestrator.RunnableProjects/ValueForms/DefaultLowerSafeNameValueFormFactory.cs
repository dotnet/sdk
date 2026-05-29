// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultLowerSafeNameValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "lower_safe_name";

        internal DefaultLowerSafeNameValueFormFactory()
            : base(FormIdentifier) { }

        protected override string Process(string value) => DefaultSafeNameValueFormFactory.ToSafeName(value).ToLowerInvariant();
    }
}
