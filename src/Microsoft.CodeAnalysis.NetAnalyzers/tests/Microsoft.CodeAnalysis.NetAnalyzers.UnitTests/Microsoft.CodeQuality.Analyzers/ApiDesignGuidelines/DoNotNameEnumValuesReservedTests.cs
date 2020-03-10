// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotNameEnumValuesReserved,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotNameEnumValuesReserved,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotNameEnumValuesReservedTests
    {
        [Fact]
        public async Task CA1700_NameContainsReserved_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum Enum1
{
    Reserved,
    SomethingReserved,
    ReservedSuffix,
}",
                GetCSharpResultAt(4, 5, "Enum1", "Reserved"),
                GetCSharpResultAt(5, 5, "Enum1", "SomethingReserved"),
                GetCSharpResultAt(6, 5, "Enum1", "ReservedSuffix"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum Enum1
    Reserved
    SomethingReserved
    ReservedSuffix
End Enum",
                GetBasicResultAt(3, 5, "Enum1", "Reserved"),
                GetBasicResultAt(4, 5, "Enum1", "SomethingReserved"),
                GetBasicResultAt(5, 5, "Enum1", "ReservedSuffix"));
        }

        [Fact]
        public async Task CA1700_NameContainsReservedWithoutCorrectCase_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum Enum1
{
    reserved,
    RESERVED,
    Somethingreserved,
    ReserveDSuffix,
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum Enum1
    reserved
    Somethingreserved
    ReserveDSuffix
End Enum");
        }

        [Fact]
        public async Task CA1700_EnumIsNotPublicAndNameContainsReserved_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal enum Enum1
{
    Reserved,
    SomethingReserved,
    ReservedSuffix,
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Enum Enum1
    Reserved
    SomethingReserved
    ReservedSuffix
End Enum");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string className, string memberName)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(className, memberName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string className, string memberName)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(className, memberName);
    }
}
