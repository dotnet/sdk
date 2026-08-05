// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;

namespace Microsoft.TemplateEngine.Edge.Constraints
{
    internal abstract class ConstraintBase : ITemplateConstraint
    {
        internal ConstraintBase(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory)
        {
            EnvironmentSettings = environmentSettings;
            Factory = factory;
        }

        public string Type => Factory.Type;

        public abstract string DisplayName { get; }

        protected IEngineEnvironmentSettings EnvironmentSettings { get; }

        protected ITemplateConstraintFactory Factory { get; }

        public TemplateConstraintResult Evaluate(string? args)
        {
            try
            {
                return EvaluateInternal(args);
            }
            catch (ConfigurationException ce)
            {
                return TemplateConstraintResult.CreateEvaluationFailure(this, ce.Message, LocalizableStrings.Generic_Constraint_WrongConfigurationCTA);
            }
        }

        protected abstract TemplateConstraintResult EvaluateInternal(string? args);
    }
}
