// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class DoNotRaiseExceptionsInExceptionClausesTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotRaiseExceptionsInExceptionClausesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotRaiseExceptionsInExceptionClausesAnalyzer();
        }

        [Fact]
        public void CSharpSimpleCase()
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
            VerifyCSharp(code,
                GetCSharpResultAt(22, 13, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule));
        }

        [Fact]
        public void BasicSimpleCase()
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
            VerifyBasic(code,
                GetBasicResultAt(13, 13, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule));
        }

        [Fact]
        public void CSharpNestedFinally()
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
            VerifyCSharp(code,
                GetCSharpResultAt(15, 17, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule),
                GetCSharpResultAt(19, 17, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule),
                GetCSharpResultAt(23, 17, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule),
                GetCSharpResultAt(25, 13, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule));
        }

        [Fact]
        public void BasicNestedFinally()
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
            VerifyBasic(code,
                GetBasicResultAt(9, 17, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule),
                GetBasicResultAt(11, 17, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule),
                GetBasicResultAt(13, 17, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule),
                GetBasicResultAt(15, 13, DoNotRaiseExceptionsInExceptionClausesAnalyzer.Rule));
        }
    }
}