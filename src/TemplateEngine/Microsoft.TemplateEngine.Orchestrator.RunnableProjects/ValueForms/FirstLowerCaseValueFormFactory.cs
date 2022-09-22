// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class FirstLowerCaseValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "firstLowerCase";

        internal FirstLowerCaseValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return value switch
            {
                "" => value,
                _ => value.First().ToString().ToLower() + value.Substring(1),
            };
        }
    }
}
