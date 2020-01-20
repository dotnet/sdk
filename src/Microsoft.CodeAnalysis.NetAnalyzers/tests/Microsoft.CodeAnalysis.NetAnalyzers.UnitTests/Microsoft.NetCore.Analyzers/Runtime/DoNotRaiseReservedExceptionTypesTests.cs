// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotRaiseReservedExceptionTypesTests : DiagnosticAnalyzerTestBase
    {
        [Fact]
        public void CreateSystemNotImplementedException()
        {
            VerifyCSharp(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            throw new NotImplementedException();
        }
    }
}");

            VerifyBasic(@"
Imports System

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
            Throw New NotImplementedException()
		End Sub
	End Class
End Namespace");
        }

        [Fact]
        public void CreateSystemException()
        {
            VerifyCSharp(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            throw new Exception();
        }
    }
}",
            GetTooGenericCSharpResultAt(10, 19, "System.Exception"));

            VerifyBasic(@"
Imports System

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
            Throw New Exception()
		End Sub
	End Class
End Namespace",
            GetTooGenericBasicResultAt(7, 19, "System.Exception"));
        }

        [Fact]
        public void CreateSystemStackOverflowException()
        {
            VerifyCSharp(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            throw new StackOverflowException();
        }
    }
}",
            GetReservedCSharpResultAt(10, 19, "System.StackOverflowException"));

            VerifyBasic(@"
Imports System

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
            Throw New StackOverflowException()
		End Sub
	End Class
End Namespace",
            GetReservedBasicResultAt(7, 19, "System.StackOverflowException"));
        }

        private DiagnosticResult GetTooGenericCSharpResultAt(int line, int column, string callee)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseReservedExceptionTypesAnalyzer.TooGenericRule, callee);
        }

        private DiagnosticResult GetReservedCSharpResultAt(int line, int column, string callee)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseReservedExceptionTypesAnalyzer.ReservedRule, callee);
        }

        private DiagnosticResult GetTooGenericBasicResultAt(int line, int column, string callee)
        {
            return GetBasicResultAt(line, column, DoNotRaiseReservedExceptionTypesAnalyzer.TooGenericRule, callee);
        }

        private DiagnosticResult GetReservedBasicResultAt(int line, int column, string callee)
        {
            return GetBasicResultAt(line, column, DoNotRaiseReservedExceptionTypesAnalyzer.ReservedRule, callee);
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotRaiseReservedExceptionTypesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotRaiseReservedExceptionTypesAnalyzer();
        }
    }
}