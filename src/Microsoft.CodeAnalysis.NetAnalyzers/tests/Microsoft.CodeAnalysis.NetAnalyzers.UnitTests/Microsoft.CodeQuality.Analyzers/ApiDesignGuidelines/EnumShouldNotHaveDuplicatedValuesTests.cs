// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumShouldNotHaveDuplicatedValues,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumShouldNotHaveDuplicatedValues,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.ApiDesignGuidelines
{
    public class EnumShouldNotHaveDuplicatedValuesTests
    {
        [Fact]
        public async Task EnumNoDuplication_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
End Enum");
        }

        [Fact]
        public async Task EnumExplicitDuplication_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 1,
}",
                GetCSharpResultAt(5, 5, "Value2", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 1
End Enum",
                GetBasicResultAt(4, 5, "Value2", "1", "Value1"));
        }

        [Fact]
        public async Task AllEnumTypesExplicitDuplication_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyByteEnum : byte
{
    Value1 = 1,
    Value2 = 1,
}

public enum MySByteEnum : sbyte
{
    Value1 = 1,
    Value2 = 1,
}

public enum MyShortEnum : short
{
    Value1 = 1,
    Value2 = 1,
}

public enum MyUShortEnum : ushort
{
    Value1 = 1,
    Value2 = 1,
}

public enum MyIntEnum : int
{
    Value1 = 1,
    Value2 = 1,
}

public enum MyUIntEnum : uint
{
    Value1 = 1,
    Value2 = 1,
}

public enum MyLongEnum : long
{
    Value1 = 1,
    Value2 = 1,
}

public enum MyULongEnum : ulong
{
    Value1 = 1,
    Value2 = 1,
}",
                GetCSharpResultAt(5, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(11, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(17, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(23, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(29, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(35, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(41, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(47, 5, "Value2", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyByteEnum As Byte
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MySByteEnum As SByte
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MyShortEnum As Short
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MyUShortEnum As UShort
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MyIntEnum As Integer
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MyUIntEnum As UInteger
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MyLongEnum As Long
    Value1 = 1
    Value2 = 1
End Enum

Public Enum MyULongEnum As ULong
    Value1 = 1
    Value2 = 1
End Enum
",
                GetBasicResultAt(4, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(9, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(14, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(19, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(24, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(29, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(34, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(39, 5, "Value2", "1", "Value1"));
        }

        [Fact]
        public async Task EnumExplicitBitwiseShiftDuplication_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    None = 0x0,
    Value1 = 0x1,
    Value2 = 0x1,
    Value3 = 0x4,
}",
                GetCSharpResultAt(6, 5, "Value2", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    None = &H0
    Value1 = &H1
    Value2 = &H1
    Value3 = &H4
End Enum",
                GetBasicResultAt(5, 5, "Value2", "1", "Value1"));
        }

        [Fact]
        public async Task EnumExplicitDuplicationNotConsecutive_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = 1,
}",
                GetCSharpResultAt(6, 5, "Value3", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = 1
End Enum",
                GetBasicResultAt(5, 5, "Value3", "1", "Value1"));
        }

        [Fact]
        public async Task EnumImplicitDuplication_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2,
    Value3 = 2,
}",
                GetCSharpResultAt(6, 5, "Value3", "2", "Value2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2
    Value3 = 2
End Enum",
                GetBasicResultAt(5, 5, "Value3", "2", "Value2"));
        }

        [Fact]
        public async Task EnumValueReference_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = Value1,
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = Value1
End Enum");
        }

        [Fact]
        public async Task EnumDuplicatedBitwiseValueReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = Value1 | Value1,
}",
                GetCSharpResultAt(6, 5, "Value3", "1", "Value1"),
                GetCSharpBitwiseResultAt(6, 23, "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = Value1 Or Value1
End Enum",
                GetBasicResultAt(5, 5, "Value3", "1", "Value1"),
                GetBasicBitwiseResultAt(5, 24, "Value1"));
        }

        [Fact]
        public async Task EnumDuplicatedBitwiseValueReferenceComplex_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = 3,
    Value4 = Value1 | Value2 | Value1,
}",
                GetCSharpResultAt(7, 5, "Value4", "3", "Value3"),
                GetCSharpBitwiseResultAt(7, 32, "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = 3
    Value4 = Value1 Or Value2 Or Value1
End Enum",
                GetBasicResultAt(6, 5, "Value4", "3", "Value3"),
                GetBasicBitwiseResultAt(6, 34, "Value1"));
        }

        [Fact]
        public async Task EnumDuplicatedBitwiseValueReferenceAndValue_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = Value1 | 1,
}",
                GetCSharpResultAt(6, 5, "Value3", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = Value1 Or 1
End Enum",
                GetBasicResultAt(5, 5, "Value3", "1", "Value1"));
        }

        [Fact]
        public async Task EnumDuplicatedValueMaths_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2 - 1,
    Value3 = Value1 * Value2,
}",
                GetCSharpResultAt(5, 5, "Value2", "1", "Value1"),
                GetCSharpResultAt(6, 5, "Value3", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2 - 1
    Value3 = Value1 * Value2
End Enum",
                GetBasicResultAt(4, 5, "Value2", "1", "Value1"),
                GetBasicResultAt(5, 5, "Value3", "1", "Value1"));
        }

        [Fact]
        public async Task EnumValueMaths_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 1 + 1,
    Value3 = (Value1 + 1) * Value2,
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 1 + 1
    Value3 = (Value1 + 1) * Value2
