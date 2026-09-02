// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumWithFlagsAttributeFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class EnumWithFlagsAttributeTests
    {
        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_SimpleCaseAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""
                    {{attribute}}
                    public enum SimpleFlagsEnumClass
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Four = 4
                    }

                    {{attribute}}
                    public enum HexFlagsEnumClass
                    {
                        One = 0x1,
                        Two = 0x2,
                        Four = 0x4,
                        All = 0x7
                    }
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027CSharpResultAt(2, 13, "SimpleFlagsEnumClass"),
                GetCA1027CSharpResultAt(11, 13, "HexFlagsEnumClass"));

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumWithFlagsAttributes_SimpleCase_InternalAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""
                    {{attribute}}
                    internal enum SimpleFlagsEnumClass
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Four = 4
                    }

                    internal class OuterClass
                    {
                        {{attribute}}
                        public enum HexFlagsEnumClass
                        {
                            One = 0x1,
                            Two = 0x2,
                            Four = 0x4,
                            All = 0x7
                        }
                    }
                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_SimpleCaseWithScopeAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""
                    {{attribute}}
                    public enum {|CA1027:SimpleFlagsEnumClass|}
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Four = 4
                    }

                    {{attribute}}
                    public enum HexFlagsEnumClass
                    {
                        One = 0x1,
                        Two = 0x2,
                        Four = 0x4,
                        All = 0x7
                    }
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027CSharpResultAt(11, 13, "HexFlagsEnumClass"));
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_SimpleCaseAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""
                    {attribute}
                    Public Enum SimpleFlagsEnumClass
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    	Four = 4
                    End Enum

                    {attribute}
                    Public Enum HexFlagsEnumClass
                    	One = &H1
                    	Two = &H2
                    	Four = &H4
                    	All = &H7
                    End Enum
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027BasicResultAt(2, 13, "SimpleFlagsEnumClass"),
                GetCA1027BasicResultAt(10, 13, "HexFlagsEnumClass"));

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumWithFlagsAttributes_SimpleCase_InternalAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""
                    {attribute}
                    Friend Enum SimpleFlagsEnumClass
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    	Four = 4
                    End Enum

                    Friend Class OuterClass
                        {attribute}
                        Public Enum HexFlagsEnumClass
                    	    One = &H1
                    	    Two = &H2
                    	    Four = &H4
                    	    All = &H7
                        End Enum
                    End Class
                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_SimpleCaseWithScopeAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $$"""
                    {{attribute}}
                    Public Enum {|CA1027:SimpleFlagsEnumClass|}
                        Zero = 0
                        One = 1
                        Two = 2
                        Four = 4
                    End Enum

                    {{attribute}}
                    Public Enum HexFlagsEnumClass
                        One = &H1
                        Two = &H2
                        Four = &H4
                        All = &H7
                    End Enum
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027BasicResultAt(10, 13, "HexFlagsEnumClass"));
        }

        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_DuplicateValuesAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""
                    {{attribute}}
                    public enum DuplicateValuesEnumClass
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Four = 4,
                        AnotherFour = 4,
                        ThreePlusOne = Two + One + One
                    }
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027CSharpResultAt(2, 13, "DuplicateValuesEnumClass"));

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_DuplicateValuesAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""
                    {attribute}
                    Public Enum DuplicateValuesEnumClass
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    	Four = 4
                    	AnotherFour = 4
                    	ThreePlusOne = Two + One + One
                    End Enum
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027BasicResultAt(2, 13, "DuplicateValuesEnumClass"));

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_MissingPowerOfTwoAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""

                    {{attribute}}
                    public enum MissingPowerOfTwoEnumClass
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Four = 4,
                        Sixteen = 16
                    }

                    {{attribute}}
                    public enum MultipleMissingPowerOfTwoEnumClass
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Four = 4,
                        ThirtyTwo = 32
                    }
                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027CSharpResultAt(3, 13, "MissingPowerOfTwoEnumClass"),
                GetCA1027CSharpResultAt(13, 13, "MultipleMissingPowerOfTwoEnumClass"));

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_IncorrectNumbersAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""

                    {{attribute}}
                    public enum AnotherTestValue
                    {
                        Value1 = 0,
                        Value2 = 1,
                        Value3 = 1,
                        Value4 = 3
                    }
                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags,
                GetCA2217CSharpResultAt(3, 13, "AnotherTestValue", "2"));
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_MissingPowerOfTwoAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""

                    {attribute}
                    Public Enum MissingPowerOfTwoEnumClass
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    	Four = 4
                    	Sixteen = 16
                    End Enum

                    {attribute}
                    Public Enum MultipleMissingPowerOfTwoEnumClass
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    	Four = 4
                    	ThirtyTwo = 32
                    End Enum

                    """;
            }

            // Verify CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags,
                GetCA1027BasicResultAt(3, 13, "MissingPowerOfTwoEnumClass"),
                GetCA1027BasicResultAt(12, 13, "MultipleMissingPowerOfTwoEnumClass"));

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_IncorrectNumbersAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""

                    {attribute}
                    Public Enum AnotherTestValue
                    	Value1 = 0
                    	Value2 = 1
                    	Value3 = 1
                    	Value4 = 3
                    End Enum

                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags,
                GetCA2217BasicResultAt(3, 13, "AnotherTestValue", "2"));
        }

        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_ContiguousValuesAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""
                    {{attribute}}
                    public enum ContiguousEnumClass
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2
                    }

                    {{attribute}}
                    public enum ContiguousEnumClass2
                    {
                        Zero = 0,
                        One = 1,
                        Two = 2,
                        Three = 3,
                        Four = 4,
                        Five = 5
                    }

                    {{attribute}}
                    public enum ValuesNotDeclaredEnumClass
                    {
                        Zero,
                        One,
                        Two,
                        Three,
                        Four,
                        Five
                    }

                    {{attribute}}
                    public enum ShortUnderlyingType: short
                    {
                        Zero = 0,
                        One,
                        Two,
                        Three,
                        Four,
                        Five
                    }
                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_ContiguousValuesAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""
                    {attribute}
                    Public Enum ContiguousEnumClass
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    End Enum

                    {attribute}
                    Public Enum ContiguousEnumClass2
                    	Zero = 0
                    	One = 1
                    	Two = 2
                    	Three = 3
                    	Four = 4
                    	Five = 5
                    End Enum

                    {attribute}
                    Public Enum ValuesNotDeclaredEnumClass
                    	Zero
                    	One
                    	Two
                    	Three
                    	Four
                    	Five
                    End Enum

                    {attribute}
                    Public Enum ShortUnderlyingType As Short
                    	Zero = 0
                    	One
                    	Two
                    	Three
                    	Four
                    	Five
                    End Enum
                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod]
        public async Task CSharp_EnumWithFlagsAttributes_NonSimpleFlagsAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""

                    {{attribute}}
                    public enum NonSimpleFlagEnumClass
                    {
                        Zero = 0x0,      // 0000
                        One = 0x1,      // 0001
                        Two = 0x2,      // 0010
                        Eight = 0x8,    // 1000
                        Twelve = 0xC,   // 1100
                        HighValue = -1    // will be cast to UInt32.MaxValue, then zero-extended to UInt64
                    }

                    {{attribute}}
                    public enum BitValuesClass
                    {
                        None = 0x0,
                        One = 0x1,      // 0001
                        Two = 0x2,      // 0010
                        Eight = 0x8,    // 1000
                        Twelve = 0xC,   // 1100
                    }

                    {{attribute}}
                    public enum LabelsClass
                    {
                        None = 0,
                        One = 1,
                        Four = 4,
                        Six = 6,
                        Seven = 7
                    }
                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags,
                GetCA2217CSharpResultAt(3, 13, "NonSimpleFlagEnumClass", "4, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304, 8388608, 16777216, 33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 2147483648, 4294967296, 8589934592, 17179869184, 34359738368, 68719476736, 137438953472, 274877906944, 549755813888, 1099511627776, 2199023255552, 4398046511104, 8796093022208, 17592186044416, 35184372088832, 70368744177664, 140737488355328, 281474976710656, 562949953421312, 1125899906842624, 2251799813685248, 4503599627370496, 9007199254740992, 18014398509481984, 36028797018963968, 72057594037927936, 144115188075855872, 288230376151711744, 576460752303423488, 1152921504606846976, 2305843009213693952, 4611686018427387904, 9223372036854775808"),
                GetCA2217CSharpResultAt(14, 13, "BitValuesClass", "4"),
                GetCA2217CSharpResultAt(24, 13, "LabelsClass", "2"));
        }

        [TestMethod]
        public async Task VisualBasic_EnumWithFlagsAttributes_NonSimpleFlagsAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""

                    {attribute}
                    Public Enum NonSimpleFlagEnumClass
                    	Zero = &H0     ' 0000
                    	One = &H1      ' 0001
                    	Two = &H2      ' 0010
                    	Eight = &H8    ' 1000
                    	Twelve = &Hc   ' 1100
                    	HighValue = -1 ' will be cast to UInt32.MaxValue, then zero-extended to UInt64
                    End Enum

                    {attribute}
                    Public Enum BitValuesClass
                    	None = &H0
                    	One = &H1    ' 0001
                    	Two = &H2    ' 0010
                    	Eight = &H8  ' 1000
                    	Twelve = &Hc ' 1100
                    End Enum

                    {attribute}
                    Public Enum LabelsClass
                    	None = 0
                    	One = 1
                    	Four = 4
                    	Six = 6
                    	Seven = 7
                    End Enum

                    """;
            }

            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags,
                GetCA2217BasicResultAt(3, 13, "NonSimpleFlagEnumClass", "4, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304, 8388608, 16777216, 33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 2147483648, 4294967296, 8589934592, 17179869184, 34359738368, 68719476736, 137438953472, 274877906944, 549755813888, 1099511627776, 2199023255552, 4398046511104, 8796093022208, 17592186044416, 35184372088832, 70368744177664, 140737488355328, 281474976710656, 562949953421312, 1125899906842624, 2251799813685248, 4503599627370496, 9007199254740992, 18014398509481984, 36028797018963968, 72057594037927936, 144115188075855872, 288230376151711744, 576460752303423488, 1152921504606846976, 2305843009213693952, 4611686018427387904, 9223372036854775808"),
                GetCA2217BasicResultAt(13, 13, "BitValuesClass", "4"),
                GetCA2217BasicResultAt(22, 13, "LabelsClass", "2"));
        }

        [TestMethod, WorkItem(6982, "https://github.com/dotnet/roslyn-analyzers/issues/6982")]
        public async Task CSharp_EnumWithFlagsAttributes_MembersShareValueAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "[System.Flags]" : "";
                return $$"""
                    {{attribute}}
                    public enum MembersShareValueEnumClass
                    {
                        One,
                        Tow,
                        Three,
                        Two = Tow
                    }
                    """;
            }
            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyCS.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyCS.VerifyAnalyzerAsync(codeWithFlags);
        }

        [TestMethod, WorkItem(6982, "https://github.com/dotnet/roslyn-analyzers/issues/6982")]
        public async Task VisualBasic_EnumWithFlagsAttributes_MembersShareValueAsync()
        {
            static string CreateCode(bool hasFlags)
            {
                string attribute = hasFlags ? "<System.Flags>" : "";
                return $"""
                    {attribute}
                    Public Enum MembersShareValueEnumClass
                    	One
                    	Tow
                    	Three
                        Two = Tow
                    End Enum
                    """;
            }
            // Verify no CA1027: Mark enums with FlagsAttribute
            string codeWithoutFlags = CreateCode(hasFlags: false);
            await VerifyVB.VerifyAnalyzerAsync(codeWithoutFlags);

            // Verify no CA2217: Do not mark enums with FlagsAttribute
            string codeWithFlags = CreateCode(hasFlags: true);
            await VerifyVB.VerifyAnalyzerAsync(codeWithFlags);
        }

        private static DiagnosticResult GetCA1027CSharpResultAt(int line, int column, string enumTypeName)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(EnumWithFlagsAttributeAnalyzer.Rule1027)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(enumTypeName);

        private static DiagnosticResult GetCA1027BasicResultAt(int line, int column, string enumTypeName)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(EnumWithFlagsAttributeAnalyzer.Rule1027)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(enumTypeName);

        private static DiagnosticResult GetCA2217CSharpResultAt(int line, int column, string enumTypeName, string missingValuesString)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(EnumWithFlagsAttributeAnalyzer.Rule2217)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(enumTypeName, missingValuesString);

        private static DiagnosticResult GetCA2217BasicResultAt(int line, int column, string enumTypeName, string missingValuesString)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(EnumWithFlagsAttributeAnalyzer.Rule2217)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(enumTypeName, missingValuesString);
    }
}
