// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideGetHashCodeOnOverridingEqualsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            throw new NotSupportedException("CA2218 is not applied to C# since it already reports CS0661");
        }

        [Fact]
        public void Good_Class_Equals()
        {
            VerifyBasic(@"
Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return 0
    End Function
End Class");
        }

        [Fact]
        public void Good_Class_NoEquals()
        {
            VerifyBasic(@"
Class C
End Class");
        }

        [Fact]
        public void Good_Structure_Equals()
        {
            VerifyBasic(@"
Structure C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return 0
    End Function
End Structure");
        }

        [Fact]
        public void Good_Structure_NoEquals()
        {
            VerifyBasic(@"
Structure C
End Structure");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/7305")]
        public void Ignored_Interace()
        {
            VerifyBasic(@"
Interace I
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Interface");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/7305")]
        public void Ignored_TopLevel()
        {
            VerifyBasic(@"
Public Overrides Function Equals(o As Object) As Boolean
    Return True
End Function");
        }

        [Fact]
        public void Bad_Class()
        {
            VerifyBasic(@"
Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class",
            // Test0.vb(2,7): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(2, 7, BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer.Rule));
        }

        [Fact]
        public void Bad_Structure()
        {
            VerifyBasic(@"
Structure C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Structure",
            // Test0.vb(2,11): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(2, 11, BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer.Rule));
        }

        [Fact]
        public void Bad_NotOverride()
        {
            VerifyBasic(@"
Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Shadows Function GetHashCode() As Integer
        Return 0
    End Function
End Class",
            // Test0.vb(2,7): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(2, 7, BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer.Rule));
        }

        [Fact]
        public void Bad_FalseOverride()
        {
            VerifyBasic(@"
Class Base
    Public Overridable Shadows Function GetHashCode() As Integer
        Return 0
    End Function
End Class

Class Derived : Inherits Base
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return 0
    End Function
End Class",
            // Test0.vb(8,7): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(8, 7, BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer.Rule));
        }
    }
}