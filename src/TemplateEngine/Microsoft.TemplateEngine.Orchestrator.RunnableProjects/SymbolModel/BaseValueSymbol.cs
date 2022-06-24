// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal abstract class BaseValueSymbol : BaseReplaceSymbol
    {
        /// <summary>
        /// Initializes this instance with given JSON data.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="jObject"></param>
        /// <param name="defaultOverride"></param>
        protected BaseValueSymbol(string name, JObject jObject, string? defaultOverride) : base (jObject, name)
        {
            DefaultValue = defaultOverride ?? jObject.ToString(nameof(DefaultValue));
            IsRequired = jObject.ToBool(nameof(IsRequired));
            DataType = jObject.ToString(nameof(DataType));
            if (!jObject.TryGetValue(nameof(Forms), StringComparison.OrdinalIgnoreCase, out JToken? formsToken) || !(formsToken is JObject formsObject))
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

        protected BaseValueSymbol(BaseValueSymbol clone, SymbolValueFormsModel formsFallback) : base(clone)
        {
            DefaultValue = clone.DefaultValue;
            Forms = clone.Forms.GlobalForms.Count != 0 ? clone.Forms : formsFallback;
            IsRequired = clone.IsRequired;
            DataType = clone.DataType;
        }

        protected BaseValueSymbol(string name, string? replaces) : base(name, replaces)
        {
            Forms = SymbolValueFormsModel.Default;
        }

        internal string? DefaultValue { get; init; }

        internal SymbolValueFormsModel Forms { get; init; }

        internal bool IsRequired { get; init; }

        internal string? DataType { get; init; }

    }
}
