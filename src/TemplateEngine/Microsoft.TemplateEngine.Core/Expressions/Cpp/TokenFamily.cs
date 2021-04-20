// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Core.Expressions.Cpp
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
        QuotedLiteral,
        SingleQuotedLiteral,
        Literal,
        Reference = 0x40000000
    }
}
