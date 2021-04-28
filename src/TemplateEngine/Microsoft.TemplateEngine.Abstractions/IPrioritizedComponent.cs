// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Allows component to set its own priority compared to other components of same type.
    /// It is up to caller of <see cref="IComponentManager.OfType{T}"/> to order and respect this.
    /// </summary>
    public interface IPrioritizedComponent : IIdentifiedComponent
    {
        /// <summary>
        /// Default priority is 0.
        /// Components of same type that don't implement this interface are considered as priority 0.
        /// Notice that negative priority can be used, which places component below default.
        /// Example order of execution will be:
        /// 1) a component with priority 100
        /// 2) a component without priority(defaults to 0)
        /// 3) a component with priority -100.
        /// </summary>
        int Priority { get; }
    }
}
