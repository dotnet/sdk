// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.TemplateEngine.TemplateLocalizer.Core.Exceptions;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators
{
    /// <summary>
    /// Creates a key for a given element using the value of one of its members.
    /// The type of the value of the member should be string.
    /// </summary>
    internal sealed class ChildValueKeyCreator : IJsonKeyCreator
    {
        public ChildValueKeyCreator(string memberPropertyName)
        {
            MemberPropertyName = memberPropertyName;
        }

        /// <summary>
        /// Gets the name of the property to be searched in the member list
        /// of the <see cref="JsonElement"/> that the key will be created for.
        /// </summary>
        public string MemberPropertyName { get; }

        /// <inheritdoc/>
        public string CreateKey(JsonElement element, string? elementName, string? parentElementName, int indexInParent)
        {
            if (!element.TryGetProperty(MemberPropertyName, out JsonElement keyProperty) || keyProperty.ValueKind != JsonValueKind.String)
            {
                string owningElementName = (parentElementName == null ? elementName : (parentElementName + TemplateStringExtractor._keySeparator + elementName)) ?? string.Empty;
                throw new JsonMemberMissingException(owningElementName, MemberPropertyName);
            }

            string key = keyProperty.GetString() ?? string.Empty;
            return parentElementName == null ? key : string.Concat(parentElementName, TemplateStringExtractor._keySeparator, key);
        }
    }
}
