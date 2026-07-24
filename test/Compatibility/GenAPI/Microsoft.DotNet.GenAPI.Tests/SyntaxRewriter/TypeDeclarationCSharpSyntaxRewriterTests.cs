// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.GenAPI.SyntaxRewriter;

namespace Microsoft.DotNet.GenAPI.Tests.SyntaxRewriter
{
    [TestClass]
    public class TypeDeclarationCSharpSyntaxRewriterTests : CSharpSyntaxRewriterTestBase
    {
        [TestMethod]
        public void TestRemoveSystemObjectAsBaseClass()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(addPartialModifier: true),
                original: """
                namespace A
                {
                    class B : global::System.Object
                    {
                    }
                }
                """,
                expected: """
                namespace A
                {
                    partial class B
                    {
                    }
                }
                """);
        }

        [TestMethod]
        public void TestAddPartialKeyword()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(addPartialModifier: true),
                original: """
                namespace A
                {
                    class B { }
                    struct C { }
                    interface D { }
                }
                """,
                expected: """
                namespace A
                {
                    partial class B { }
                    partial struct C { }
                    partial interface D { }
                }
                """);
        }

        [TestMethod]
        public void TestPartialTypeDeclaration()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(addPartialModifier: true),
                original: """
                namespace A
                {
                    partial class B { }
                    partial struct C { }
                    partial interface D { }
                }
                """,
                expected: """
                namespace A
                {
                    partial class B { }
                    partial struct C { }
                    partial interface D { }
                }
                """);
        }
    }
}
