// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines a component loadable by <see cref="IComponentManager"/>.
    /// </summary>
    public interface IIdentifiedComponent
    {
        /// <summary>
        /// Gets the identifier of the component. Should be unique.
        /// </summary>
        Guid Id { get; }
    }
}
