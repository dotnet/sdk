// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DeclareTypesInNamespacesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpDeclareTypesInNamespacesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DeclareTypesInNamespacesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicDeclareTypesInNamespacesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DeclareTypesInNamespacesTests
    {
        [Fact]
        public async Task OuterTypeInGlobalNamespace_Warns()
        {
            var csCode = @"
public class [|Class|]
{
}
";
            await VerifyCS.VerifyCodeFixAsync(csCode, csCode);

            var vbCode = @"
Public Class [|[MyClass]|]
End Class";
            await VerifyVB.VerifyCodeFixAsync(vbCode, vbCode);
        }

        [Fact]
        public async Task NestedTypeInGlobalNamespace_WarnsOnlyOnce()
        {
            var csCode = @"
public class [|Class|]
{
    public class Nested {}
}
";
            await VerifyCS.VerifyCodeFixAsync(csCode, csCode);

            var vbCode = @"
Public Class [|[MyClass]|]
    Public Class Nested
    End Class
End Class
";
            await VerifyVB.VerifyCodeFixAsync(vbCode, vbCode);
        }

        [Fact]
        public async Task InternalClassInGlobalNamespace_DoesNotWarn()
        {
            var csCode = @"
internal class Class
{
    public class Nested {}
}";
            await VerifyCS.VerifyCodeFixAsync(csCode, csCode);

            var vbCode = @"
Friend Class [MyClass]
    Public Class Nested
    End Class
End Class";
            await VerifyVB.VerifyCodeFixAsync(vbCode, vbCode);
        }

        [Fact]
        public async Task PublicClassInNonGlobalNamespace_DoesNotWarn()
        {
            var csCode = @"
namespace NS
{
    public class Class
    {
        public class Nested {}
    }
}";
            await VerifyCS.VerifyCodeFixAsync(csCode, csCode);

            var vbCode = @"
Namespace NS
    Public Class [MyClass]
        Public Class Nested
        End Class
    End Class
End Namespace";
            await VerifyVB.VerifyCodeFixAsync(vbCode, vbCode);
        }

        [Fact]
        public async Task TopLevelProgramClass_DoesNotWarn()
        {
            var csCode = @"
System.Console.WriteLine();

public partial class Program
{
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources = { csCode, },
                },
                FixedCode = csCode,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            }.RunAsync();
        }
    }
}
