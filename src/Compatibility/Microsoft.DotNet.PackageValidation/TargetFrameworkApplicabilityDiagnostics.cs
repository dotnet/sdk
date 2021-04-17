// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Base class for reporting package validation diagnostics.
    /// </summary>
    public class TargetFrameworkApplicabilityDiagnostics : IDiagnostic
    {
        public string DiagnosticId { get; }
        public string ReferenceId { get; }
        public string Message { get; }

        private TargetFrameworkApplicabilityDiagnostics() { }

        public TargetFrameworkApplicabilityDiagnostics(string diagnosticId, string referenceId, string message)
        {
            DiagnosticId = diagnosticId;
            ReferenceId = referenceId;
            Message = message;
        }
        public override string ToString() => $"{DiagnosticId} : {Message}";
    }
}
