// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class CacheTag : ICacheTag, IAllowDefaultIfOptionWithoutValue
    {
        public CacheTag(string? displayName, string? description, IReadOnlyDictionary<string, ParameterChoice> choices, string? defaultValue)
            : this(displayName, description, choices, defaultValue, null)
        {
        }

        public CacheTag(string? displayName, string? description, IReadOnlyDictionary<string, ParameterChoice> choices, string? defaultValue, string? defaultIfOptionWithoutValue)
        {
            DisplayName = displayName;
            Description = description;
            Choices = choices.CloneIfDifferentComparer(StringComparer.OrdinalIgnoreCase);
            DefaultValue = defaultValue;
            DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
        }

        public string? DisplayName { get; }

        public string? Description { get; }

        public IReadOnlyDictionary<string, ParameterChoice> Choices { get; }

        public string? DefaultValue { get; }

        public string? DefaultIfOptionWithoutValue { get; set; }

        public bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }
    }
}
