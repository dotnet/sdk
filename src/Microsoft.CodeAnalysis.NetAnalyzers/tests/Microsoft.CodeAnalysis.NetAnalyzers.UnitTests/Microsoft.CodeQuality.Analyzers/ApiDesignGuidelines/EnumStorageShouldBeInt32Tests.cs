// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumStorageShouldBeInt32Analyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpEnumStorageShouldBeInt32Fixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumStorageShouldBeInt32Analyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicEnumStorageShouldBeInt32Fixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumStorageShouldBeInt32Tests
    {
        #region CSharpUnitTests

        [Fact]
        public async Task CSharp_CA1028_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
namespace Test
{
    public enum TestEnum1 //no violation - because underlying type is Int32
    {
        Value1 = 1,
        Value2 = 2
    }
    public static class OuterClass
    {
        [Flags]
        public enum TestEnum2 : long //no violation - because underlying type is Int64 and has Flag attributes
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = Value1 | Value2
        }
        private enum TestEnum3 : byte //no violation - because accessibility is private 
        {
            Value1 = 1,
            Value2 = 2
        }
        internal class innerClass
        {
            public enum TestEnum4 : long //no violation - because resultant accessibility is private 
            {
                Value1 = 1,
                Value2 = 2
            }
        }
    }
}
 ");
        }

        [Fact]
        public async Task CSharp_CA1028_DiagnosticForInt64WithNoFlags()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
namespace Test
{
    public enum TestEnum1 : long // violation - because underlying type is Int64 and has no Flags attribute
    {
        Value1 = 1,
        Value2 = 2
    }
}
",
            GetCSharpResultAt(5, 17, EnumStorageShouldBeInt32Analyzer.Rule, "TestEnum1", "long"));
        }

        [Fact]
        public async Task CSharp_CA1028_DiagnosticForSByte()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
namespace Test
{
    public enum TestEnum2 : sbyte // violation - because underlying type is not Int32
    {
        Value1 = 1,
        Value2 = 2
    }
}
",
            GetCSharpResultAt(5, 17, EnumStorageShouldBeInt32Analyzer.Rule, "TestEnum2", "sbyte"));
        }

        [Fact]
        public async Task CSharp_CA1028_DiagnosticForUShort()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
namespace Test
{
    public enum TestEnum3 : ushort // violation - because underlying type is not Int32
    {
        Value1 = 1,
        Value2 = 2
    }
}
",
            GetCSharpResultAt(5, 17, EnumStorageShouldBeInt32Analyzer.Rule, "TestEnum3", "ushort"));
        }
        #endregion

        #region BasicUnitTests

        [Fact]
        public async Task Basic_CA1028_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Module Module1
    Public Enum TestEnum1 'no violation - because underlying type is Int32
        Value1 = 1
        Value2 = 2
    End Enum
    Public Class OuterClass
        <Flags()>
        Public Enum TestEnum2 As Long 'no violation - because underlying type is Int64 and has Flag attributes
            Value1 = 1
            Value2 = 2
            Value3 = Value1 Or Value2
        End Enum
        Private Enum TestEnum3 As Byte 'no violation - because accessibility Is private 
            Value1 = 1
            Value2 = 2
        End Enum
        Private Class innerClass
            Public Enum TestEnum4 As Long 'no violation - because resultant accessibility Is private 
                Value1 = 1
                Value2 = 2
            End Enum
        End Class
    End Class
End Module
 ");
        }

        [Fact]
        public async Task Basic_CA1028_DiagnosticForInt64WithNoFlags()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Module Module1
    Public Enum TestEnum1 As Long 'violation - because underlying type is Int64 and has no Flags attribute
        Value1 = 1
        Value2 = 2
    End Enum
End Module
",
            GetBasicResultAt(4, 17, EnumStorageShouldBeInt32Analyzer.Rule, "TestEnum1", "Long"));
        }

        [Fact]
        public async Task Basic_CA1028_DiagnosticForByte()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Module Module1
    Public Enum TestEnum2 As Byte 'violation - because underlying type is not Int32
        Value1 = 1
        Value2 = 2
    End Enum
End Module
",
            GetBasicResultAt(4, 17, EnumStorageShouldBeInt32Analyzer.Rule, "TestEnum2", "Byte"));
        }

        [Fact]
        public async Task Basic_CA1028_DiagnosticForUShort()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Module Module1
    Public Enum TestEnum3 As UShort 'violation - because underlying type is not Int32
        Value1 = 1
        Value2 = 2
    End Enum
End Module
",
            GetBasicResultAt(4, 17, EnumStorageShouldBeInt32Analyzer.Rule, "TestEnum3", "UShort"));
        }
        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
