// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    /// <summary>
    /// Represents the result of an export operation where template.json files are used to generate templatestrings.json files.
    /// </summary>
    public sealed class ExportResult
    {
        /// <summary>
        /// Creates an instance of <see cref="ExportResult"/>.
        /// </summary>
        public ExportResult(string? templateJsonPath) : this(templateJsonPath, null, null) { }

        /// <summary>
        /// Creates an instance of <see cref="ExportResult"/>.
        /// </summary>
        public ExportResult(string? templateJsonPath, string? errorMessage) : this(templateJsonPath, templateJsonPath, null) { }

        /// <summary>
        /// Creates an instance of <see cref="ExportResult"/>.
        /// </summary>
        public ExportResult(string? templateJsonPath, Exception? innerException) : this(templateJsonPath, null, innerException) { }

        /// <summary>
        /// Creates an instance of <see cref="ExportResult"/>.
        /// </summary>
        public ExportResult(string? templateJsonPath, string? errorMessage, Exception? innerException)
        {
            TemplateJsonPath = templateJsonPath;
            ErrorMessage = errorMessage;
            InnerException = innerException;
        }

        /// <summary>
        /// Gets the path to the template.json file that was used as input.
        /// </summary>
        public string? TemplateJsonPath { get; }

        /// <summary>
        /// Gets the success state of the operation.
        /// </summary>
        public bool Succeeded => ErrorMessage == null && InnerException == null;

        /// <summary>
        /// Gets the message explaining the underlying error, if the operation has failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the related exception in the case that the operation fails.
        /// Value of this property may be null if the underlying error is detected
        /// at the application logic and no actual exceptions occured.
        /// </summary>
        public Exception? InnerException { get; }

        public override string ToString()
        {
            return $"{nameof(ExportResult)} {{{TemplateJsonPath}, {ErrorMessage}, {InnerException?.Message}}}";
        }
    }
}
