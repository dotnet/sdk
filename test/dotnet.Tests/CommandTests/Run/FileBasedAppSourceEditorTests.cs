// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class FileBasedAppSourceEditorTests(ITestOutputHelper log) : SdkTest(log)
{
    private static FileBasedAppSourceEditor CreateEditor(string source)
    {
        return FileBasedAppSourceEditor.Load(new SourceFile("/app/Program.cs", SourceText.From(source, Encoding.UTF8)));
    }

    [Theory]
    [InlineData("#:package MyPackage@1.0.1")]
    [InlineData("#:package   MyPackage @ abc")]
    [InlineData("#:package MYPACKAGE")]
    public void ReplaceExisting(string inputLine)
    {
        Verify(
            $"""
            {inputLine}
            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0
            Console.WriteLine();
            """));
    }

    [Fact]
    public void OnlyStatement()
    {
        Verify(
            """
            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            Console.WriteLine();
            """));
    }

    [Theory]
    [InlineData("// only comment")]
    [InlineData("/* only comment */")]
    public void OnlyComment(string comment)
    {
        Verify(
            comment,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            $"""
            {comment}

            #:package MyPackage@1.0.0

            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            $"""
            {comment}


            """));
    }

    [Fact]
    public void Empty()
    {
        Verify(
            "",
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0

            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            ""));
    }

    [Fact]
    public void PreExistingWhiteSpace()
    {
        Verify(
            """


            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0


            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            Console.WriteLine();
            """));
    }

    [Fact]
    public void Comments()
    {
        Verify(
            """
            // Comment1a
            // Comment1b

            // Comment2a
            // Comment2b
            Console.WriteLine();
            // Comment3
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            // Comment1a
            // Comment1b

            // Comment2a
            // Comment2b

            #:package MyPackage@1.0.0

            Console.WriteLine();
            // Comment3
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            // Comment1a
            // Comment1b
            
            // Comment2a
            // Comment2b

            Console.WriteLine();
            // Comment3
            """));
    }

    [Fact]
    public void CommentsWithWhiteSpaceAfter()
    {
        Verify(
            """
            // Comment


            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            // Comment


            #:package MyPackage@1.0.0

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            // Comment


            Console.WriteLine();
            """));
    }

    [Fact]
    public void Comment_Documentation()
    {
        Verify(
            """
            /// doc comment
            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            /// doc comment

            #:package MyPackage@1.0.0

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            /// doc comment

            Console.WriteLine();
            """));
    }

    [Fact]
    public void Comment_MultiLine()
    {
        Verify(
            """
            /* test */
            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            /* test */

            #:package MyPackage@1.0.0

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            /* test */

            Console.WriteLine();
            """));
    }

    [Fact]
    public void Comment_MultiLine_NoNewLine()
    {
        Verify(
            """
            /* test */Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0

            /* test */Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            /* test */Console.WriteLine();
            """));
    }

    [Fact]
    public void Comment_MultiLine_NoNewLine_Multiple()
    {
        Verify(
            """
            // test
            /* test */Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            // test

            #:package MyPackage@1.0.0

            /* test */Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives.Single()),
            """
            // test

            /* test */Console.WriteLine();
            """));
    }

    [Fact]
    public void Group()
    {
        Verify(
            """
            #:property X=Y
            #:package B@C
            #:project D
            #:package E

            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:property X=Y
            #:package B@C
            #:package MyPackage@1.0.0
            #:project D
            #:package E

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives[2]),
            """
            #:property X=Y
            #:package B@C
            #:project D
            #:package E

            Console.WriteLine();
            """));
    }

    [Fact]
    public void GroupEnd()
    {
        Verify(
            """
            #:property X=Y
            #:package B@C

            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:property X=Y
            #:package B@C
            #:package MyPackage@1.0.0

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives[2]),
            """
            #:property X=Y
            #:package B@C

            Console.WriteLine();
            """));
    }

    [Fact]
    public void GroupWithoutSpace()
    {
        Verify(
            """
            #:package B@C
            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package B@C
            #:package MyPackage@1.0.0
            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives[1]),
            """
            #:package B@C
            Console.WriteLine();
            """));
    }

    [Fact]
    public void OtherDirectives()
    {
        Verify(
            """
            #:property A
            #:project D
            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0
            #:property A
            #:project D
            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives[0]),
            """
            #:property A
            #:project D
            Console.WriteLine();
            """));
    }

    [Fact]
    public void AfterTokens()
    {
        Verify(
            """
            using System;

            #:package A

            Console.WriteLine();
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0

            using System;

            #:package A

            Console.WriteLine();
            """),
            (static editor => editor.Remove(editor.Directives[0]),
            """
            using System;

            #:package A

            Console.WriteLine();
            """));
    }

    [Fact]
    public void SkippedTokensTrivia()
    {
        Verify(
            """
            #if false
            Test
            #endif
            """,
            (static editor => editor.Add(new CSharpDirective.Package(default) { Name = "MyPackage", Version = "1.0.0" }),
            """
            #:package MyPackage@1.0.0

            #if false
            Test
            #endif
            """),
            (static editor => editor.Remove(editor.Directives[0]),
            """
            #if false
            Test
            #endif
            """));
    }

    [Fact]
    public void RemoveMultiple()
    {
        Verify(
            """
            #:package Humanizer@2.14.1
            #:property X=Y
            #:package Humanizer@2.9.9

            Console.WriteLine();
            """,
            (static editor =>
            {
                editor.Remove(editor.Directives.OfType<CSharpDirective.Package>().First());
                editor.Remove(editor.Directives.OfType<CSharpDirective.Package>().First());
            },
            """
            #:property X=Y

            Console.WriteLine();
            """));
    }

    private void Verify(
        string input,
        params ReadOnlySpan<(Action<FileBasedAppSourceEditor> action, string expectedOutput)> verify)
    {
        var editor = CreateEditor(input);
        int index = 0;
        foreach (var (action, expectedOutput) in verify)
        {
            action(editor);
            var actualOutput = editor.SourceFile.Text.ToString();
            if (actualOutput != expectedOutput)
            {
                Log.WriteLine("Expected output:\n---");
                Log.WriteLine(expectedOutput);
                Log.WriteLine("---\nActual output:\n---");
                Log.WriteLine(actualOutput);
                Log.WriteLine("---");
                Assert.Fail($"Output mismatch at index {index}.");
            }
            index++;
        }
    }
}
