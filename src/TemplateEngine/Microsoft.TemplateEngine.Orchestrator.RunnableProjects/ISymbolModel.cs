// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal interface ISymbolModel
    {
        string Type { get; }

        string Binding { get; set; }

        string Replaces { get; set; }

        IReadOnlyList<IReplacementContext> ReplacementContexts { get; }

        string FileRename { get; set; }
    }
}
