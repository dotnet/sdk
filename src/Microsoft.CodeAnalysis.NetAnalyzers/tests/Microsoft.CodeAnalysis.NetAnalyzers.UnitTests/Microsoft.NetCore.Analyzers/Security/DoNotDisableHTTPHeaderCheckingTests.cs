// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotDisableHTTPHeaderChecking,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotDisableHTTPHeaderCheckingTests
    {
        [Fact]
        public async Task TestLiteralDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = false;
    }
}",
            GetCSharpResultAt(10, 9));
        }

        [Fact]
        public async Task TestConstantDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        const bool flag = false;
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = flag;
    }
}",
            GetCSharpResultAt(11, 9));
        }

        [Fact]
        public async Task TestPropertyInitializerDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection
        {
            EnableHeaderChecking = false
        };
    }
}",
            GetCSharpResultAt(11, 13));
        }

        //Ideally, we would generate a diagnostic in this case.
        [Fact]
        public async Task TestVariableNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var flag = false;
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = flag;
    }
}");
        }

        [Fact]
        public async Task TestLiteralNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = true;
    }
}");
        }

        [Fact]
        public async Task TestConstantNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        const bool flag = true;
        var httpRuntimeSection = new HttpRuntimeSection();
        httpRuntimeSection.EnableHeaderChecking = flag;
    }
}");
        }

        [Fact]
        public async Task TestPropertyInitializerNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Web.Configuration;

class TestClass
{
    public void TestMethod()
    {
        var httpRuntimeSection = new HttpRuntimeSection
        {
            EnableHeaderChecking = true
        };
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources = { source },
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
