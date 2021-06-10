// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal abstract class BaseValueSymbol : ISymbolModel
    {
        protected BaseValueSymbol() { }

        /// <summary>
        /// Initializes this instance with given JSON data.
        /// </summary>
        /// <param name="jObject"></param>
        /// <param name="defaultOverride"></param>
        protected BaseValueSymbol(JObject jObject, string defaultOverride)
        {
            Binding = jObject.ToString(nameof(Binding));
            DefaultValue = defaultOverride ?? jObject.ToString(nameof(DefaultValue));
            FileRename = jObject.ToString(nameof(FileRename));
            IsRequired = jObject.ToBool(nameof(IsRequired));
            Type = jObject.ToString(nameof(Type));
            Replaces = jObject.ToString(nameof(Replaces));
            DataType = jObject.ToString(nameof(DataType));
            ReplacementContexts = SymbolModelConverter.ReadReplacementContexts(jObject);

            if (!jObject.TryGetValue(nameof(Forms), StringComparison.OrdinalIgnoreCase, out JToken formsToken) || !(formsToken is JObject formsObject))
            {
                // no value forms explicitly defined, use the default ("identity")
                Forms = SymbolValueFormsModel.Default;
            }
            else
            {
                // the config defines forms for the symbol. Use them.
                Forms = SymbolValueFormsModel.FromJObject(formsObject);
            }
        }

        public string Binding { get; init; }

        public string Type { get; init; }

        public string Replaces { get; init; }

        public string FileRename { get; init; }

        public IReadOnlyList<IReplacementContext> ReplacementContexts { get; init; }

        internal string DefaultValue { get; init; }

        internal SymbolValueFormsModel Forms { get; init; }

        internal bool IsRequired { get; init; }

        internal string DataType { get; init; }
    }
}
