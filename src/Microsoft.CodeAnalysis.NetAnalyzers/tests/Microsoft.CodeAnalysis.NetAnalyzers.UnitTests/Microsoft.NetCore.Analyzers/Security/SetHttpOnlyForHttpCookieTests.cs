// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.SetHttpOnlyForHttpCookie,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class SetHttpOnlyForHttpCookieTests
    {
        protected async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            string httpCookieCSharpSourceCode = @"
namespace System.Web
{
    public sealed class HttpCookie
    {
        public HttpCookie (string name)
        {
        }

        public HttpCookie (string name, string value)
        {
        }
        
        public bool HttpOnly { get; set; }
    }
}";
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source, httpCookieCSharpSourceCode }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task Test_AssignHttpOnlyWithFalse_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        httpCookie.HttpOnly = false;
    }
}",
            GetCSharpResultAt(9, 9));
        }

        [Fact]
        public async Task Test_AssignHttpOnlyWithFalsePossibly_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            httpCookie.HttpOnly = false;
        }
    }
}",
            GetCSharpResultAt(14, 13));
        }

        [Fact]
        public async Task Test_ReturnHttpCookieWithFalseHttpOnly_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public HttpCookie TestMethod(HttpCookie httpCookie)
    {
        httpCookie.HttpOnly = false;

        return httpCookie;
    }
}",
            GetCSharpResultAt(8, 9));
        }

        [Fact]
        public async Task Test_ReturnHttpCookie_WithoutSettingHttpOnly_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public HttpCookie TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");

        return httpCookie;
    }
}",
            GetCSharpResultAt(10, 16));
        }

        [Fact]
        public async Task Test_PassHttpCookieAsAParamter_WithoutSettingHttpOnly_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        TestMethod2(httpCookie);
    }

    public void TestMethod2(HttpCookie httpCookie)
    {
    }
}",
            GetCSharpResultAt(9, 21));
        }

        [Fact]
        public async Task Test_PassHttpCookieAsAParamter_WithSettingHttpOnlyAsFalse_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        httpCookie.HttpOnly = false;
        TestMethod2(httpCookie);
    }

    public void TestMethod2(HttpCookie httpCookie)
    {
    }
}",
            GetCSharpResultAt(9, 9));
        }

        [Fact]
        public async Task Test_PassHttpCookieAsAParamter_WithSettingHttpOnlyAsFalsePossibly_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            httpCookie.HttpOnly = false;
        }

        TestMethod2(httpCookie);
    }

    public void TestMethod2(HttpCookie httpCookie)
    {
    }
}",
            GetCSharpResultAt(14, 13));
        }

        [Fact]
        public async Task Test_CreateHttpCookieWithNullArguments_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(null, null);
    }
}");
        }

        [Fact]
        public async Task Test_AssignHttpOnlyWithTrue_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        httpCookie.HttpOnly = true;
    }
}");
        }

        [Fact]
        public async Task Test_JustObjectCreation_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
    }
}");
        }

        [Fact]
        public async Task Test_AssignHttpOnlyWithTruePossibly_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            httpCookie.HttpOnly = true;
        }
    }
}");
        }

        [Fact]
        public async Task Test_ReturnHttpCookieWithUnkownHttpOnly_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public HttpCookie TestMethod(HttpCookie httpCookie)
    {
        return httpCookie;
    }
}");
        }

        [Fact]
        public async Task Test_ReturnHttpCookieWithTrueHttpOnly_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public HttpCookie TestMethod(HttpCookie httpCookie)
    {
        httpCookie.HttpOnly = true;

        return httpCookie;
    }
}");
        }

        [Fact]
        public async Task Test_PassHttpCookieAsAParamter_WithSettingHttpOnlyAsTrue_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        httpCookie.HttpOnly = true;
        TestMethod2(httpCookie);
    }

    public HttpCookie TestMethod2(HttpCookie httpCookie)
    {
        return httpCookie;
    }
}");
        }

        [Fact]
        public async Task Test_PassHttpCookieAsAParamter_WithSettingHttpOnlyAsTruePossibly_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        var httpCookie = new HttpCookie(""cookieName"");
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            httpCookie.HttpOnly = true;
        }

        TestMethod2(httpCookie);
    }

    public void TestMethod2(HttpCookie httpCookie)
    {
    }
}");
        }

        [Fact]
        public async Task Test_PassHttpCookieWithNullValue_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public void TestMethod()
    {
        TestMethod2(null);
    }

    public void TestMethod2(HttpCookie httpCookie)
    {
    }
}");
        }

        [Fact]
        public async Task Test_ReturnNull_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web;

class TestClass
{
    public HttpCookie TestMethod(HttpCookie httpCookie)
    {
        return null;
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic()
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
