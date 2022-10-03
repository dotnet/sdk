// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class Parameter : ITemplateParameter, IAllowDefaultIfOptionWithoutValue
#pragma warning restore CS0618 // Type or member is obsolete
    {
        public Parameter(string name, string type, string dataType)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(Name)} property should not be null or whitespace", nameof(name));
            }

            this.Name = name;
            this.Type = type;
            this.DataType = dataType;
            this.Precedence = TemplateParameterPrecedence.Default;
        }

        public IReadOnlyDictionary<string, ParameterChoice>? Choices { get; internal set; }

        public string? Documentation
        {
            get => Description;
            internal set => Description = value;
        }

        public string? Description { get; internal set; }

        public string? DefaultValue { get; internal set; }

        public string Name { get; internal init; }

        public string? DisplayName { get; internal set; }

        public bool IsName { get; internal set; }

        [Obsolete("Use Precedence instead.")]
        public TemplateParameterPriority Priority => Precedence.PrecedenceDefinition.ToTemplateParameterPriority();

        public TemplateParameterPrecedence Precedence { get; internal set; }

        public string Type { get; internal set; }

        public string DataType { get; internal set; }

        public string? DefaultIfOptionWithoutValue { get; set; }

        public bool AllowMultipleValues { get; internal set; }

        public bool EnableQuotelessLiterals { get; internal set; }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is ITemplateParameter parameter)
            {
                return Equals(parameter);
            }

            return false;
        }

        public override int GetHashCode() => Name.GetHashCode();

        public bool Equals(ITemplateParameter other) => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(other.Name) && Name == other.Name;
    }
}
