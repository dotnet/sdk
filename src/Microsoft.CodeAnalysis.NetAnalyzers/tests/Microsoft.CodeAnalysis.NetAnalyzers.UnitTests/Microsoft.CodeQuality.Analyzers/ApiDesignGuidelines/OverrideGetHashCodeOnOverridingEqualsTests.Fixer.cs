// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer, // Diagnostic is from the compiler
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpOverrideGetHashCodeOnOverridingEqualsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideGetHashCodeOnOverridingEqualsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideGetHashCodeOnOverridingEqualsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideGetHashCodeOnOverridingEqualsFixerTests
    {
        [Fact]
        public async Task CS0659Async()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class {|CS0659:C|}
{
    public override bool Equals(object obj) => true;
}
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
class C
{
    public override bool Equals(object obj) => true;

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }
}
",
                    },
                },
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var compilationOptions = solution.GetProject(projectId).CompilationOptions;
                        compilationOptions = compilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Error);
                        return solution.WithProjectCompilationOptions(projectId, compilationOptions);
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CS0659_SimplifiedAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

class {|CS0659:C|}
{
    public override bool Equals(object obj) => true;
}
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
using System;

class C
{
    public override bool Equals(object obj) => true;

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
",
                    },
                },
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var compilationOptions = solution.GetProject(projectId).CompilationOptions;
                        compilationOptions = compilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Error);
                        return solution.WithProjectCompilationOptions(projectId, compilationOptions);
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Basic_CA2218Async()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Class [|C|]
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class
",
@"
Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

        [Fact]
        public async Task Basic_CA2218_SimplifiedAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Class [|C|]
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function
End Class
",
@"
Imports System

Class C
    Public Overrides Function Equals(o As Object) As Boolean
        Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New NotImplementedException()
    End Function
End Class
");
        }
    }
}