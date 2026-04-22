// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
