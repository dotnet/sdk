// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class ChainValueFormFactory : DependantValueFormFactory<IReadOnlyList<string>>
    {
        internal const string FormIdentifier = "chain";

        internal ChainValueFormFactory()
            : base(FormIdentifier) { }

        protected override string Process(string value, IReadOnlyList<string>? steps, IReadOnlyDictionary<string, IValueForm> otherForms)
        {
            if (steps == null)
            {
                return value;
            }

            string result = value;
            foreach (string step in steps)
            {
                result = otherForms[step].Process(result, otherForms);
            }
            return result;
        }

        protected override IReadOnlyList<string> ReadConfiguration(JObject jObject)
        {
            return jObject.ArrayAsStrings("steps");
        }
    }
}
