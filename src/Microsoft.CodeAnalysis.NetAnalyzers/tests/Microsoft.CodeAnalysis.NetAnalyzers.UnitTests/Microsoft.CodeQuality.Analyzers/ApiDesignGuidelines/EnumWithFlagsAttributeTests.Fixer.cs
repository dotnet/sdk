// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumWithFlagsAttributeFixerTests
    {
        [Fact]
        public async Task CSharp_EnumWithFlagsAttributes_SimpleCaseAsync()
        {
            var code = @"
public enum {|CA1027:SimpleFlagsEnumClass|}
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4
}

public enum {|CA1027:HexFlagsEnumClass|}
{
    One = 0x1,
    Two = 0x2,
    Four = 0x4,
    All = 0x7
}";

            var expected = @"
[System.Flags]
public enum SimpleFlagsEnumClass
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4
}

[System.Flags]
public enum HexFlagsEnumClass
{
    One = 0x1,
    Two = 0x2,
    Four = 0x4,
    All = 0x7
}";

            // Verify fixes for CA1027
            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task VisualBasic_EnumWithFlagsAttributes_SimpleCaseAsync()
        {
            var code = @"
Public Enum {|CA1027:SimpleFlagsEnumClass|}
    Zero = 0
    One = 1
    Two = 2
    Four = 4
End Enum

Public Enum {|CA1027:HexFlagsEnumClass|}
    One = &H1
    Two = &H2
    Four = &H4
    All = &H7
End Enum";

            var expected = @"
<System.Flags>
Public Enum SimpleFlagsEnumClass
    Zero = 0
    One = 1
    Two = 2
    Four = 4
End Enum

<System.Flags>
Public Enum HexFlagsEnumClass
    One = &H1
    Two = &H2
    Four = &H4
    All = &H7
End Enum";

            // Verify fixes for CA1027
            await VerifyVB.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task CSharp_EnumWithFlagsAttributes_DuplicateValuesAsync()
        {
            string code = @"
public enum {|CA1027:DuplicateValuesEnumClass|}
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4,
    AnotherFour = 4,
    ThreePlusOne = Two + One + One
}
";

            string expected = @"
[System.Flags]
public enum DuplicateValuesEnumClass
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4,
    AnotherFour = 4,
    ThreePlusOne = Two + One + One
}
";

            // Verify fixes for CA1027
            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task VisualBasic_EnumWithFlagsAttributes_DuplicateValuesAsync()
        {
            string code = @"
Public Enum {|CA1027:DuplicateValuesEnumClass|}
    Zero = 0
    One = 1
    Two = 2
    Four = 4
    AnotherFour = 4
    ThreePlusOne = Two + One + One
End Enum
";

            string expected = @"
<System.Flags>
Public Enum DuplicateValuesEnumClass
    Zero = 0
    One = 1
    Two = 2
    Four = 4
    AnotherFour = 4
    ThreePlusOne = Two + One + One
End Enum
";

            // Verify fixes for CA1027
            await VerifyVB.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task CSharp_EnumWithFlagsAttributes_MissingPowerOfTwoAsync()
        {
            string code = @"
public enum {|CA1027:MissingPowerOfTwoEnumClass|}
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4,
    Sixteen = 16
}

public enum {|CA1027:MultipleMissingPowerOfTwoEnumClass|}
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4,
    ThirtyTwo = 32
}";

            var expected = @"
[System.Flags]
public enum MissingPowerOfTwoEnumClass
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4,
    Sixteen = 16
}

[System.Flags]
public enum MultipleMissingPowerOfTwoEnumClass
{
    Zero = 0,
    One = 1,
    Two = 2,
    Four = 4,
    ThirtyTwo = 32
}";

            // Verify fixes for CA1027
            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task CSharp_EnumWithFlagsAttributes_IncorrectNumbersAsync()
        {
            string code = @"
[System.Flags]
public enum {|CA2217:AnotherTestValue|}
{
    Value1 = 0,
    Value2 = 1,
    Value3 = 1,
    Value4 = 3
}";

            var expected = @"
public enum AnotherTestValue
{
    Value1 = 0,
    Value2 = 1,
    Value3 = 1,
    Value4 = 3
}";

            // Verify fixes for CA2217
            await VerifyCS.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task VisualBasic_EnumWithFlagsAttributes_MissingPowerOfTwoAsync()
        {
            string code = @"
Public Enum {|CA1027:MissingPowerOfTwoEnumClass|}
	Zero = 0
	One = 1
	Two = 2
	Four = 4
	Sixteen = 16
End Enum

Public Enum {|CA1027:MultipleMissingPowerOfTwoEnumClass|}
	Zero = 0
	One = 1
	Two = 2
	Four = 4
	ThirtyTwo = 32
End Enum
";

            string expected = @"
<System.Flags>
Public Enum MissingPowerOfTwoEnumClass
	Zero = 0
	One = 1
	Two = 2
	Four = 4
	Sixteen = 16
End Enum

<System.Flags>
Public Enum MultipleMissingPowerOfTwoEnumClass
	Zero = 0
	One = 1
	Two = 2
	Four = 4
	ThirtyTwo = 32
End Enum
";

            // Verify fixes for CA1027
            await VerifyVB.VerifyCodeFixAsync(code, expected);
        }

        [Fact]
        public async Task VisualBasic_EnumWithFlagsAttributes_IncorrectNumberAsync()
        {
            string code = @"
<System.Flags>
Public Enum {|CA2217:AnotherTestValue|}
	Value1 = 0
	Value2 = 1
	Value3 = 1
	Value4 = 3
End Enum
";

            string expected = @"
Public Enum AnotherTestValue
	Value1 = 0
	Value2 = 1
	Value3 = 1
	Value4 = 3
End Enum
";

            // Verify fixes for CA2217
            await VerifyVB.VerifyCodeFixAsync(code, expected);
        }
    }
}
