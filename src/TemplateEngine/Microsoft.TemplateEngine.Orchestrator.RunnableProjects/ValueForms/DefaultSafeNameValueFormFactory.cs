// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class DefaultSafeNameValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "safe_name";

        internal DefaultSafeNameValueFormFactory()
            : base(FormIdentifier) { }

        internal static string ToSafeName(string value)
        {
            const string replacement = "_";
            string workingValue = Regex.Replace(value, @"(^\s+|\s+$)", string.Empty);
            workingValue = Regex.Replace(workingValue, @"(((?<=\.)|^)(?=\d)|\W)", replacement);
            return workingValue;
        }

        protected override string Process(string value) => ToSafeName(value);
    }
}
