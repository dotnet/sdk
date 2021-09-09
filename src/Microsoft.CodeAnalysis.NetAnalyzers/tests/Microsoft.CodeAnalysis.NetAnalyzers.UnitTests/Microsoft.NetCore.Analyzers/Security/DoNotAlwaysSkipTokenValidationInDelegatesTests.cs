// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotAlwaysSkipTokenValidationInDelegates,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotAlwaysSkipTokenValidationInDelegatesTests
    {
        [Fact]
        public async Task TestLambdaDiagnostic()
        {
            const string CodeFormat = @"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{{
    public void TestMethod()
    {{
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.{0} => {{ return true; }};
    }}
}}";
            var delegateDeclarationStrings = new string[]
            {
                "AudienceValidator = (a, b, c)",
                "LifetimeValidator = (a, b, c, d)"
            };

            foreach (string declaration in delegateDeclarationStrings)
            {
                string code = string.Format(CultureInfo.InvariantCulture, CodeFormat, declaration);
                await VerifyCSharpAnalyzerAsync(
                    code,
                    GetCSharpResultAt(10, 20 + declaration.IndexOf('(')).WithArguments(
                        declaration.Substring(0, declaration.IndexOf(" ", StringComparison.OrdinalIgnoreCase))));
            }
        }

        [Fact]
        public async Task TestLambdaWithLiteralValueDiagnostic()
        {
            const string CodeFormat = @"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{{
    public void TestMethod()
    {{
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.{0} => true;
    }}
}}";
            var delegateDeclarationStrings = new string[]
            {
                "AudienceValidator = (a, b, c)",
                "LifetimeValidator = (a, b, c, d)"
            };

            foreach (string declaration in delegateDeclarationStrings)
            {
                string code = string.Format(CultureInfo.InvariantCulture, CodeFormat, declaration);
                await VerifyCSharpAnalyzerAsync(
                    code,
                    GetCSharpResultAt(10, 20 + declaration.IndexOf('(')).WithArguments(
                        declaration.Substring(0, declaration.IndexOf(" ", StringComparison.OrdinalIgnoreCase))));
            }
        }

        [Fact]
        public async Task TestAnonymousMethodDiagnostic()
        {
            const string CodeFormat = @"
using System;
using Microsoft.IdentityModel.Tokens;

class TestClass
{{
    public void TestMethod()
    {{
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.{0} = delegate {{ return true; }};
    }}
}}";
            var delegateDeclarationStrings = new string[]
            {
                "AudienceValidator",
                "LifetimeValidator"
            };

            foreach (string declaration in delegateDeclarationStrings)
            {
                string code = string.Format(CultureInfo.InvariantCulture, CodeFormat, declaration);
                await VerifyCSharpAnalyzerAsync(code, GetCSharpResultAt(10, 23 + declaration.Length)
                    .WithArguments(declaration));
            }
        }

        [Fact]
        public async Task TestDelegateCreationLocalFunctionDiagnostic_LifetimeValidator()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.LifetimeValidator = new LifetimeValidator(AcceptAllLifetimes);

        bool AcceptAllLifetimes(
            DateTime? start,
            DateTime? end,
            SecurityToken securityToken,
            TokenValidationParameters validationParameters)
        {
            return true;
        }
    }
}",
            GetCSharpResultAt(11, 40).WithArguments("LifetimeValidator"));
        }

        [Fact]
        public async Task TestDelegateCreationLocalFunctionDiagnostic_AudienceValidator()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = new AudienceValidator(AcceptAllAudiences);

        bool AcceptAllAudiences(
            IEnumerable<string> audiences,
            SecurityToken securityToken,
            TokenValidationParameters validationParameters)
        {
            return true;
        }
    }
}",
            GetCSharpResultAt(11, 40).WithArguments("AudienceValidator"));
        }

        [Fact]
        public async Task TestDelegateCreationDiagnostic_LifetimeValidator()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    bool AcceptAllLifetimes(
        DateTime? start,
        DateTime? end,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        return true;
    }

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.LifetimeValidator = new LifetimeValidator(AcceptAllLifetimes);
    }
}",
            GetCSharpResultAt(20, 40).WithArguments("LifetimeValidator"));
        }

        [Fact]
        public async Task TestDelegateCreationDiagnostic_AudienceValidator()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        return true;
    }

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = new AudienceValidator(AcceptAllAudiences);
    }
}",
            GetCSharpResultAt(19, 40).WithArguments("AudienceValidator"));
        }

        [Fact]
        public async Task TestDelegateCreationNormalMethodWithLambdaDiagnostic_AudienceValidator()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters) => true;

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = new AudienceValidator(AcceptAllAudiences);
    }
}",
            GetCSharpResultAt(16, 40).WithArguments("AudienceValidator"));
        }

        // Ideally we could detect this but we'll have to rely on CodeQL instead for more robust detection.
        [Fact]
        public async Task TestDelegatedMethodFromDifferentAssemblyNoDiagnostic()
        {
            string source1 = @"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace AcceptAllAudiencesNamespace
{
    public class AcceptAllAudiencesClass
    {
        public static bool AcceptAllAudiences(
            IEnumerable<string> audiences,
            SecurityToken securityToken,
            TokenValidationParameters validationParameters)
        {
            return true;
        }
    }
}";

            var source2 = @"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using AcceptAllAudiencesNamespace;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = new AudienceValidator(AcceptAllAudiencesClass.AcceptAllAudiences);
    }
}";

            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithWilson,
                TestState =
                {
                    Sources = { source2 },
                },
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var sideProject = solution.AddProject("DependencyProject", "DependencyProject", LanguageNames.CSharp)
                            .AddDocument("Dependency.cs", source1).Project
                            .AddMetadataReferences(solution.GetProject(projectId).MetadataReferences)
                            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                        return sideProject.Solution.GetProject(projectId)
                            .AddProjectReference(new ProjectReference(sideProject.Id))
                            .Solution;
                    }
                }
            }.RunAsync();
        }

        // Ideally we could detect this but we'll have to rely on CodeQL instead for more robust detection.
        [Fact]
        public async Task TestDelegatedMethodFromLocalFromDifferentAssemblyNoDiagnostic()
        {
            string source1 = @"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace AcceptAllAudiencesNamespace
{
    public class AcceptAllAudiencesClass
    {
        public static bool AcceptAllAudiences2(
            IEnumerable<string> audiences,
            SecurityToken securityToken,
            TokenValidationParameters validationParameters)
        {
            return true;
        }
    }
}";

            var source2 = @"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using AcceptAllAudiencesNamespace;

class TestClass
{
    public bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        return AcceptAllAudiencesClass.AcceptAllAudiences2(audiences, securityToken, validationParameters);
    }

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = new AudienceValidator(AcceptAllAudiences);
    }
}";

            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithWilson,
                TestState =
                {
                    Sources = { source2 },
                },
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var sideProject = solution.AddProject("DependencyProject", "DependencyProject", LanguageNames.CSharp)
                            .AddDocument("Dependency.cs", source1).Project
                            .AddMetadataReferences(solution.GetProject(projectId).MetadataReferences)
                            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                        return sideProject.Solution.GetProject(projectId)
                            .AddProjectReference(new ProjectReference(sideProject.Id))
                            .Solution;
                    }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task TestLambdaNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = (a, b, c) => { if(a != null) {return true;} return false;};
    }
}");
        }

        [Fact]
        public async Task TestLambdaWithLiteralValueNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator = (a, b, c) => false;
    }
}");
        }

        [Fact]
        public async Task TestAnonymousMethodNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator += delegate { return false; };
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationLocalFunctionNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator  = new AudienceValidator(AcceptAllAudiences);

        bool AcceptAllAudiences(
            IEnumerable<string> audiences,
            SecurityToken securityToken,
            TokenValidationParameters validationParameters)
        {
            if(audiences != null)
            {
                return true;
            }

            return false;
        }
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        if(audiences != null)
        {
            return true;
        }

        return false;
    }

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator  = new AudienceValidator(AcceptAllAudiences);
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationNoDiagnostic2()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        if(audiences != null)
        {
            return true;
        }

        return false;
    }

    public void TestMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationNormalMethodWithLambdaNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters) => false;

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator  = new AudienceValidator(AcceptAllAudiences);
    }
}");
        }

        // Ideally we could detect this but we'll have to rely on CodeQL instead for more robust detection.
        [Fact]
        public async Task TestDelegateCreationFromLocalFromLocalNoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

class TestClass
{
    public bool AcceptAllAudiences2(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        return true;
    }

    public bool AcceptAllAudiences(
        IEnumerable<string> audiences,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        return AcceptAllAudiences2(
          audiences,
          securityToken,
          validationParameters);
    }

    public void TestMethod()
    {
        TokenValidationParameters parameters = new TokenValidationParameters();
        parameters.AudienceValidator  = new AudienceValidator(AcceptAllAudiences);
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

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
    }
}