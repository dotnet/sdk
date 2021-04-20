// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Custom composition manager that allows mapping between <see cref="Guid"/> and components that implement <see cref="IIdentifiedComponent"/>.
    /// Used for generators, post actions, different implementations of installers, providers...
    /// </summary>
    public interface IComponentManager
    {
        /// <summary>
        /// Gets specific component via <see cref="Guid"/>.
        /// E.g: to lookup implementation of "Restore NuGet packages" post action, pass in {210D431B-A78B-4D2F-B762-4ED3E3EA9025} <see cref="Guid"/>.
        /// </summary>
        /// <remarks>
        /// If <typeparamref name="T"/> and <see cref="Guid"/> mismatch, <c>false</c> is returned.
        /// </remarks>
        /// <typeparam name="T">type to lookup.</typeparam>
        /// <param name="id"><see cref="Guid"/> that is defined in <see cref="IIdentifiedComponent.Id"/>.</param>
        /// <param name="component">singleton instance of requested component.</param>
        /// <returns><c>true</c> if component was found.</returns>
        bool TryGetComponent<T>(Guid id, out T component)
            where T : class, IIdentifiedComponent;

        /// <summary>
        /// Returns all components of specified type.
        /// </summary>
        /// <typeparam name="T">type of component.</typeparam>
        /// <returns>singleton component instances of requested type.</returns>
        IEnumerable<T> OfType<T>()
            where T : class, IIdentifiedComponent;

        /// <summary>
        /// Adds component to manager, which can be looked up later via <see cref="TryGetComponent{T}(Guid, out T)"/> or <see cref="OfType{T}"/>.
        /// </summary>
        /// <param name="type">type that implements <see cref="IIdentifiedComponent"/>.</param>
        void Register(Type type);

        /// <summary>
        /// Adds list of components to manager, which can be looked up later via <see cref="TryGetComponent{T}(Guid, out T)"/> or <see cref="OfType{T}"/>.
        /// </summary>
        /// <param name="typeList"></param>
        void RegisterMany(IEnumerable<Type> typeList);
    }
}
