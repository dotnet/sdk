// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureRandomness,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureRandomness,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseInsecureRandomnessTests
    {
        [Fact]
        public async Task Test_UsingMethodNext_OfRandom_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod(Random random)
    {
        var sensitiveVariable = random.Next();
    }
}",
            GetCSharpResultAt(8, 33, "Random"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

class TestClass
    public Sub TestMethod(random As Random)
        Dim sensitiveVariable As Integer
        sensitiveVariable = random.Next()
    End Sub
End Class",
            GetBasicResultAt(7, 29, "Random"));
        }

        [Fact]
        public async Task Test_UsingMethodNextDouble_OfRandom_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod(Random random)
    {
        var sensitiveVariable = random.NextDouble();
    }
}",
            GetCSharpResultAt(8, 33, "Random"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

class TestClass
    public Sub TestMethod(random As Random)
        Dim sensitiveVariable As Integer
        sensitiveVariable = random.NextDouble()
    End Sub
End Class",
            GetBasicResultAt(7, 29, "Random"));
        }

        [Fact]
        public async Task Test_UsingMethodGetHashCode_OfObject_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod(Random random)
    {
        var hashCode = random.GetHashCode();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

class TestClass
    public Sub TestMethod(random As Random)
        Dim hashCode As Integer
        hashCode = random.GetHashCode()
    End Sub
End Class");
        }

        [Fact]
        public async Task Test_UsingConstructor_OfRandom_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        var random = new Random();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

class TestClass
    public Sub TestMethod
        Dim random As New Random
    End Sub
End Class");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
