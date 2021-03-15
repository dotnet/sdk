// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CacheParameter : ICacheParameter, IAllowDefaultIfOptionWithoutValue
    {
        public CacheParameter(string dataType, string? defaultValue, string? displayName, string? description)
            : this(dataType, defaultValue, displayName, description, null)
        {
        }

        public CacheParameter(string dataType, string? defaultValue, string? displayName, string? description, string? defaultIfOptionWithoutValue)
        {
            DataType = dataType;
            DefaultValue = defaultValue;
            DisplayName = displayName;
            Description = description;
            DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
        }

        [JsonProperty]
        public string? DataType { get; }

        [JsonProperty]
        public string? DefaultValue { get; }

        [JsonProperty]
        public string? DisplayName { get; }

        [JsonProperty]
        public string? Description { get; }

        [JsonProperty]
        public string? DefaultIfOptionWithoutValue { get; set; }

        public bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }
    }
}
