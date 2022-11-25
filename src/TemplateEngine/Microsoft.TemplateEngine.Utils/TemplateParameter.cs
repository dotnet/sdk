// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
#pragma warning disable CS0618 // Type or member is obsolete - compatibility
    public class TemplateParameter : ITemplateParameter, IAllowDefaultIfOptionWithoutValue
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private string? _defaultIfOptionWithoutValue;

        public TemplateParameter(
            string name,
            string type,
            string datatype,
            TemplateParameterPrecedence? precedence = default,
            bool isName = false,
            string? defaultValue = null,
            string? defaultIfOptionWithoutValue = null,
            string? description = null,
            string? displayName = null,
            bool allowMultipleValues = false,
            IReadOnlyDictionary<string, ParameterChoice>? choices = null)
        {
            Name = name;
            Type = type;
            DataType = datatype;
            IsName = isName;
            DefaultValue = defaultValue;
            DefaultIfOptionWithoutValue = defaultIfOptionWithoutValue;
            Description = description;
            DisplayName = displayName;
            AllowMultipleValues = allowMultipleValues;
            Precedence = precedence ?? TemplateParameterPrecedence.Default;

            if (this.IsChoice())
            {
                Choices = choices ?? new Dictionary<string, ParameterChoice>();
            }
        }

        [Obsolete("Use Description instead.")]
        public string? Documentation => Description;

        public string Name { get; }

        [Obsolete("Use Precedence instead.")]
        public TemplateParameterPriority Priority => Precedence.PrecedenceDefinition.ToTemplateParameterPriority();

        public TemplateParameterPrecedence Precedence { get; init; } = TemplateParameterPrecedence.Default;

        public string Type { get; }

        public bool IsName { get; init; }

        public string? DefaultValue { get; init; }

        public string DataType { get; }

        public string? DefaultIfOptionWithoutValue
        {
            get => _defaultIfOptionWithoutValue;
            init => _defaultIfOptionWithoutValue = value;
        }

        public IReadOnlyDictionary<string, ParameterChoice>? Choices { get; init; }

        public string? Description { get; init; }

        public string? DisplayName { get; init; }

        public bool AllowMultipleValues { get; init; }

        string? IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue
        {
            get => _defaultIfOptionWithoutValue;
            set => _defaultIfOptionWithoutValue = value;
        }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }

        public override bool Equals(object obj)
        {
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

        public override int GetHashCode() => Name != null ? Name.GetHashCode() : 0;

        public bool Equals(ITemplateParameter other) => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(other.Name) && Name == other.Name;
    }

}
