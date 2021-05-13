// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CreationResult : ICreationResult
    {
        public CreationResult(IReadOnlyList<IPostAction> postActions, IReadOnlyList<ICreationPath> primaryOutputs)
        {
            PostActions = postActions ?? throw new System.ArgumentNullException(nameof(postActions));
            PrimaryOutputs = primaryOutputs ?? throw new System.ArgumentNullException(nameof(primaryOutputs));
        }

        public IReadOnlyList<IPostAction> PostActions { get; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; }
    }
}
