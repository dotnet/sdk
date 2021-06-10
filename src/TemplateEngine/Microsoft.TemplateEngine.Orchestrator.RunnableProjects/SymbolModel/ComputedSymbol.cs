// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal class ComputedSymbol : ISymbolModel
    {
        internal const string TypeName = "computed";

        internal ComputedSymbol(JObject jObject)
        {
            DataType = jObject.ToString(nameof(DataType));
            Value = jObject.ToString(nameof(Value));
            Type = jObject.ToString(nameof(Type));
            Evaluator = jObject.ToString(nameof(Evaluator));
        }

        public string Value { get; init; }

        public string Type { get; init; }

        public IReadOnlyList<IReplacementContext> ReplacementContexts => Array.Empty<IReplacementContext>();

        string ISymbolModel.Binding => null;

        string ISymbolModel.Replaces => null;

        string ISymbolModel.FileRename => null;

        internal string DataType { get; init; }

        internal string Evaluator { get; init; }
    }
}
