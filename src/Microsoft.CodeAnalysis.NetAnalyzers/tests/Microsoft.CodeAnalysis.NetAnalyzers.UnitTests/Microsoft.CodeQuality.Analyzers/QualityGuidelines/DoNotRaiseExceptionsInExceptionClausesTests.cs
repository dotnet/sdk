// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotRaiseExceptionsInExceptionClausesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotRaiseExceptionsInExceptionClausesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class DoNotRaiseExceptionsInExceptionClausesTests
    {
        [Fact]
        public async Task CSharpSimpleCaseAsync()
        {
            var code = @"
using System;

public class Test
{
    public void Method()
    {
        try
        {
            throw new Exception();
        }
        catch (ArgumentException e)
        {
            throw new Exception();
        }
        catch
        {
            throw new Exception();
        }
        finally
        {
            throw new Exception();
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(22, 13));
        }

        [Fact]
        public async Task BasicSimpleCaseAsync()
        {
            var code = @"
Imports System

Public Class Test
    Public Sub Method()
        Try
            Throw New Exception()
        Catch e As ArgumentException
            Throw New Exception()
        Catch
            Throw New Exception()
        Finally
            Throw New Exception()
        End Try
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicResultAt(13, 13));
        }

        [Fact]
        public async Task CSharpNestedFinallyAsync()
        {
            var code = @"
using System;

public class Test
{
    public static void Main()
    {
        try
        {
        }
        finally
        {
            try
            {
                throw new Exception();
            }
            catch 
            {
                throw new Exception();
            }
            finally
            {
                throw new Exception();
            }
            throw new Exception();
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(15, 17),
                GetCSharpResultAt(19, 17),
                GetCSharpResultAt(23, 17),
                GetCSharpResultAt(25, 13));
        }

        [Fact]
        public async Task BasicNestedFinallyAsync()
        {
            var code = @"
Imports System

Public Class Test
    Public Sub Method()
        Try
        Finally
            Try
                Throw New Exception()
            Catch
                Throw New Exception()
            Finally
                Throw New Exception()
            End Try
            Throw New Exception()
        End Try
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicResultAt(9, 17),
                GetBasicResultAt(11, 17),
                GetBasicResultAt(13, 17),
                GetBasicResultAt(15, 13));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}