End Enum");
        }

        [Fact]
        public async Task EnumComplexBitwiseParts_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = (Value1 + Value2) | (Value1 + Value2),
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = (Value1 + Value2) Or (Value1 + Value2)
End Enum");
        }

        [Fact]
        public async Task EnumBitwisePartsInAddition_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum
{
    Value1 = 1,
    Value2 = 2,
    Value3 = (Value1 | Value2) + (Value1 | Value2),
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum
    Value1 = 1
    Value2 = 2
    Value3 = (Value1 Or Value2) + (Value1 Or Value2)
End Enum");
        }

        [Fact]
        public async Task EnumMemberReferencesAnotherEnum()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum MyEnum1
{
    Value1 = 1,
    Value2 = 2
}

public enum MyEnum2
{
    Value1 = 1,
    Value2 = MyEnum1.Value1
}",
                GetCSharpResultAt(11, 5, "Value2", "1", "Value1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum MyEnum1
    Value1 = 1
    Value2 = 2
End Enum

Public Enum MyEnum2
    Value1 = 1
    Value2 = MyEnum1.Value1
End Enum",
                GetBasicResultAt(9, 5, "Value2", "1", "Value1"));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string fieldName, string constantValue, string duplicatedFieldName) =>
            VerifyCS.Diagnostic(EnumShouldNotHaveDuplicatedValues.RuleDuplicatedValue)
                .WithLocation(line, column)
                .WithArguments(fieldName, constantValue, duplicatedFieldName);

        private static DiagnosticResult GetCSharpBitwiseResultAt(int line, int column, string duplicatedFieldName) =>
            VerifyCS.Diagnostic(EnumShouldNotHaveDuplicatedValues.RuleDuplicatedBitwiseValuePart)
                .WithLocation(line, column)
                .WithArguments(duplicatedFieldName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string fieldName, string constantValue, string duplicatedFieldName) =>
            VerifyVB.Diagnostic(EnumShouldNotHaveDuplicatedValues.RuleDuplicatedValue)
                .WithLocation(line, column)
                .WithArguments(fieldName, constantValue, duplicatedFieldName);

        private static DiagnosticResult GetBasicBitwiseResultAt(int line, int column, string duplicatedFieldName) =>
            VerifyVB.Diagnostic(EnumShouldNotHaveDuplicatedValues.RuleDuplicatedBitwiseValuePart)
                .WithLocation(line, column)
                .WithArguments(duplicatedFieldName);
    }
}
