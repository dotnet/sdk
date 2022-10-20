// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators
{
    /// <summary>
    /// Creates a key for a given element using the elements index with respect to its siblings.
    /// </summary>
    internal sealed class IndexBasedKeyCreator : IJsonKeyCreator
    {
        /// <inheritdoc/>
        public string CreateKey(JsonElement element, string? elementName, string? parentElementName, int indexInParent, int parentChildCount)
        {
            return string.Concat(parentElementName, "[", indexInParent.ToString(), "]");
        }
    }
}
