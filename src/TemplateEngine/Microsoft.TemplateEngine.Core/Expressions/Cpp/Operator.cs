// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Expressions.Cpp
{
    internal enum Operator
    {
        None,
        And,
        Or,
        Xor,
        Not,
        GreaterThan,
        GreaterThanOrEqualTo,
        LessThan,
        LessThanOrEqualTo,
        EqualTo,
        NotEqualTo,
        BitwiseAnd,
        BitwiseOr,
        LeftShift,
        RightShift
    }
}
