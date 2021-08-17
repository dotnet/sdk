// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumsShouldHaveZeroValueAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpEnumsShouldHaveZeroValueFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumsShouldHaveZeroValueAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicEnumsShouldHaveZeroValueFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumsShouldHaveZeroValueFixerTests
    {
        [Fact]
        public async Task CSharp_EnumsShouldZeroValueFlagsRename()
        {
            var code = @"
public class Outer
{
    [System.Flags]
    public enum E
    {
        A = 0,
        B = 3
    }
}

[System.Flags]
public enum E2
{
    A2 = 0,
    B2 = 1
}

[System.Flags]
public enum E3
{
    A3 = (ushort)0,
    B3 = (ushort)1
}

[System.Flags]
public enum E4
{
    A4 = 0,
    B4 = (int)2  // Sample comment
}

[System.Flags]
public enum NoZeroValuedField
{
    A5 = 1,
    B5 = 2
}";

            var expectedFixedCode = @"
public class Outer
{
    [System.Flags]
    public enum E
    {
        None = 0,
        B = 3
    }
}

[System.Flags]
public enum E2
{
    None = 0,
    B2 = 1
}

[System.Flags]
public enum E3
{
    None = (ushort)0,
    B3 = (ushort)1
}

[System.Flags]
public enum E4
{
    None = 0,
    B4 = (int)2  // Sample comment
}

[System.Flags]
public enum NoZeroValuedField
{
    A5 = 1,
    B5 = 2
}";
            await VerifyCS.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(7, 9, 7, 10).WithArguments("E", "A"),
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(15, 5, 15, 7).WithArguments("E2", "A2"),
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(22, 5, 22, 7).WithArguments("E3", "A3"),
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(29, 5, 29, 7).WithArguments("E4", "A4"),
                },
                expectedFixedCode);
        }

        [Fact]
        public async Task CSharp_EnumsShouldZeroValueFlagsMultipleZero()
        {
            var code = @"// Some comment
public class Outer
{
    [System.Flags]
    public enum E
    {
        None = 0,
        A = 0
    }
}
// Some comment
[System.Flags]
public enum E2
{
    None = 0,
    A = None
}";
            var expectedFixedCode = @"// Some comment
public class Outer
{
    [System.Flags]
    public enum E
    {
        None = 0
    }
}
// Some comment
[System.Flags]
public enum E2
{
    None = 0
}";
            await VerifyCS.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero).WithSpan(5, 17, 5, 18).WithArguments("E"),
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero).WithSpan(13, 13, 13, 15).WithArguments("E2"),
                },
                expectedFixedCode);
        }

        [Fact]
        public async Task CSharp_EnumsShouldZeroValueNotFlagsNoZeroValue()
        {
            var code = @"
public class Outer
{
    public enum E
    {
        A = 1
    }

    public enum E2
    {
        None = 1,
        A = 2
    }
}

public enum E3
{
    None = 0,
    A = 1
}

public enum E4
{
    None = 0,
    A = 0
}
";

            var expectedFixedCode = @"
public class Outer
{
    public enum E
    {
        None,
        A = 1
    }

    public enum E2
    {
        None,
        A = 2
    }
}

public enum E3
{
    None = 0,
    A = 1
}

public enum E4
{
    None = 0,
    A = 0
}
";
            await VerifyCS.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero).WithSpan(4, 17, 4, 18).WithArguments("E"),
                    VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero).WithSpan(9, 17, 9, 19).WithArguments("E2"),
                },
                expectedFixedCode);
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename()
        {
            var code = @"
Public Class Outer
    <System.Flags>
    Public Enum E
        A = 0
        B = 1
    End Enum
End Class

<System.Flags>
Public Enum E2
    A2 = 0
    B2 = 1
End Enum

<System.Flags>
Public Enum E3
    A3 = CUShort(0)
    B3 = CUShort(1)
End Enum

<System.Flags>
Public Enum NoZeroValuedField
    A5 = 1
    B5 = 2
End Enum
";

            var expectedFixedCode = @"
Public Class Outer
    <System.Flags>
    Public Enum E
        None = 0
        B = 1
    End Enum
End Class

<System.Flags>
Public Enum E2
    None = 0
    B2 = 1
End Enum

<System.Flags>
Public Enum E3
    None = CUShort(0)
    B3 = CUShort(1)
End Enum

<System.Flags>
Public Enum NoZeroValuedField
    A5 = 1
    B5 = 2
End Enum
";
            await VerifyVB.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(5, 9, 5, 10).WithArguments("E", "A"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(12, 5, 12, 7).WithArguments("E2", "A2"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(18, 5, 18, 7).WithArguments("E3", "A3"),
                },
                expectedFixedCode);
        }

        [WorkItem(836193, "DevDiv")]
        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename_AttributeListHasTrivia()
        {
            var code = @"
Public Class Outer
    <System.Flags> _
    Public Enum E
        A = 0
        B = 1
    End Enum
End Class

<System.Flags> _
Public Enum E2
    A2 = 0
    B2 = 1
End Enum

<System.Flags> _
Public Enum E3
    A3 = CUShort(0)
    B3 = CUShort(1)
End Enum

<System.Flags> _
Public Enum NoZeroValuedField
    A5 = 1
    B5 = 2
End Enum
";

            var expectedFixedCode = @"
Public Class Outer
    <System.Flags> _
    Public Enum E
        None = 0
        B = 1
    End Enum
End Class

<System.Flags> _
Public Enum E2
    None = 0
    B2 = 1
End Enum

<System.Flags> _
Public Enum E3
    None = CUShort(0)
    B3 = CUShort(1)
End Enum

<System.Flags> _
Public Enum NoZeroValuedField
    A5 = 1
    B5 = 2
End Enum
";
            await VerifyVB.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(5, 9, 5, 10).WithArguments("E", "A"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(12, 5, 12, 7).WithArguments("E2", "A2"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename).WithSpan(18, 5, 18, 7).WithArguments("E3", "A3"),
                },
                expectedFixedCode);
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsMultipleZero()
        {
            var code = @"
Public Class Outer
    <System.Flags>
    Public Enum E
        None = 0
        A = 0
    End Enum
End Class

<System.Flags>
Public Enum E2
    None = 0
    A = None
End Enum

<System.Flags>
Public Enum E3
    A3 = 0
    B3 = CUInt(0)  ' Not a constant
End Enum";

            var expectedFixedCode = @"
Public Class Outer
    <System.Flags>
    Public Enum E
        None = 0
    End Enum
End Class

<System.Flags>
Public Enum E2
    None = 0
End Enum

<System.Flags>
Public Enum E3
    None
End Enum";

            await VerifyVB.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero).WithSpan(4, 17, 4, 18).WithArguments("E"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero).WithSpan(11, 13, 11, 15).WithArguments("E2"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero).WithSpan(17, 13, 17, 15).WithArguments("E3"),
                },
                expectedFixedCode);
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValue()
        {
            var code = @"
Public Class C
    Public Enum E
        A = 1
    End Enum

    Public Enum E2
        None = 1
        A = 2
    End Enum
End Class

Public Enum E3
    None = 0
    A = 1
End Enum

Public Enum E4
    None = 0
    A = 0
End Enum
";

            var expectedFixedCode = @"
Public Class C
    Public Enum E
        None
        A = 1
    End Enum

    Public Enum E2
        None
        A = 2
    End Enum
End Class

Public Enum E3
    None = 0
    A = 1
End Enum

Public Enum E4
    None = 0
    A = 0
End Enum
";
            await VerifyVB.VerifyCodeFixAsync(
                code,
                new[]
                {
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero).WithSpan(3, 17, 3, 18).WithArguments("E"),
                    VerifyVB.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero).WithSpan(7, 17, 7, 19).WithArguments("E2"),
                },
                expectedFixedCode);
        }
    }
}
