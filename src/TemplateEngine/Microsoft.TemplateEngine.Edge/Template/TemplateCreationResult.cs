// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    internal class TemplateCreationResult : ITemplateCreationResult
    {
        /// <summary>
        /// Creates <see cref="TemplateCreationResult"/>.
        /// </summary>
        /// <param name="status">status of template creation.</param>
        /// <param name="templateName">template name.</param>
        /// <param name="localizedErrorMessage">localized error message. Should be set when <paramref name="status"/> is not <see cref="CreationResultStatus.Success"/>.
        /// </param>
        /// <param name="creationOutputs">results of template creation.</param>
        /// <param name="outputBaseDir">output directory.</param>
        /// <param name="creationEffects">results of template dry run.</param>
        /// <exception cref="ArgumentException">when <paramref name="localizedErrorMessage"/> is null or empty and <paramref name="status"/> is not <see cref="CreationResultStatus.Success"/>.</exception>
        internal TemplateCreationResult(
            CreationResultStatus status,
            string templateName,
            string? localizedErrorMessage = null,
            ICreationResult? creationOutputs = null,
            string? outputBaseDir = null,
            ICreationEffects? creationEffects = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                throw new ArgumentException($"'{nameof(templateName)}' cannot be null or whitespace.", nameof(templateName));
            }

            Status = status;
            ErrorMessage = localizedErrorMessage;
            if (string.IsNullOrWhiteSpace(ErrorMessage) && status != CreationResultStatus.Success)
            {
                throw new ArgumentException($"{nameof(localizedErrorMessage)} cannot be null or empty when {nameof(status)} is not {CreationResultStatus.Success}", nameof(localizedErrorMessage));
            }

            TemplateFullName = templateName;
            CreationResult = creationOutputs;
            OutputBaseDirectory = outputBaseDir;
            CreationEffects = creationEffects;
        }

        public string? ErrorMessage { get; }

        public CreationResultStatus Status { get; }

        public string TemplateFullName { get; }

        public ICreationResult? CreationResult { get; }

        public string? OutputBaseDirectory { get; }

        public ICreationEffects? CreationEffects { get; }
    }
}
