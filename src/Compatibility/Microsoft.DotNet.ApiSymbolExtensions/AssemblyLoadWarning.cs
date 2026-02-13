// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Class that represents a warning that occurred while trying to load a specific assembly.
    /// </summary>
    /// <param name="diagnosticId">String representing the diagnostic ID.</param>
    /// <param name="referenceId">String representing the ID for the object that the diagnostic was created for.</param>
    /// <param name="message">String describing the diagnostic.</param>
    public class AssemblyLoadWarning(string diagnosticId, string referenceId, string message) : IDiagnostic, IEquatable<AssemblyLoadWarning>
    {
        private readonly StringComparer _ordinalComparer = StringComparer.Ordinal;

        /// <inheritdoc/>
        public string DiagnosticId { get; } = diagnosticId;

        /// <inheritdoc/>
        public string ReferenceId { get; } = referenceId;

        /// <inheritdoc/>
        public string Message { get; } = message;

        /// <inheritdoc/>
        public bool Equals(AssemblyLoadWarning? other) => other != null &&
            _ordinalComparer.Equals(DiagnosticId, other.DiagnosticId) &&
            _ordinalComparer.Equals(ReferenceId, other.ReferenceId) &&
            _ordinalComparer.Equals(Message, other.Message);

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is AssemblyLoadWarning assemblyLoadWarning && Equals(assemblyLoadWarning);

        /// <inheritdoc />
        public override int GetHashCode()
        {
#if NET
            return HashCode.Combine(DiagnosticId, ReferenceId, Message);
#else
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DiagnosticId);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ReferenceId);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Message);
            return hashCode;
#endif
        }
    }
}
