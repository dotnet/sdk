// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    /// <summary>
    /// Represents result of template instantiation / dry run via <see cref="TemplateCreator.InstantiateAsync(ITemplateInfo, string?, string?, System.Collections.Generic.IReadOnlyDictionary{string, string}, bool, string?, bool, System.Threading.CancellationToken)". />.
    /// </summary>
    public interface ITemplateCreationResult
    {
        /// <summary>
        /// Result of template dry run.
        /// Template dry run always performed prior to instantiation.
        /// </summary>
        ICreationEffects? CreationEffects { get; }

        /// <summary>
        /// Error message, null if operation is successful.
        /// Should be localized.
        /// </summary>
        string? ErrorMessage { get; }

        /// <summary>
        /// The output directory used for template instantiation.
        /// </summary>
        string? OutputBaseDirectory { get; }

        /// <summary>
        /// The result of template instantiation.
        /// Null in case of dry run.
        /// </summary>
        ICreationResult? CreationResult { get; }

        /// <summary>
        /// Status of template instantiation.
        /// </summary>
        CreationResultStatus Status { get; }

        /// <summary>
        /// Processed template name.
        /// </summary>
        string TemplateFullName { get; }
    }
}
