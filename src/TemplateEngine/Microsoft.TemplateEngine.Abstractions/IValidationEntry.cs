// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Represents a validation error when loading the template.
    /// </summary>
    public interface IValidationEntry
    {
        /// <summary>
        /// Error severity.
        /// </summary>
        public enum SeverityLevel
        {
            None,
            Info,
            Warning,
            Error,
        }

        /// <summary>
        /// Gets the error severity.
        /// </summary>
        SeverityLevel Severity { get; }

        /// <summary>
        /// Gets the code of the error.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Gets the error message (localized when available).
        /// </summary>
        string ErrorMessage { get; }

        /// <summary>
        /// Gets the details about error location.
        /// </summary>
        ErrorLocation? Location { get; }

        /// <summary>
        /// Details of the error location.
        /// </summary>
        public readonly struct ErrorLocation
        {
            /// <summary>
            /// Gets the filename where the error occurred.
            /// </summary>
            public string Filename { get; init; }

            /// <summary>
            /// Gets the filename where the error occurred.
            /// </summary>
            public int LineNumber { get; init; }

            /// <summary>
            /// Gets the filename where the error occurred.
            /// </summary>
            public int Position { get; init; }
        }
    }
}
