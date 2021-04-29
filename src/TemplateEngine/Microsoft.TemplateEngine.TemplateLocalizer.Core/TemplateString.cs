// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    /// <summary>
    /// Represents a string in template.json file that needs to be localized.
    /// </summary>
    internal struct TemplateString : IEquatable<TemplateString>
    {
        /// <summary>
        /// Creates an instance of <see cref="TemplateString"/>.
        /// </summary>
        /// <param name="identifier">A string that uniquely identifies this template field in the template.json file.</param>
        /// <param name="localizationKey">A string that uniquely identifies this field in localized templatestrings.json file.</param>
        /// <param name="value">Localizable string.</param>
        public TemplateString(string identifier, string localizationKey, string value)
        {
            Identifier = identifier;
            LocalizationKey = localizationKey;
            Value = value;
        }

        /// <summary>
        /// Gets the string that identifies this localizable string in template.json file.
        /// Given a template.json file and an <see cref="Identifier"/>, it is possible to
        /// directly find the corresponding JSON element.
        /// </summary>
        /// <example>"symbols.Framework.choices.[0].displayName".</example>
        public string Identifier { get; }

        /// <summary>
        /// Gets the key of this localizable string, which identifies this in templatestrings.json
        /// file. This is different than <see cref="Identifier"/> in the way that it is intended to
        /// be more user friendly.
        /// </summary>
        /// <example>"symbols.Framework.choices.net6_0.displayName".</example>
        public string LocalizationKey { get; }

        /// <summary>
        /// Gets the value of the string found at the location identified with <see cref="Identifier"/>.
        /// </summary>
        public string Value { get; }

        /// <inheritdoc/>
        public bool Equals(TemplateString other)
        {
            return Identifier == other.Identifier
                && LocalizationKey == other.LocalizationKey
                && Value == other.Value;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is TemplateString other))
            {
                return false;
            }

            return Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return unchecked(((17 * 23 + Identifier.GetHashCode()) * 23
                + (LocalizationKey?.GetHashCode() ?? 0)) * 23
                + (Value?.GetHashCode() ?? 0));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Concat('{', LocalizationKey, ',', Value, '}');
        }
    }
}
