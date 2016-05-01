using System;

namespace Mutant.Chicken.Core.Expressions.Cpp
{
    [Flags]
    internal enum TokenFamily
    {
        And,
        Or,
        Xor,
        Not,
        GreaterThan,
        GreaterThanOrEqualTo,
        LessThan,
        LessThanOrEqualTo,
        EqualTo,
        EqualToShort,
        NotEqualTo,
        BitwiseAnd,
        BitwiseOr,
        LeftShift,
        RightShift,
        OpenBrace,
        CloseBrace,
        Whitespace,
        Tab,
        WindowsEOL,
        UnixEOL,
        LegacyMacEOL,
        Literal,
        Reference = 0x40000000
    }
}