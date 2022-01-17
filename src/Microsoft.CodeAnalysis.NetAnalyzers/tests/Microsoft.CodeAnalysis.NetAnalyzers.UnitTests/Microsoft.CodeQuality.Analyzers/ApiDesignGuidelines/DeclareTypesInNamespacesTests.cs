// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
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
        public async Task OuterTypeInGlobalNamespace_WarnsAsync()
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
        public async Task NestedTypeInGlobalNamespace_WarnsOnlyOnceAsync()
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
        public async Task InternalClassInGlobalNamespace_DoesNotWarnAsync()
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
        public async Task PublicClassInNonGlobalNamespace_DoesNotWarnAsync()
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
    }
}