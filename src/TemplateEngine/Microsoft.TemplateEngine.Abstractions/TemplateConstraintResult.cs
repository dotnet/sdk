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
        public TemplateConstraintResult(Status status, string? localizedMessage = null, string? callToAction = null)
        {
            EvaluationStatus = status;

            if (status != Status.Allowed && string.IsNullOrWhiteSpace(localizedMessage))
            {
                throw new ArgumentException($"'{nameof(localizedMessage)}' cannot be null or whitespace when '{nameof(status)}' is not '{Status.Allowed}'.", nameof(localizedMessage));
            }

            LocalizedErrorMessage = localizedMessage;
            CallToAction = callToAction;
        }

        public enum Status
        {
            NotEvaluated,
            Allowed,
            Restricted
        }

        /// <summary>
        /// Determines if the conditions are met.
        /// </summary>
        public Status EvaluationStatus { get; }

        /// <summary>
        /// Localized message explaining why the constraint is not met.
        /// </summary>
        public string? LocalizedErrorMessage { get; }

        /// <summary>
        /// Localized message explaining what should be done by user in order the constraint is met. Optional.
        /// </summary>
        public string? CallToAction { get; }
    }
}

