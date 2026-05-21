// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Analyzer.Utilities.FlowAnalysis.Analysis.PropertySetAnalysis
{
    internal enum PropertySetAbstractValueKind
    {
        /// <summary>
        /// Doesn't matter.
        /// </summary>
        Unknown,

        /// <summary>
        /// Not flagged for badness.
        /// </summary>
        Unflagged,

        /// <summary>
        /// Flagged for badness.
        /// </summary>
        Flagged,

        /// <summary>
        /// Maybe flagged.
        /// </summary>
        MaybeFlagged,
    }
}
