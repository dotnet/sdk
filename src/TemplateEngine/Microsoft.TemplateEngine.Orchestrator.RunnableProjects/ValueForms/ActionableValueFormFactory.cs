// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    /// <summary>
    /// Base implementation for a <see cref="IValueFormFactory"/> for a <see cref="IValueForm"/> that doesn't have a configuration.
    /// </summary>
    internal abstract class ActionableValueFormFactory : BaseValueFormFactory
    {
        protected ActionableValueFormFactory(string identifier) : base(identifier) { }

        public override IValueForm FromJObject(string name, JObject? configuration = null)
        {
            return new ActionableValueForm(name, this);
        }

        public override IValueForm Create(string? name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Identifier;
            }
            return new ActionableValueForm(name!, this)
            {
                IsDefault = true
            };
        }

        protected abstract string Process(string value);

        protected class ActionableValueForm : BaseValueForm
        {
            private readonly ActionableValueFormFactory _factory;

            internal ActionableValueForm(string name, ActionableValueFormFactory factory) : base(name, factory.Identifier)
            {
                _factory = factory;
            }

            public override string Process(string value, IReadOnlyDictionary<string, IValueForm> otherForms)
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                return _factory.Process(value);
            }
        }
    }

}
