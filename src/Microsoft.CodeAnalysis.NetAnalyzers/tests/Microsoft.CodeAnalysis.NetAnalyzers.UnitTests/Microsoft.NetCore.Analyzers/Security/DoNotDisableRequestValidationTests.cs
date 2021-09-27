// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotDisableRequestValidation,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotDisableRequestValidationTests
    {
        private async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            string validateInputAttributeCSharpSourceCode = @"
namespace System.Web.Mvc
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple=false, Inherited=true)]
    public class ValidateInputAttribute : System.Attribute
    {
        public ValidateInputAttribute (bool enableValidation)
        {
        }
    }
}";
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source, validateInputAttributeCSharpSourceCode }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task TestLiteralAtActionLevelDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

class TestControllerClass
{
    [ValidateInput(false)]
    public void TestActionMethod()
    {
    }
}",
            GetCSharpResultAt(7, 17, "TestActionMethod"));
        }

        [Fact]
        public async Task TestConstAtActionLevelDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

class TestControllerClass
{
    private const bool flag = false;

    [ValidateInput(flag)]
    public void TestActionMethod()
    {
    }
}",
            GetCSharpResultAt(9, 17, "TestActionMethod"));
        }

        [Fact]
        public async Task TestLiteralAtControllerLevelDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

[ValidateInput(false)]
class TestControllerClass
{
    public void TestActionMethod()
    {
    }
}",
            GetCSharpResultAt(5, 7, "TestControllerClass"));
        }

        [Fact]
        public async Task TestSetBothControllerLevelAndActionLevelDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

[ValidateInput(true)]
class TestControllerClass
{
    [ValidateInput(false)]
    public void TestActionMethod()
    {
    }
}",
            GetCSharpResultAt(8, 17, "TestActionMethod"));
        }

        [Fact]
        public async Task TestLiteralAtActionLevelNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

class TestControllerClass
{
    [ValidateInput(true)]
    public void TestActionMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestConstAtActionLevelNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

class TestControllerClass
{
    private const bool flag = true;

    [ValidateInput(flag)]
    public void TestActionMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestLiteralAtControllerLevelNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

[ValidateInput(true)]
class TestControllerClass
{
    public void TestActionMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestSetBothControllerLevelAndActionLevelNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

[ValidateInput(false)]
class TestControllerClass
{
    [ValidateInput(true)]
    public void TestActionMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestWithoutValidateInputAttributeNoDiagnosticAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Web.Mvc;

class TestControllerClass
{
    public void TestActionMethod()
    {
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
