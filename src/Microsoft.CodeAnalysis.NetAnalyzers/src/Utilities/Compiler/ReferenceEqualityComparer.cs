// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Analyzer.Utilities
{
    /// <summary>
    /// Compares objects based upon their reference identity.
    /// </summary>
    internal class ReferenceEqualityComparer : IEqualityComparer<object?>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        private ReferenceEqualityComparer()
        {
        }

        bool IEqualityComparer<object?>.Equals(object? a, object? b)
        {
            return a == b;
        }

        int IEqualityComparer<object?>.GetHashCode(object? a)
        {
            return ReferenceEqualityComparer.GetHashCode(a);
        }

        public static int GetHashCode(object? a)
        {
            return RuntimeHelpers.GetHashCode(a);
        }
    }
}
