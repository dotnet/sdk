// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class Parameter : ITemplateParameter, IExtendedTemplateParameter, IAllowDefaultIfOptionWithoutValue
    {
        [JsonProperty]
        [Obsolete("This property is no longer used. It is populated only when creating parameters from parameter and derived symbols for compatibility reason.")]
        public string FileRename { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, ParameterChoice> Choices { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Forms { get; set; }

        [JsonIgnore]
        public string Documentation
        {
            get { return Description; }
            set { Description = value; }
        }

        string ITemplateParameter.Name => Name;

        TemplateParameterPriority ITemplateParameter.Priority => Requirement;

        string ITemplateParameter.Type => Type;

        bool ITemplateParameter.IsName => IsName;

        string ITemplateParameter.DefaultValue => DefaultValue;

        string ITemplateParameter.DataType => DataType;

        string IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue
        {
            get
            {
                return DefaultIfOptionWithoutValue;
            }

            set
            {
                DefaultIfOptionWithoutValue = value;
            }
        }

        [JsonProperty]
        internal string Description { get; set; }

        [JsonProperty]
        internal string DefaultValue { get; set; }

        [JsonIgnore]
        internal string Name { get; set; }

        [JsonProperty]
        internal bool IsName { get; set; }

        [JsonProperty]
        internal TemplateParameterPriority Requirement { get; set; }

        [JsonProperty]
        internal string Type { get; set; }

        [JsonProperty]
        internal bool IsVariable { get; set; }

        [JsonProperty]
        internal string DataType { get; set; }

        [JsonProperty]
        internal string DefaultIfOptionWithoutValue { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }

        internal bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }
    }
}
