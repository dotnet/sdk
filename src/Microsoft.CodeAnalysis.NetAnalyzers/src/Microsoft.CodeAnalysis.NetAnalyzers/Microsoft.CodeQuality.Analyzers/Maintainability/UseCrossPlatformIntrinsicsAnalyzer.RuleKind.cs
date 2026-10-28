// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    public partial class UseCrossPlatformIntrinsicsAnalyzer
    {
        public enum RuleKind
        {
            // These names match the underlying IL names for the cross-platform API that will be used in the fixer.

            op_Addition,
            op_BitwiseAnd,
            op_BitwiseOr,
            op_Division,
            op_ExclusiveOr,
            op_LeftShift,
            op_Multiply,
            op_OnesComplement,
            op_RightShift,
            op_Subtraction,
            op_UnaryNegation,
            op_UnsignedRightShift,

            Count,
        }
    }
}
