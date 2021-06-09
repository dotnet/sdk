// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Globalization;

namespace Microsoft.DotNet.ValidationSuppression
{
    /// <summary>
    /// Represents a Suppression for a validation error.
    /// </summary>
    public class Suppression : IEquatable<Suppression>
    {
        /// <summary>
        /// The DiagnosticId represenging the error to be suppressed.
        /// </summary>
        public string? DiagnosticId { get; set; }

        /// <summary>
        /// The target of where to suppress the <see cref="DiagnosticId"/>
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Left operand of an APICompat comparison.
        /// </summary>
        public string? Left { get; set; }

        /// <summary>
        /// Right operand of an APICOmpat comparison.
        /// </summary>
        public string? Right { get; set; }

        /// <inheritdoc/>
        public bool Equals(Suppression? other)
        {
            if (other == null)
            {
                return false;
            }

            StringComparer stringComparer = StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true);

            if ((!string.IsNullOrEmpty(DiagnosticId?.Trim()) || !string.IsNullOrEmpty(other.DiagnosticId?.Trim())) && !stringComparer.Equals(DiagnosticId?.Trim(), other.DiagnosticId?.Trim()))
            {
                return false;
            }

            if ((!string.IsNullOrEmpty(Target?.Trim()) || !string.IsNullOrEmpty(other.Target?.Trim())) && !stringComparer.Equals(Target?.Trim(), other.Target?.Trim()))
            {
                return false;
            }

            if ((!string.IsNullOrEmpty(Left?.Trim()) || !string.IsNullOrEmpty(other.Left?.Trim())) && !stringComparer.Equals(Left?.Trim(), other.Left?.Trim()))
            {
                return false;
            }

            if ((!string.IsNullOrEmpty(Right?.Trim()) || !string.IsNullOrEmpty(other.Right?.Trim())) && !stringComparer.Equals(Right?.Trim(), other.Right?.Trim()))
            {
                return false;
            }

            return true;
        }
    }
}
