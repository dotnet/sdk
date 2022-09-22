// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class FirstUpperCaseValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "firstUpperCase";

        internal FirstUpperCaseValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            return value switch
            {
                "" => value,
                _ => value.First().ToString().ToUpper() + value.Substring(1),
            };
        }
    }
}
