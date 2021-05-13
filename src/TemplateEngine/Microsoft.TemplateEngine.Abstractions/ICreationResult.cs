// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Stores info about template creation, to be returned to the host as the result of template instantiation or dry run.
    /// </summary>
    public interface ICreationResult
    {
        /// <summary>
        /// Gets post actions to be done by the host.
        /// </summary>
        IReadOnlyList<IPostAction> PostActions { get; }

        /// <summary>
        /// Gets the primary outputs of template instantiation.
        /// These are the files that post actions should be applied on.
        /// </summary>
        IReadOnlyList<ICreationPath> PrimaryOutputs { get; }
    }
}
