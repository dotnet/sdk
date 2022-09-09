// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions.Constraints
{
    /// <summary>
    /// Result of constraint evaluation.
    /// </summary>
    public class TemplateConstraintResult
    {
        private readonly string _constraintType;

        private TemplateConstraintResult(ITemplateConstraint constraint)
        {
            Constraint = constraint;
            _constraintType = constraint.Type;
        }

        private TemplateConstraintResult(string constraintType)
        {
            _constraintType = constraintType;
        }

        public enum Status
        {
            NotEvaluated,
            Allowed,
            Restricted
        }

        /// <summary>
        /// Gets the executed <see cref="ITemplateConstraint"/>.
        /// </summary>
        public ITemplateConstraint? Constraint { get; }

        /// <summary>
        /// Gets the executed constraint type.
        /// </summary>
        public string ConstraintType => Constraint?.Type ?? _constraintType;

        /// <summary>
        /// Determines if the conditions are met.
        /// </summary>
        public Status EvaluationStatus { get; private set; }

        /// <summary>
        /// Localized message explaining why the constraint is not met.
        /// </summary>
        public string? LocalizedErrorMessage { get; private set; }

        /// <summary>
        /// Localized message explaining what should be done by user in order the constraint is met. Optional.
        /// </summary>
        public string? CallToAction { get; private set; }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for allowed evaluation result.
        /// </summary>
        /// <param name="constraint">The executed constraint.</param>
        public static TemplateConstraintResult CreateAllowed(ITemplateConstraint constraint)
        {
            return new TemplateConstraintResult(constraint)
            {
                EvaluationStatus = Status.Allowed
            };
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for restricted evaluation result.
        /// </summary>
        /// <param name="constraint">The executed constraint.</param>
        /// <param name="localizedErrorMessage">The reason of the restriction.</param>
        /// <param name="cta">Call to action to fulfill the restriction (optional).</param>
        public static TemplateConstraintResult CreateRestricted(ITemplateConstraint constraint, string localizedErrorMessage, string? cta = null)
        {
            if (string.IsNullOrWhiteSpace(localizedErrorMessage))
            {
                throw new ArgumentException($"'{nameof(localizedErrorMessage)}' cannot be null or whitespace.", nameof(localizedErrorMessage));
            }

            return new TemplateConstraintResult(constraint)
            {
                EvaluationStatus = Status.Restricted,
                LocalizedErrorMessage = localizedErrorMessage,
                CallToAction = cta
            };
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for the case when the evaluation has failed.
        /// </summary>
        /// <param name="constraint">The executed constraint.</param>
        /// <param name="localizedErrorMessage">The reason of the failure.</param>
        /// <param name="cta">Call to action to resolve the problem (optional).</param>
        public static TemplateConstraintResult CreateEvaluationFailure(ITemplateConstraint constraint, string localizedErrorMessage, string? cta = null)
        {
            if (string.IsNullOrWhiteSpace(localizedErrorMessage))
            {
                throw new ArgumentException($"'{nameof(localizedErrorMessage)}' cannot be null or whitespace.", nameof(localizedErrorMessage));
            }

            return new TemplateConstraintResult(constraint)
            {
                EvaluationStatus = Status.NotEvaluated,
                LocalizedErrorMessage = localizedErrorMessage,
                CallToAction = cta
            };
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for the case when the constraint initialization has failed.
        /// If constraint was intiialized, use <see cref="CreateEvaluationFailure(ITemplateConstraint, string, string?)"/> instead.
        /// </summary>
        /// <param name="type">The executed constraint type.</param>
        /// <param name="localizedErrorMessage">The reason of the failure.</param>
        /// <param name="cta">Call to action to resolve the problem (optional).</param>
        public static TemplateConstraintResult CreateInitializationFailure(string type, string localizedErrorMessage, string? cta = null)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
            }

            if (string.IsNullOrWhiteSpace(localizedErrorMessage))
            {
                throw new ArgumentException($"'{nameof(localizedErrorMessage)}' cannot be null or whitespace.", nameof(localizedErrorMessage));
            }

            return new TemplateConstraintResult(type)
            {
                EvaluationStatus = Status.NotEvaluated,
                LocalizedErrorMessage = localizedErrorMessage,
                CallToAction = cta
            };
        }
    }
}

