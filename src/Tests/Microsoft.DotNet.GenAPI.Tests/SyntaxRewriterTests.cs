﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
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
        protected void Compare(CSharpSyntaxRewriter rewriter, string original, string expected)
        {
            StringWriter _stringWriter = new();
            SyntaxNode root = CSharpSyntaxTree.ParseText(original).GetRoot();
            rewriter.Visit(root)
                .WriteTo(_stringWriter);

            StringBuilder stringBuilder = _stringWriter.GetStringBuilder();
            var resulted = stringBuilder.ToString();

            Assert.True(resulted.Equals(expected),
                $"Expected:\n{expected}\nResulted:\n{resulted}");
        }

        protected void CompareSyntaxTree(CSharpSyntaxRewriter rewriter, string original, string expected)
        {
            StringWriter _stringWriter = new();
            SyntaxNode root = CSharpSyntaxTree.ParseText(original).GetRoot();
            rewriter.Visit(root)
                .WriteTo(_stringWriter);

            StringBuilder stringBuilder = _stringWriter.GetStringBuilder();
            var resulted = stringBuilder.ToString();

            SyntaxTree resultedSyntaxTree = CSharpSyntaxTree.ParseText(resulted);
            SyntaxTree expectedSyntaxTree = CSharpSyntaxTree.ParseText(expected);

            // compare SyntaxTree and not string representation
            Assert.True(resultedSyntaxTree.IsEquivalentTo(expectedSyntaxTree),
                $"Expected:\n{expected}\nResulted:\n{resulted}");
        }
    }

    public class SingleLineStatementCSharpSyntaxRewriterTests : SyntaxRewriterTests
    {
        [Fact]
        public void TestEmptyMethodBody()
        {
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
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
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
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
            Compare(new SingleLineStatementCSharpSyntaxRewriter(),
                original: """
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
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(),
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

        [Fact]
        public void TestAddPartialKeyword()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(),
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

        [Fact]
        public void TestPartialTypeDeclaration()
        {
            CompareSyntaxTree(new TypeDeclarationCSharpSyntaxRewriter(),
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

    public class BodySyntaxRewriterTests : SyntaxRewriterTests
    {
        [Fact]
        public void TestMethodDeclaration()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter(null),
                original: """
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
                """);
        }

        [Fact]
        public void TestMethodDeclarationWithExceptionMessage()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
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
                """);
        }

        [Fact]
        public void TestPropertyDeclaration()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter(null),
                original: """
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
                """);
        }

        [Fact]
        public void TestPropertyDeclarationWithExceptionMessage()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
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
                """);
        }

        [Fact]
        public void TestCustomOperatorDeclaration()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter(null),
                original: """
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
                """);
        }

        [Fact]
        public void TestCustomOperatorDeclarationWithExceptionMessage()
        {
            CompareSyntaxTree(new BodyBlockCSharpSyntaxRewriter("Not implemented"),
                original: """
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
                """);
        }
    }

    public class FieldDeclarationSyntaxRewriterTests : SyntaxRewriterTests
    {
        [Fact]
        public void TestConstantFieldGeneration()
        {
            CompareSyntaxTree(new FieldDeclarationCSharpSyntaxRewriter(),
                original: """
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
