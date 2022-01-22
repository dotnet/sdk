// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideGetHashCodeOnOverridingEqualsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideGetHashCodeOnOverridingEqualsTests
    {
        [Fact]
        public async Task Good_Class_EqualsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Good_Class_NoEqualsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
End Class");
        }

        [Fact]
        public async Task Good_Structure_EqualsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Good_Structure_NoEqualsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Structure C
End Structure");
        }

        [Fact]
        public async Task Bad_ClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class",
            // Test0.vb(2,7): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(2, 7));
        }

        [Fact]
        public async Task Bad_StructureAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Structure C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Structure",
            // Test0.vb(2,11): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(2, 11));
        }

        [Fact]
        public async Task Bad_NotOverrideAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Shadows Function GetHashCode() As Integer
        Return 0
    End Function
End Class",
            // Test0.vb(2,7): warning CA2224: Override GetHashCode on overriding Equals
            GetBasicResultAt(2, 7));
        }

        [Fact]
        public async Task Bad_FalseOverrideAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
            GetBasicResultAt(8, 7));
        }

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
