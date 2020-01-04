// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumStorageShouldBeInt32Tests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new EnumStorageShouldBeInt32Analyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EnumStorageShouldBeInt32Analyzer();
        }

        #region CSharpUnitTests

        [Fact]
        public void CSharp_CA1028_NoDiagnostic()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA1028_DiagnosticForInt64WithNoFlags()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA1028_DiagnosticForSByte()
        {
            VerifyCSharp(@"
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
        public void CSharp_CA1028_DiagnosticForUShort()
        {
            VerifyCSharp(@"
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
        public void Basic_CA1028_NoDiagnostic()
        {
            VerifyBasic(@"
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
        public void Basic_CA1028_DiagnosticForInt64WithNoFlags()
        {
            VerifyBasic(@"
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
        public void Basic_CA1028_DiagnosticForByte()
        {
            VerifyBasic(@"
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
        public void Basic_CA1028_DiagnosticForUShort()
        {
            VerifyBasic(@"
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
    }
}
