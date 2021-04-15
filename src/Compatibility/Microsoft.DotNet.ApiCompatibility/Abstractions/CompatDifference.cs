﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class representing a difference of compatibility, containing detailed information about it.
    /// </summary>
    public class CompatDifference : IDiagnostic, IEquatable<CompatDifference>
    {
        /// <summary>
        /// The Diagnostic ID for this difference.
        /// </summary>
        public string DiagnosticId { get; }

        /// <summary>
        /// The <see cref="DifferenceType"/>.
        /// </summary>
        public DifferenceType Type { get; }

        /// <summary>
        /// A diagnostic message for the difference.
        /// </summary>
        public virtual string Message { get; }

        /// <summary>
        /// A unique ID in order to identify the API that the difference was raised for.
        /// </summary>
        public string ReferenceId { get; }

        private CompatDifference() { }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="id"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="member"><see cref="ISymbol"/> for which the difference is associated to.</param>
        public CompatDifference(string diagnosticId, string message, DifferenceType type, ISymbol member)
            : this(diagnosticId, message, type, member?.GetDocumentationCommentId())
        {
        }

        /// <summary>
        /// Instantiate a new object representing the compatibility difference.
        /// </summary>
        /// <param name="id"><see cref="string"/> representing the diagnostic ID.</param>
        /// <param name="message"><see cref="string"/> message describing the difference.</param>
        /// <param name="type"><see cref="DifferenceType"/> to describe the type of the difference.</param>
        /// <param name="memberId"><see cref="string"/> containing the member ID for which the difference is associated to.</param>
        public CompatDifference(string diagnosticId, string message, DifferenceType type, string memberId)
        {
            DiagnosticId = diagnosticId ?? throw new ArgumentNullException(nameof(diagnosticId));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Type = type;
            ReferenceId = memberId ?? throw new ArgumentNullException(nameof(memberId));
        }

        /// <summary>
        /// Evaluates whether the current object is equal to another <see cref="CompatDifference"/>.
        /// </summary>
        /// <param name="other"><see cref="CompatDifference"/> to compare against.</param>
        /// <returns>True if equals, False if different.</returns>
        public bool Equals(CompatDifference other) =>
            other != null &&
            Type == other.Type &&
            DiagnosticId.Equals(other.DiagnosticId, StringComparison.OrdinalIgnoreCase) &&
            ReferenceId.Equals(other.ReferenceId, StringComparison.OrdinalIgnoreCase) &&
            Message.Equals(other.Message, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the hashcode that reperesents this instance.
        /// </summary>
        /// <returns>Unique <see cref="int"/> based on the properties' values of the instance.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(ReferenceId, DiagnosticId, Message, Type);

        /// <summary>
        /// Gets a <see cref="string"/> representation of the difference.
        /// </summary>
        /// <returns><see cref="string"/> describing the difference.</returns>
        public override string ToString() => $"{DiagnosticId} : {Message}";
    }
}
