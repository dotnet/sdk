// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators
{
    /// <summary>
    /// Creates a key for a given element using the name of the element.
    /// </summary>
    internal sealed class NameKeyCreator : IJsonKeyCreator
    {
        /// <inheritdoc/>
        public string CreateKey(JsonElement element, string? elementName, string? parentElementName, int indexInParent, int parentChildCount)
        {
            return elementName ?? string.Empty;
        }
    }
}
