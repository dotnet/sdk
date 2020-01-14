// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDoNotRaiseReservedExceptionTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDoNotRaiseReservedExceptionTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotRaiseReservedExceptionTypesTests
    {
        [Fact]
        public async Task CreateSystemNotImplementedException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CreateSystemException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            GetTooGenericCSharpResultAt(10, 23, "System.Exception"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
            Throw New Exception()
		End Sub
	End Class
End Namespace",
            GetTooGenericBasicResultAt(7, 23, "System.Exception"));
        }

        [Fact]
        public async Task CreateSystemStackOverflowException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            GetReservedCSharpResultAt(10, 23, "System.StackOverflowException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
            Throw New StackOverflowException()
		End Sub
	End Class
End Namespace",
            GetReservedBasicResultAt(7, 23, "System.StackOverflowException"));
        }

        private DiagnosticResult GetTooGenericCSharpResultAt(int line, int column, string callee)
            => VerifyCS.Diagnostic(CSharpDoNotRaiseReservedExceptionTypesAnalyzer.TooGenericRule)
                .WithLocation(line, column)
                .WithArguments(callee);

        private DiagnosticResult GetTooGenericBasicResultAt(int line, int column, string callee)
            => VerifyVB.Diagnostic(BasicDoNotRaiseReservedExceptionTypesAnalyzer.TooGenericRule)
                .WithLocation(line, column)
                .WithArguments(callee);

        private DiagnosticResult GetReservedCSharpResultAt(int line, int column, string callee)
           => VerifyCS.Diagnostic(CSharpDoNotRaiseReservedExceptionTypesAnalyzer.ReservedRule)
               .WithLocation(line, column)
               .WithArguments(callee);

        private DiagnosticResult GetReservedBasicResultAt(int line, int column, string callee)
            => VerifyVB.Diagnostic(BasicDoNotRaiseReservedExceptionTypesAnalyzer.ReservedRule)
                .WithLocation(line, column)
                .WithArguments(callee);
    }
}