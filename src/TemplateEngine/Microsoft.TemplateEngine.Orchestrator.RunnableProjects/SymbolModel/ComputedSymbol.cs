// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class ComputedSymbol : ISymbolModel
    {
        internal const string TypeName = "computed";

        internal string DataType { get; private set; }

        public string Value { get; internal set; }

        public string Type { get; private set; }

        internal string Evaluator { get; private set; }

        public IReadOnlyList<IReplacementContext> ReplacementContexts => Empty<IReplacementContext>.List.Value;

        string ISymbolModel.Binding
        {
            get { return null; }
            set { }
        }

        string ISymbolModel.Replaces
        {
            get { return null; }
            set { }
        }

        string ISymbolModel.FileRename
        {
            get { return null; }
            set { }
        }

        internal static ISymbolModel FromJObject(JObject jObject)
        {
            ComputedSymbol sym = new ComputedSymbol
            {
                DataType = jObject.ToString(nameof(DataType)),
                Value = jObject.ToString(nameof(Value)),
                Type = jObject.ToString(nameof(Type)),
                Evaluator = jObject.ToString(nameof(Evaluator))
            };

            return sym;
        }
    }
}
