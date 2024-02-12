// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    /// <summary>
    /// Base implementation of <see cref="IValueFormFactory"/>.
    /// When implementing a value form inherit from <see cref="ActionableValueFormFactory"/> or <see cref="ConfigurableValueFormFactory{T}"/> or <see cref="DependentValueFormFactory{T}"/> instead.
    /// </summary>
    internal abstract class BaseValueFormFactory : IValueFormFactory
    {
        private readonly string _identifier;

        protected BaseValueFormFactory(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"'{nameof(identifier)}' cannot be null or whitespace.", nameof(identifier));
            }
            _identifier = identifier;
        }

        public string Identifier => _identifier;

        public abstract IValueForm Create(string? name = null);

        public abstract IValueForm FromJObject(string name, JObject? configuration = null);

        protected abstract class BaseValueForm : IValueForm
        {
            internal BaseValueForm(string name, string identifier)
            {
                Name = name;
                Identifier = identifier;
            }

            public string Identifier { get; }

            public string Name { get; }

            public bool IsDefault { get; init; }

            public abstract string Process(string value, IReadOnlyDictionary<string, IValueForm> otherForms);
        }
    }

}
