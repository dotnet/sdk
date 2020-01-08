// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
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
            GetTooGenericCSharpResultAt(10, 23, "System.Exception"));

            VerifyBasic(@"
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
            GetReservedCSharpResultAt(10, 23, "System.StackOverflowException"));

            VerifyBasic(@"
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

        private const string RuleId = CSharpDoNotRaiseReservedExceptionTypesAnalyzer.RuleId;

        private DiagnosticResult GetTooGenericCSharpResultAt(int line, int column, string callee)
        {
            return GetCSharpResultAt(line, column, RuleId, string.Format(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesMessageTooGeneric, callee));
        }

        private DiagnosticResult GetReservedCSharpResultAt(int line, int column, string callee)
        {
            return GetCSharpResultAt(line, column, RuleId, string.Format(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesMessageReserved, callee));
        }

        private DiagnosticResult GetTooGenericBasicResultAt(int line, int column, string callee)
        {
            return GetBasicResultAt(line, column, RuleId, string.Format(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesMessageTooGeneric, callee));
        }

        private DiagnosticResult GetReservedBasicResultAt(int line, int column, string callee)
        {
            return GetBasicResultAt(line, column, RuleId, string.Format(MicrosoftNetCoreAnalyzersResources.DoNotRaiseReservedExceptionTypesMessageReserved, callee));
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicDoNotRaiseReservedExceptionTypesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpDoNotRaiseReservedExceptionTypesAnalyzer();
        }
    }
}