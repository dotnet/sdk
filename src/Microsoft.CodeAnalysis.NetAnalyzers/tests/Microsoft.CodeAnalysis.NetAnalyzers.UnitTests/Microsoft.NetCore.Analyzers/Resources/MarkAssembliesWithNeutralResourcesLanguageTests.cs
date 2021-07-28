// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Resources.CSharpMarkAssembliesWithNeutralResourcesLanguageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Resources.BasicMarkAssembliesWithNeutralResourcesLanguageAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Resources.UnitTests
{
    public class MarkAssembliesWithNeutralResourcesLanguageTests
    {
        private const string CSharpDesignerFile = @"
namespace DesignerFile {
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")]
    internal class Resource1 { }
}";

        private const string BasicDesignerFile = @"
Namespace My.Resources
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")> _
    Friend Class Resource1
    End Class
End Namespace";

        [Fact]
        public async Task TestCSharpNoResourceFile()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"class C {}");
        }

        [Fact]
        public async Task TestBasicNoResourceFile()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"Class C
End Class");
        }

        [Fact]
        public async Task TestCSharpResourceFile()
        {
            await VerifyCSharpWithDependenciesAsync(@"class C {}", VerifyCS.Diagnostic());
        }

        [Fact]
        public async Task TestBasicResourceFile()
        {
            await VerifyBasicWithDependenciesAsync(@"Class C
End Class", VerifyVB.Diagnostic());
        }

        [Fact]
        public async Task TestCSharpInvalidAttribute1()
        {
#pragma warning disable RS0030 // Do not used banned APIs
            await VerifyCSharpWithDependenciesAsync(@"[assembly: System.Resources.NeutralResourcesLanguage("""")]", VerifyCS.Diagnostic().WithLocation(1, 12));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        public async Task TestCSharpInvalidAttribute2()
        {
#pragma warning disable RS0030 // Do not used banned APIs
            await VerifyCSharpWithDependenciesAsync(@"[assembly: System.Resources.NeutralResourcesLanguage(null)]", VerifyCS.Diagnostic().WithLocation(1, 12));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        public async Task TestBasicInvalidAttribute1()
        {
#pragma warning disable RS0030 // Do not used banned APIs
            await VerifyBasicWithDependenciesAsync(@"<Assembly: System.Resources.NeutralResourcesLanguage("""")>", VerifyVB.Diagnostic().WithLocation(1, 2));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        public async Task TestBasicInvalidAttribute2()
        {
#pragma warning disable RS0030 // Do not used banned APIs
            await VerifyBasicWithDependenciesAsync(@"<Assembly: System.Resources.NeutralResourcesLanguage(Nothing)>", VerifyVB.Diagnostic().WithLocation(1, 2));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        public async Task TestCSharpvalidAttribute()
        {
            await VerifyCSharpWithDependenciesAsync(@"[assembly: System.Resources.NeutralResourcesLanguage(""en"")]");
        }

        [Fact]
        public async Task TestBasicvalidAttribute()
        {
            await VerifyBasicWithDependenciesAsync(@"<Assembly: System.Resources.NeutralResourcesLanguage(""en"")>");
        }

        private async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        source,
                        ("Test.Designer.cs", CSharpDesignerFile),
                    },
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private async Task VerifyBasicWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        source,
                        ("Test.Designer.vb", BasicDesignerFile),
                    },
                },
            };

            vbTest.ExpectedDiagnostics.AddRange(expected);

            await vbTest.RunAsync();
        }
    }
}
