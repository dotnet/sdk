// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public enum Operators
    {
        None = 0,
        And = 41,
        Or = 42,
        Xor = 39,
        Not = 2, //Unary, gets precedence
        GreaterThan = 33,
        GreaterThanOrEqualTo = 35,
        LessThan = 32,
        LessThanOrEqualTo = 34,
        EqualTo = 36,
        NotEqualTo = 37,
        BadSyntax = -1,
        Identity = 1, //Equiv to "scope resolution"
        LeftShift = 30,
        RightShift = 31,
        BitwiseAnd = 38,
        BitwiseOr = 40,
        Add = 28,
        Subtract = 29,
        Multiply = 25,
        Divide = 26,
        Modulus = 27,
        Exponentiate = 24
    }
}
