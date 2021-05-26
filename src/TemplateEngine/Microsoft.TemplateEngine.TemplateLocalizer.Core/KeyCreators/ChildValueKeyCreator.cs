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
        /// <summary>
        /// Creates an instance of <see cref="ChildValueKeyCreator"/>.
        /// </summary>
        /// <param name="memberPropertyName">Name of the member containing the value of the key.</param>
        /// <param name="onlyChildDefaultValue">Default value to be used if the element does not have
        /// a property with name specified in <paramref name="memberPropertyName"/> and it is the only
        /// child of the parent element. If the value is null and the specified member doesn't exist,
        /// <see cref="JsonMemberMissingException"/> will be thrown.</param>
        public ChildValueKeyCreator(string memberPropertyName, string? onlyChildDefaultValue = default)
        {
            MemberPropertyName = memberPropertyName;
            OnlyChildDefaultValue = onlyChildDefaultValue;
        }

        /// <summary>
        /// Gets the name of the property to be searched in the member list
        /// of the <see cref="JsonElement"/> that the key will be created for.
        /// </summary>
        public string MemberPropertyName { get; }

        /// <summary>
        /// Gets the default value to be used in the case that the element doesn't have a member with
        /// the specified name and it is the only child of its parent.
        /// </summary>
        public string? OnlyChildDefaultValue { get; }

        /// <inheritdoc/>
        public string CreateKey(JsonElement element, string? elementName, string? parentElementName, int indexInParent, int parentChildCount)
        {
            string key = string.Empty;

            if (element.TryGetProperty(MemberPropertyName, out JsonElement keyProperty) && keyProperty.ValueKind == JsonValueKind.String)
            {
                key = keyProperty.GetString() ?? string.Empty;
            }
            else if (OnlyChildDefaultValue != null && parentChildCount == 1)
            {
                key = OnlyChildDefaultValue;
            }
            else
            {
                string owningElementName = (parentElementName == null ? elementName : (parentElementName + TemplateStringExtractor._keySeparator + elementName)) ?? string.Empty;
                throw new JsonMemberMissingException(owningElementName, MemberPropertyName);
            }

            return parentElementName == null ? key : string.Concat(parentElementName, TemplateStringExtractor._keySeparator, key);
        }
    }
}
