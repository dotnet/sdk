// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotDisableTokenValidationChecks,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotDisableTokenValidationChecksTests
    {
        [Fact]
        public async Task TestLiteralDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        [|parameters.RequireExpirationTime = false|];
        [|parameters.ValidateAudience = false|];
        [|parameters.ValidateIssuer = false|];
        [|parameters.ValidateLifetime = false|];
        parameters.RequireAudience = false;  // here and below are valid to be false, no warning expected.
        parameters.RequireSignedTokens = false;
        parameters.ValidateActor = false;
        parameters.ValidateIssuerSigningKey = false;
        parameters.ValidateTokenReplay = false;
    }
}");
        }

        [Fact]
        public async Task RegressionTestForPreventingNullRefDuringParsing()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        [|parameters.RequireExpirationTime = false|];
        parameters.ValidAlgorithms = null;
    }
}");
        }

        [Fact]
        public async Task TestConstantDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        const bool flag = false;
        var parameters = new TokenValidationParameters();
        [|parameters.RequireExpirationTime = flag|];
        [|parameters.ValidateAudience = flag|];
        [|parameters.ValidateIssuer = flag|];
        [|parameters.ValidateLifetime = flag|];
        parameters.RequireAudience = flag;  // here and below are valid to be false, no warning expected.
        parameters.RequireSignedTokens = flag;
        parameters.ValidateActor = flag;
        parameters.ValidateIssuerSigningKey = flag;
        parameters.ValidateTokenReplay = flag;
    }
}");
        }

        [Fact]
        public async Task TestPropertyInitializerDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        var parameters = new TokenValidationParameters
        {
            [|RequireExpirationTime = false|],
            [|ValidateAudience = false|],
            [|ValidateIssuer = false|],
            [|ValidateLifetime = false|],
            RequireAudience = false, // here and below are valid to be false, no warning expected.
            RequireSignedTokens = false,
            ValidateActor = false,
            ValidateIssuerSigningKey = false,
            ValidateTokenReplay = false
        };
    }
}");
        }

        //Ideally, we would generate a diagnostic in this case.
        [Fact]
        public async Task TestVariableNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        var flag = false;
        var parameters = new TokenValidationParameters();
        parameters.RequireExpirationTime = flag;
        parameters.ValidateAudience = flag;
        parameters.ValidateIssuer = flag;
        parameters.ValidateLifetime = flag;
        parameters.RequireAudience = flag;  // here and below are valid to be false, no warning desired.
        parameters.RequireSignedTokens = flag;
        parameters.ValidateActor = flag;
        parameters.ValidateIssuerSigningKey = flag;
        parameters.ValidateTokenReplay = flag;
    }
}");
        }

        [Fact]
        public async Task TestLiteralNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        var parameters = new TokenValidationParameters();
        parameters.RequireExpirationTime = true;
        parameters.ValidateAudience = true;
        parameters.ValidateIssuer = true;
        parameters.ValidateLifetime = true;
        parameters.RequireAudience = true;
        parameters.RequireSignedTokens = true;
        parameters.ValidateActor = true;
        parameters.ValidateIssuerSigningKey = true;
        parameters.ValidateTokenReplay = true;
    }
}");
        }

        [Fact]
        public async Task TestConstantNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        const bool flag = true;
        var parameters = new TokenValidationParameters();
        parameters.RequireExpirationTime = flag;
        parameters.ValidateAudience = flag;
        parameters.ValidateIssuer = flag;
        parameters.ValidateLifetime = flag;
        parameters.RequireAudience = flag;
        parameters.RequireSignedTokens = flag;
        parameters.ValidateActor = flag;
        parameters.ValidateIssuerSigningKey = flag;
        parameters.ValidateTokenReplay = flag;
    }
}");
        }

        [Fact]
        public async Task TestPropertyInitializerNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        var parameters = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true,
            RequireAudience = true,
            RequireSignedTokens = true,
            ValidateActor = true,
            ValidateIssuerSigningKey = true,
            ValidateTokenReplay = true
        };
    }
}");
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithWilson,
                TestState =
                {
                    Sources = { source },
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }
    }
}
