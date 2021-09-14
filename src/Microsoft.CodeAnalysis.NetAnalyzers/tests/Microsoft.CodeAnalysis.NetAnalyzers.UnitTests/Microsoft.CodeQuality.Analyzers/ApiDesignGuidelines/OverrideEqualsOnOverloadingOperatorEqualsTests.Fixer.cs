// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.Testing.EmptyDiagnosticAnalyzer, // Diagnostic is from the compiler
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpOverrideEqualsOnOverloadingOperatorEqualsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideEqualsOnOverloadingOperatorEqualsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideEqualsOnOverloadingOperatorEqualsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideEqualsOnOverloadingOperatorEqualsFixerTests
    {
        [Fact]
        public async Task CS0660()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class {|CS0660:{|CS0661:C|}|}
{
    public static bool operator ==(C c1, C c2) => true;
    public static bool operator !=(C c1, C c2) => false;
}
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
class {|CS0659:{|CS0661:C|}|}
{
    public static bool operator ==(C c1, C c2) => true;
    public static bool operator !=(C c1, C c2) => false;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (ReferenceEquals(obj, null))
        {
            return false;
        }

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
        public async Task CS0660_Simplified()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

class {|CS0660:{|CS0661:C|}|}
{
    public static bool operator ==(C c1, C c2) => true;
    public static bool operator !=(C c1, C c2) => false;
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

class {|CS0659:{|CS0661:C|}|}
{
    public static bool operator ==(C c1, C c2) => true;
    public static bool operator !=(C c1, C c2) => false;

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (ReferenceEquals(obj, null))
        {
            return false;
        }

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
        public async Task CA2224()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Class [|C|]
    Public Shared Operator =(c1 As C, c2 As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(c1 As C, c2 As C) As Boolean
        Return False
    End Operator
End Class
",
@"
Class C
    Public Shared Operator =(c1 As C, c2 As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(c1 As C, c2 As C) As Boolean
        Return False
    End Operator

    Public Overrides Function Equals(obj As Object) As Boolean
        If ReferenceEquals(Me, obj) Then
            Return True
        End If

        If ReferenceEquals(obj, Nothing) Then
            Return False
        End If

        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2224_Simplified()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Class [|C|]
    Public Shared Operator =(c1 As C, c2 As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(c1 As C, c2 As C) As Boolean
        Return False
    End Operator
End Class
",
@"
Imports System

Class C
    Public Shared Operator =(c1 As C, c2 As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(c1 As C, c2 As C) As Boolean
        Return False
    End Operator

    Public Overrides Function Equals(obj As Object) As Boolean
        If ReferenceEquals(Me, obj) Then
            Return True
        End If

        If ReferenceEquals(obj, Nothing) Then
            Return False
        End If

        Throw New NotImplementedException()
    End Function
End Class
");
        }
    }
}