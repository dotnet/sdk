// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseSecureCookiesASPNetCore,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseSecureCookiesASPNetCoreTests
    {
        [Fact]
        public async Task TestHasWrongSecurePropertyAssignmentDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = false;
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(12, 9, UseSecureCookiesASPNetCore.DefinitelyUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestHasWrongSecurePropertyAssignmentMaybeChangedRightDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = false;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            cookieOptions.Secure = true;
        }

        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                    GetCSharpResultAt(20, 9, UseSecureCookiesASPNetCore.MaybeUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestHasRightSecurePropertyAssignmentMaybeChangedWrongDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = true;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            cookieOptions.Secure = false;
        }

        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(20, 9, UseSecureCookiesASPNetCore.MaybeUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestAssignSecurePropertyAnUnassignedVariableMaybeChangedWrongDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value, bool secure)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = secure;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            cookieOptions.Secure = false;
        }

        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(20, 9, UseSecureCookiesASPNetCore.MaybeUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestAssignSecurePropertyAnUnassignedVariableMaybeChangedRightDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value, bool secure)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = secure;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            cookieOptions.Secure = true;
        }

        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(20, 9, UseSecureCookiesASPNetCore.MaybeUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestAssignSecurePropertyAnAssignedVariableMaybeChangedDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        var secure = true;
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            secure = false;
        }
        
        cookieOptions.Secure = secure;
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(21, 9, UseSecureCookiesASPNetCore.MaybeUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestHasWrongSecurePropertyInitializerDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions() { Secure = false };
        var responseCookies = new ResponseCookies(null, null);
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(11, 9, UseSecureCookiesASPNetCore.DefinitelyUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestWithoutSecurePropertyAssignmentDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(11, 9, UseSecureCookiesASPNetCore.DefinitelyUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestParamterLengthLessThan3TrueDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value);
    }
}",
                GetCSharpResultAt(10, 9, UseSecureCookiesASPNetCore.DefinitelyUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestGetCookieOptionsFromOtherMethodInterproceduralDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, GetCookieOptions());
    }

    public CookieOptions GetCookieOptions()
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = false;

        return cookieOptions;
    }
}",
                GetCSharpResultAt(10, 9, UseSecureCookiesASPNetCore.DefinitelyUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestPassCookieOptionsAsParameterInterproceduralDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = false;
        TestMethod2(key, value, cookieOptions); 
    }

    public void TestMethod2(string key, string value, CookieOptions cookieOptions)
    {
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}",
                GetCSharpResultAt(17, 9, UseSecureCookiesASPNetCore.DefinitelyUseSecureCookiesASPNetCoreRule));
        }

        [Fact]
        public async Task TestHasRightSecurePropertyAssignmentNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.Secure = true;
        var responseCookies = new ResponseCookies(null, null); 
        responseCookies.Append(key, value, cookieOptions);
    }
}");
        }

        [Fact]
        public async Task TestHasRightSecurePropertyInitializerNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

class TestClass
{
    public void TestMethod(string key, string value)
    {
        var cookieOptions = new CookieOptions() { Secure = true };
        var responseCookies = new ResponseCookies(null, null);
        responseCookies.Append(key, value, cookieOptions);
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAspNetCoreMvc,
                TestCode = source,
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(rule)
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
