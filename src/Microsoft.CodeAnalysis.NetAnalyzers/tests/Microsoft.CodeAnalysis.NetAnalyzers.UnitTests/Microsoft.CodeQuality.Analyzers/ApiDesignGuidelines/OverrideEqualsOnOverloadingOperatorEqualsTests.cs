// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideEqualsOnOverloadingOperatorEqualsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            throw new NotSupportedException("CA2224 is not applied to C# since it already reports CS0660");
        }

        [Fact]
        public void Good_Class_Operator()
        {
            VerifyBasic(@"
Class C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator

    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator

    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class");
        }

        [Fact]
        public void Good_Class_NoOperator()
        {
            VerifyBasic(@"
Class C
End Class");
        }

        [Fact]
        public void Good_Structure_Operator()
        {
            VerifyBasic(@"
Structure C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator

    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator

    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Structure");
        }

        [Fact]
        public void Good_Structure_NoOperator()
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
    Public Shared Operator =(a As I, b As I)
        Return True
    End Operator
End Interface");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/7305")]
        public void Ignored_TopLevel()
        {
            VerifyBasic(@"
Public Shared Operator =(a As I, b As I)
    Return True
End Operator");
        }

        [Fact]
        public void Bad_Class()
        {
            VerifyBasic(@"
Class C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator
    
    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator
End Class",
            // Test0.vb(2,7): warning CA2224: Override Equals on overloading operator equals
            GetBasicResultAt(2, 7, BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer.Rule));
        }

        [Fact]
        public void Bad_Structure()
        {
            VerifyBasic(@"
Structure C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator
    
    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator
End Structure",
            // Test0.vb(2,11): warning CA2224: Override Equals on overloading operator equals
            GetBasicResultAt(2, 11, BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer.Rule));
        }

        [Fact]
        public void Bad_NotOverride()
        {
            VerifyBasic(@"
Class C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator

    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator

    Public Shadows Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class",
            // Test0.vb(2,7): warning CA2224: Override Equals on overloading operator equals
            GetBasicResultAt(2, 7, BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer.Rule));
        }

        [Fact]
        public void Bad_FalseOverride()
        {
            VerifyBasic(@"
Class Base
    Public Overridable Shadows Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class

Class Derived : Inherits Base
    Public Shared Operator =(a As Derived, b As Derived)
        Return True
    End Operator

    Public Shared Operator <>(a As Derived, b As Derived)
        Return True
    End Operator

    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class",
            // Test0.vb(8,7): warning CA2224: Override Equals on overloading operator equals
            GetBasicResultAt(8, 7, BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer.Rule));
        }
    }
}