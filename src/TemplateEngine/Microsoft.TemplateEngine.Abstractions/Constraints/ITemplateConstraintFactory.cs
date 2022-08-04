// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.Constraints
{
    /// <summary>
    /// Template constraint factory used to create and initialize the instance of <see cref="ITemplateConstraint"/>.
    /// The template constraint should be initialized after creation to be able to evaluate in constraint is met performant enough.
    /// </summary>
    public interface ITemplateConstraintFactory : IIdentifiedComponent
    {
        /// <summary>
        /// Gets the constraint type. Should be unique and match the definition in `template.json`.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Creates and initializes new <see cref="ITemplateConstraint"/> based on current <paramref name="environmentSettings"/>.
        /// </summary>
        Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken);
    }
}

