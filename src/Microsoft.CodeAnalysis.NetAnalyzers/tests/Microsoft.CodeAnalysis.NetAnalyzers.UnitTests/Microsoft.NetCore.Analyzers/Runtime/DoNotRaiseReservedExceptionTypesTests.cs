// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotRaiseReservedExceptionTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotRaiseReservedExceptionTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotRaiseReservedExceptionTypesTests
    {
        [Fact]
        public async Task CreateSystemNotImplementedExceptionAsync()
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
        public async Task CreateSystemExceptionAsync()
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
            GetTooGenericCSharpResultAt(10, 19, "System.Exception"));

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CreateSystemStackOverflowExceptionAsync()
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
            GetReservedCSharpResultAt(10, 19, "System.StackOverflowException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
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
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotRaiseReservedExceptionTypesAnalyzer.TooGenericRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(callee);

        private DiagnosticResult GetTooGenericBasicResultAt(int line, int column, string callee)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotRaiseReservedExceptionTypesAnalyzer.TooGenericRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(callee);

        private DiagnosticResult GetReservedCSharpResultAt(int line, int column, string callee)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(DoNotRaiseReservedExceptionTypesAnalyzer.ReservedRule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(callee);

        private DiagnosticResult GetReservedBasicResultAt(int line, int column, string callee)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotRaiseReservedExceptionTypesAnalyzer.ReservedRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(callee);
    }
}