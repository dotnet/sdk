// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class to wrap an Element of T with it's <see cref="MetadataInformation"/>.
    /// </summary>
    /// <typeparam name="T">The type of the Element that is held</typeparam>
    /// <param name="element">Element to store in the container.</param>
    /// <param name="metadataInformation">Metadata related to the element</param>
    public class ElementContainer<T>(T element, MetadataInformation metadataInformation) where T : ISymbol
    {
        /// <summary>
        /// The element that the container is holding.
        /// </summary>
        public readonly T Element = element;

        /// <summary>
        /// The metadata associated to the element.
        /// </summary>
        public readonly MetadataInformation MetadataInformation = metadataInformation;
    }
}
