// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text;
using System.Xml;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class XmlEncodeValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "xmlEncode";
        private static readonly XmlWriterSettings Settings = new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Fragment };

        internal XmlEncodeValueFormFactory() : base(FormIdentifier) { }

        protected override string? Process(string? value)
        {
            StringBuilder output = new StringBuilder();
            using (XmlWriter w = XmlWriter.Create(output, Settings))
            {
                w.WriteString(value);
            }
            return output.ToString();
        }
    }
}
