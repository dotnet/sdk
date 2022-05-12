// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Result of constraint evaluation.
    /// </summary>
    public class TemplateConstraintResult
    {
        private TemplateConstraintResult(string type)
        {
            ConstraintType = type;
        }

        public enum Status
        {
            NotEvaluated,
            Allowed,
            Restricted
        }

        public string ConstraintType { get; }

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
        /// <param name="type">Constraint type.</param>
        public static TemplateConstraintResult CreateAllowed(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
            }

            return new TemplateConstraintResult(type)
            {
                EvaluationStatus = Status.Allowed
            };
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for allowed evaluation result.
        /// </summary>
        /// <param name="constraintInfo">Constraint defintion.</param>
        public static TemplateConstraintResult CreateAllowed(TemplateConstraintInfo constraintInfo)
        {
            return CreateAllowed(constraintInfo.Type);
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for restricted evaluation result.
        /// </summary>
        /// <param name="type">Constraint type.</param>
        /// <param name="localizedErrorMessage">The reason of the restriction.</param>
        /// <param name="cta">Call to action to fulfill the restriction (optional).</param>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
        public static TemplateConstraintResult CreateRestricted(string type, string localizedErrorMessage, string? cta = null)
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
        {
            if (string.IsNullOrWhiteSpace(localizedErrorMessage))
            {
                throw new ArgumentException($"'{nameof(localizedErrorMessage)}' cannot be null or whitespace.", nameof(type));
            }

            return new TemplateConstraintResult(type)
            {
                EvaluationStatus = Status.Restricted,
                LocalizedErrorMessage = localizedErrorMessage,
                CallToAction = cta
            };
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for restricted evaluation result.
        /// </summary>
        /// <param name="constraintInfo">Constraint that was evaluated.</param>
        /// <param name="localizedErrorMessage">The reason of the restriction.</param>
        /// <param name="cta">Call to action to fulfill the restriction (optional).</param>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
        public static TemplateConstraintResult CreateRestricted(TemplateConstraintInfo constraintInfo, string localizedErrorMessage, string? cta = null)
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
        {
            return CreateRestricted(constraintInfo.Type, localizedErrorMessage, cta);
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for the case when the evaluation has failed.
        /// </summary>
        /// <param name="type">Constraint type.</param>
        /// <param name="localizedErrorMessage">The reason of the failure.</param>
        public static TemplateConstraintResult CreateFailure(string type, string localizedErrorMessage)
        {
            if (string.IsNullOrWhiteSpace(localizedErrorMessage))
            {
                throw new ArgumentException($"'{nameof(localizedErrorMessage)}' cannot be null or whitespace.", nameof(type));
            }

            return new TemplateConstraintResult(type)
            {
                EvaluationStatus = Status.NotEvaluated,
                LocalizedErrorMessage = localizedErrorMessage
            };
        }

        /// <summary>
        /// Creates <see cref="TemplateConstraintResult"/> for the case when the evaluation has failed.
        /// </summary>
        /// <param name="constraintInfo">Constraint.</param>
        /// <param name="localizedErrorMessage">The reason of the failure.</param>
        public static TemplateConstraintResult CreateFailure(TemplateConstraintInfo constraintInfo, string localizedErrorMessage)
        {
            return CreateFailure(constraintInfo.Type, localizedErrorMessage);
        }
    }
}

