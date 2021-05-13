// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CreationEffects : ICreationEffects
    {
        internal CreationEffects(IReadOnlyList<IFileChange> fileChanges, ICreationResult creationResult)
        {
            FileChanges = fileChanges ?? throw new System.ArgumentNullException(nameof(fileChanges));
            CreationResult = creationResult ?? throw new System.ArgumentNullException(nameof(creationResult));
        }

        public IReadOnlyList<IFileChange> FileChanges { get; }

        public ICreationResult CreationResult { get; }
    }

    internal class CreationEffects2 : ICreationEffects, ICreationEffects2
    {
        internal CreationEffects2(IReadOnlyList<IFileChange2> fileChanges, ICreationResult creationResult)
        {
            FileChanges = fileChanges ?? throw new System.ArgumentNullException(nameof(fileChanges));
            CreationResult = creationResult ?? throw new System.ArgumentNullException(nameof(creationResult));
        }

        public IReadOnlyList<IFileChange2> FileChanges { get; }

        IReadOnlyList<IFileChange> ICreationEffects.FileChanges => FileChanges;

        public ICreationResult CreationResult { get; }
    }
}
