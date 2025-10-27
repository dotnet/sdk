// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    public partial class UseCrossPlatformIntrinsicsAnalyzer
    {
        public enum RuleKind
        {
            // These names match the underlying IL names or method names for the cross-platform API that will be used in the fixer.

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

            // Named methods (not operators)
            Abs,
            AndNot,              // For ARM BitwiseClear - direct parameter mapping
            AndNot_Swapped,      // For x86/x64 AndNot - needs parameter swap
            Ceiling,
            ConditionalSelect,
            Floor,
            FusedMultiplyAdd,
            Max,
            MaxNative,           // For x86/x64 Max - different NaN/negative zero handling
            Min,
            MinNative,           // For x86/x64 Min - different NaN/negative zero handling
            Negate,
            Round,
            Sqrt,
            Truncate,

            Count,
        }
    }
}
