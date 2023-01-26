// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Xunit;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.GenAPI.SyntaxRewriter;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Tests
{
    public class SyntaxRewriterTests
    {
        protected void Compare<T>(string original, string expected, params object?[] p) where T : CSharpSyntaxRewriter
        {
            StringWriter _stringWriter = new();
            CSharpSyntaxTree.ParseText(original)
                .GetRoot()
                .Rewrite<T>(p)
                .WriteTo(_stringWriter);

            StringBuilder stringBuilder = _stringWriter.GetStringBuilder();
            var resulted = stringBuilder.ToString();

            Assert.True(resulted.Equals(expected),
                $"Expected:\n{expected}\nResulted:\n{resulted}");
        }

        protected void CompareSyntaxTree<T>(string original, string expected, params object?[] p) where T : CSharpSyntaxRewriter
        {
            StringWriter _stringWriter = new();
            CSharpSyntaxTree.ParseText(original)
                .GetRoot()
                .Rewrite<T>(p)
                .WriteTo(_stringWriter);

            StringBuilder stringBuilder = _stringWriter.GetStringBuilder();
            var resulted = stringBuilder.ToString();

            SyntaxTree resultedSyntaxTree = CSharpSyntaxTree.ParseText(resulted);
            SyntaxTree expectedSyntaxTree = CSharpSyntaxTree.ParseText(expected);

            /// compare SyntaxTree and not string representation
            Assert.True(resultedSyntaxTree.IsEquivalentTo(expectedSyntaxTree),
                $"Expected:\n{expected}\nResulted:\n{resulted}");
        }
    }

    public class OneLineStatementSyntaxRewriterTests : SyntaxRewriterTests
    {
        [Fact]
        public void TestEmptyMethodBody()
        {
            Compare<OneLineStatementSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        void Execute() {}
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        void Execute() {}
                    }
                }
                """);
        }

        [Fact]
        public void TestMethodBodyWithSingleStatement()
        {
            Compare<OneLineStatementSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        int Execute() {
                            throw null;
                        }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int Execute() { throw null; }
                    }
                }
                """);
        }

        [Fact]
        public void TestMethodBodyWithSingleStatementInOneLine()
        {
            Compare<OneLineStatementSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        int Execute() { throw null; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int Execute() { throw null; }
                    }
                }
                """);
        }
    }

    public class TypeDeclarationSyntaxRewriterTests : SyntaxRewriterTests
    {
       [Fact]
        public void TestRemoveSystemObjectAsBaseClass()
        {
            CompareSyntaxTree<TypeDeclarationSyntaxRewriter>(original: """
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

        [Fact]
        public void TestAddPartialKeyword()
        {
            CompareSyntaxTree<TypeDeclarationSyntaxRewriter>(original: """
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

        [Fact]
        public void TestPartialTypeDeclaration()
        {
            CompareSyntaxTree<TypeDeclarationSyntaxRewriter>(original: """
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

    public class BodySyntaxRewriterTests : SyntaxRewriterTests
    {
        [Fact]
        public void TestMethodDeclaration()
        {
            string? exceptionMessage = null;
            CompareSyntaxTree<BodyBlockSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { return 1; }
                        abstract int M3;
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { throw null; }
                        abstract int M3;
                    }
                }
                """,
                exceptionMessage);
        }

        [Fact]
        public void TestMethodDeclarationWithExceptionMessage()
        {
            string? exceptionMessage = "Not implemented";
            CompareSyntaxTree<BodyBlockSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { return 1; }
                        abstract int M3;
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        void M1() {}
                        int M2() { throw new PlatformNotSupportedException("Not implemented"); }
                        abstract int M3;
                    }
                }
                """,
                exceptionMessage);
        }

        [Fact]
        public void TestPropertyDeclaration()
        {
            string? exceptionMessage = null;
            CompareSyntaxTree<BodyBlockSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        int P1 { get; set; }
                        int P2 { get; }
                        int P3 { set; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int P1 { get { throw null; } set { } }
                        int P2 { get { throw null; } }
                        int P3 { set { } }
                    }
                }
                """,
                exceptionMessage);
        }

        [Fact]
        public void TestPropertyDeclarationWithExceptionMessage()
        {
            string? exceptionMessage = "Not implemented";
            CompareSyntaxTree<BodyBlockSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        int P1 { get; set; }
                        int P2 { get; }
                        int P3 { set; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        int P1 { get { throw new PlatformNotSupportedException("Not implemented"); } set { } }
                        int P2 { get { throw new PlatformNotSupportedException("Not implemented"); } }
                        int P3 { set { } }
                    }
                }
                """,
                exceptionMessage);
        }

        [Fact]
        public void TestCustomOperatorDeclaration()
        {
            string? exceptionMessage = null;
            CompareSyntaxTree<BodyBlockSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { return true; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { throw null; }
                    }
                }
                """,
                exceptionMessage);
        }

        [Fact]
        public void TestCustomOperatorDeclarationWithExceptionMessage()
        {
            string? exceptionMessage = "Not implemented";
            CompareSyntaxTree<BodyBlockSyntaxRewriter>(original: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { return true; }
                    }
                }
                """,
                expected: """
                namespace A
                {
                    class B
                    {
                        public static bool operator ==(B lhs, B rhs) { throw new PlatformNotSupportedException("Not implemented"); }
                    }
                }
                """,
                exceptionMessage);
        }
    }

    public class FieldDeclarationSyntaxRewriterTests : SyntaxRewriterTests
    {
        [Fact]
        public void TestConstantFieldGeneration()
        {
            CompareSyntaxTree<FieldDeclarationSyntaxRewriter>(original: """
                namespace Foo
                {
                    class Bar
                    {
                        public static const int CurrentEra = 0;
                    }
                }
                """,
                expected: """
                namespace Foo
                {
                    class Bar
                    {
                        public const int CurrentEra = 0;
                    }
                }
                """);
        }
    }
}
