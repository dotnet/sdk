// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.Components
{
    /// <summary>
    /// The component provides values which may be bound to "bind" symbol.
    /// The priority order is determined by <see cref="IPrioritizedComponent.Priority"/>.
    /// </summary>
    public interface IBindSymbolSource : IPrioritizedComponent
    {
        /// <summary>
        /// The user friendly name of the component.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Prefix that is used in binding to reference the component.
        /// </summary>
        public string? SourcePrefix { get; }

        /// <summary>
        /// If set to true, the component required exact prefix match to be used.
        /// </summary>
        public bool RequiresPrefixMatch { get; }

        /// <summary>
        /// Gets the value corresponding to <paramref name="bindName"/>.
        /// </summary>
        /// <param name="settings">template engine environment settings.</param>
        /// <param name="bindName">the value to retrieve (without prefix).</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns></returns>
        public Task<string?> GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindName, CancellationToken cancellationToken);
    }
}
