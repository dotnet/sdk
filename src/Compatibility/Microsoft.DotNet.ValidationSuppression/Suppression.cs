// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

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
        public bool Equals(Suppression other)
        {
            if (DiagnosticId != null && !DiagnosticId.Equals(other.DiagnosticId, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (Target != null && !Target.Equals(other.Target, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (Left != null && !Left.Equals(other.Left, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (Right != null && !Right.Equals(other.Right, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
