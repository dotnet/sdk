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

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")]
    internal class Resource2 { }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")]
    internal class Resource3 { }
}";

        private const string BasicDesignerFile = @"
Namespace My.Resources
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")> _
    Friend Class Resource1
    End Class

    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")> _
    Friend Class Resource2
    End Class

    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")> _
    Friend Class Resource3
    End Class
End Namespace";

        [Fact]
        public async Task TestCSharpNoResourceFileAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"class C {}");
        }

        [Fact]
        public async Task TestBasicNoResourceFileAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"Class C
End Class");
        }

        [Fact]
        public async Task TestCSharpResourceFileAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"class C {}", VerifyCS.Diagnostic());
        }

        [Fact]
        public async Task TestBasicResourceFileAsync()
        {
            await VerifyBasicWithDependenciesAsync(@"Class C
End Class", VerifyVB.Diagnostic());
        }

        [Fact]
        public async Task TestCSharpInvalidAttribute1Async()
        {
#pragma warning disable RS0030 // Do not use banned APIs
            await VerifyCSharpWithDependenciesAsync(@"[assembly: System.Resources.NeutralResourcesLanguage("""")]", VerifyCS.Diagnostic().WithLocation(1, 12));
#pragma warning restore RS0030 // Do not use banned APIs
        }

        [Fact]
        public async Task TestCSharpInvalidAttribute2Async()
        {
#pragma warning disable RS0030 // Do not use banned APIs
            await VerifyCSharpWithDependenciesAsync(@"[assembly: System.Resources.NeutralResourcesLanguage(null)]", VerifyCS.Diagnostic().WithLocation(1, 12));
#pragma warning restore RS0030 // Do not use banned APIs
        }

        [Fact]
        public async Task TestBasicInvalidAttribute1Async()
        {
#pragma warning disable RS0030 // Do not use banned APIs
            await VerifyBasicWithDependenciesAsync(@"<Assembly: System.Resources.NeutralResourcesLanguage("""")>", VerifyVB.Diagnostic().WithLocation(1, 2));
#pragma warning restore RS0030 // Do not use banned APIs
        }

        [Fact]
        public async Task TestBasicInvalidAttribute2Async()
        {
#pragma warning disable RS0030 // Do not use banned APIs
            await VerifyBasicWithDependenciesAsync(@"<Assembly: System.Resources.NeutralResourcesLanguage(Nothing)>", VerifyVB.Diagnostic().WithLocation(1, 2));
#pragma warning restore RS0030 // Do not use banned APIs
        }

        [Fact]
        public async Task TestCSharpvalidAttributeAsync()
        {
            await VerifyCSharpWithDependenciesAsync(@"[assembly: System.Resources.NeutralResourcesLanguage(""en"")]");
        }

        [Fact]
        public async Task TestBasicvalidAttributeAsync()
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
