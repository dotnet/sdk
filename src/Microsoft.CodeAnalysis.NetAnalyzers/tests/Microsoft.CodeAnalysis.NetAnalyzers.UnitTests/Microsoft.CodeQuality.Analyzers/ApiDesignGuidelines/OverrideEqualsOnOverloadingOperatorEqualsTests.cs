// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideEqualsOnOverloadingOperatorEqualsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideEqualsOnOverloadingOperatorEqualsTests
    {
        [Fact]
        public async Task Good_Class_Operator()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Good_Class_NoOperator()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
End Class");
        }

        [Fact]
        public async Task Good_Structure_Operator()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Good_Structure_NoOperator()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Structure C
End Structure");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/7305")]
        public async Task Ignored_Interface()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Interface I
    Public Shared Operator =(a As I, b As I)
        Return True
    End Operator
End Interface");
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/7305")]
        public async Task Ignored_TopLevel()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Shared Operator =(a As I, b As I)
    Return True
End Operator");
        }

        [Fact]
        public async Task Bad_Class()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator
    
    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator
End Class",
            // Test0.vb(2,7): warning CA2224: Override Equals on overloading operator equals
            GetBasicResultAt(2, 7));
        }

        [Fact]
        public async Task Bad_Structure()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Structure C
    Public Shared Operator =(a As C, b As C)
        Return True
    End Operator
    
    Public Shared Operator <>(a As C, b As C)
        Return True
    End Operator
End Structure",
            // Test0.vb(2,11): warning CA2224: Override Equals on overloading operator equals
            GetBasicResultAt(2, 11));
        }

        [Fact]
        public async Task Bad_NotOverride()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
            GetBasicResultAt(2, 7));
        }

        [Fact]
        public async Task Bad_FalseOverride()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
            GetBasicResultAt(8, 7));
        }

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}