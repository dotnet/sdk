// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    /// <summary>
    /// Base implementation for a <see cref="IValueFormFactory"/> for a <see cref="IValueForm"/> that has a configuration of type <typeparamref name="T"/> and needs to know all the forms defined to process a value form.
    /// </summary>
    internal abstract class DependentValueFormFactory<T> : BaseValueFormFactory where T : class
    {
        protected DependentValueFormFactory(string identifier) : base(identifier) { }

        public override IValueForm FromJObject(string name, JObject? configuration)
        {
            if (configuration != null)
            {
                T config = ReadConfiguration(configuration);
                return new DependentValueForm(name, this, config);
            }
            return new DependentValueForm(name, this, null);
        }

        public override IValueForm Create(string? name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Identifier;
            }
            return new DependentValueForm(name!, this, null)
            {
                IsDefault = true
            };
        }

        protected abstract T ReadConfiguration(JObject jObject);

        protected abstract string Process(string value, T? configuration, IReadOnlyDictionary<string, IValueForm> otherForms);

        private class DependentValueForm : BaseValueForm
        {
            private readonly DependentValueFormFactory<T> _factory;
            private readonly T? _configuration;

            internal DependentValueForm(string name, DependentValueFormFactory<T> factory, T? configuration) : base(name, factory.Identifier)
            {
                _factory = factory;
                _configuration = configuration;
            }

            public override string Process(string value, IReadOnlyDictionary<string, IValueForm> otherForms)
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                return _factory.Process(value, _configuration, otherForms);
            }
        }
    }

}
