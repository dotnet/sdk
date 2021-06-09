// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ValidationSuppression
{
    /// <summary>
    /// Defines methods to support the comparison of Suppression objects for equality.
    /// </summary>
    internal class SuppressionComparer : IEqualityComparer<Suppression>
    {
        /// <inheritdoc/>
        public bool Equals(Suppression x, Suppression y) => x.Equals(y);

        /// <inheritdoc/>
        public int GetHashCode(Suppression obj) => HashCode.Combine(obj.DiagnosticId?.ToLowerInvariant(), obj.Target?.ToLowerInvariant(), obj.Left?.ToLowerInvariant(), obj.Right?.ToLowerInvariant());
    }
}
