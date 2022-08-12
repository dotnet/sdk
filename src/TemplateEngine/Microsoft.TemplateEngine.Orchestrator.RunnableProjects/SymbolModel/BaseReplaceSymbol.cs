// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal abstract class BaseReplaceSymbol : BaseSymbol
    {
        public BaseReplaceSymbol(string name, string? replaces) : base(name)
        {
            ReplacementContexts = Array.Empty<ReplacementContext>();
            Replaces = replaces;
        }

        protected BaseReplaceSymbol(BaseReplaceSymbol clone) : base(clone)
        {
            FileRename = clone.FileRename;
            Replaces = clone.Replaces;
            ReplacementContexts = clone.ReplacementContexts;
        }

        protected BaseReplaceSymbol(JObject jObject, string name) : base(name)
        {
            FileRename = jObject.ToString(nameof(FileRename));
            Replaces = jObject.ToString(nameof(Replaces));
            ReplacementContexts = SymbolModelConverter.ReadReplacementContexts(jObject);
        }

        /// <summary>
        /// Gets the text that should be replaced by the value of this symbol.
        /// </summary>
        internal string? Replaces { get; }

        /// <summary>
        /// Gets the replacement contexts that determine when this symbol is allowed to do replacement operations.
        /// </summary>
        internal IReadOnlyList<ReplacementContext> ReplacementContexts { get; }

        /// <summary>
        /// Gets the part of the file name that should be replaced with the value of this symbol.
        /// </summary>
        internal string? FileRename { get; }
    }
}
