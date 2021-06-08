// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// The interface represents the effects of template instantiation: the file changes applied and template creation result. Creation effects are evaluated during template dry-run (see <see cref="IGenerator.GetCreationEffectsAsync(IEngineEnvironmentSettings, ITemplate, IParameterSet, string, System.Threading.CancellationToken)"/>.
    /// The interface is outdated, new version is <see cref="ICreationEffects2"/>.
    /// </summary>
    public interface ICreationEffects
    {
        /// <summary>
        /// Gets file changes done on template instantiation.
        /// </summary>
        IReadOnlyList<IFileChange> FileChanges { get; }

        /// <summary>
        /// Gets template creation result: primary outputs and post actions.
        /// </summary>
        ICreationResult CreationResult { get; }
    }

    /// <summary>
    /// The interface represents the effects of template instantiation: the file changes applied and template creation result. Creation effects are evaluated during template dry-run (see <see cref="IGenerator.GetCreationEffectsAsync(IEngineEnvironmentSettings, ITemplate, IParameterSet, string, System.Threading.CancellationToken)"/>.
    /// </summary>
    public interface ICreationEffects2
    {
        /// <summary>
        /// Gets the file changes done on template instantiation.
        /// </summary>
        IReadOnlyList<IFileChange2> FileChanges { get; }

        /// <summary>
        /// Gets the template creation result: primary outputs and post actions.
        /// </summary>
        ICreationResult CreationResult { get; }
    }
}
