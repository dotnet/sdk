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
        public async Task OuterTypeInGlobalNamespace_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                }",
                GetCSharpExpectedResult(2, 30));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                End Class",
                GetBasicExpectedResult(2, 30));
        }

        [Fact]
        public async Task NestedTypeInGlobalNamespace_WarnsOnlyOnce()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public class Nested {}
                }",
                GetCSharpExpectedResult(2, 30));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Class Nested
                    End Class
                End Class",
                GetBasicExpectedResult(2, 30));
        }

        [Fact]
        public async Task InternalClassInGlobalNamespace_DoesNotWarn()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                internal class Class
                {
                    public class Nested {}
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Friend Class [MyClass]
                    Public Class Nested
                    End Class
                End Class");
        }

        [Fact]
        public async Task PublicClassInNonGlobalNamespace_DoesNotWarn()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                namespace NS
                {
                    public class Class
                    {
                        public class Nested {}
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Namespace NS
                    Public Class [MyClass]
                        Public Class Nested
                        End Class
                    End Class
                End Namespace");
        }

        private static DiagnosticResult GetCSharpExpectedResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicExpectedResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}