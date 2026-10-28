// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators
{
    /// <summary>
    /// Creates keys for a given <see cref="JsonElement"/> of a JSON document that can be used to uniquely identify the element
    /// among its siblings under the same parent <see cref="JsonElement"/>.
    /// </summary>
    internal interface IJsonKeyCreator
    {
        /// <summary>
        /// Creates a key for the given <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="element">The element that the key will uniquely represent under the same parent.</param>
        /// <param name="elementName">Name of the element that the key is being created for.</param>
        /// <param name="parentElementName">Name of the parent element.</param>
        /// <param name="indexInParent">Index of this element with respect to its siblings under the same parent.</param>
        /// <param name="parentChildCount">The number of children that the parent element has.</param>
        /// <returns>The key string.</returns>
        string CreateKey(JsonElement element, string? elementName, string? parentElementName, int indexInParent, int parentChildCount);
    }
}
