// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferAsSpanOverSubstringFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicPreferAsSpanOverSubstringFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferAsSpanOverSubstringTests
    {
        #region Reports Diagnostic
        [Theory]
        [InlineData(@"Substring(1)", @"AsSpan(1)")]
        [InlineData(@"Substring(1, 2)", @"AsSpan(1, 2)")]
        [InlineData(@"Substring(startIndex: 1)", @"AsSpan(start: 1)")]
        [InlineData(@"Substring(startIndex: 1, 2)", @"AsSpan(start: 1, 2)")]
        [InlineData(@"Substring(1, length: 2)", @"AsSpan(1, length: 2)")]
        [InlineData(@"Substring(startIndex: 1, length: 2)", @"AsSpan(start: 1, length: 2)")]
        [InlineData(@"Substring(length: 2, startIndex: 1)", @"AsSpan(length: 2, start: 1)")]
        public Task SingleArgumentStaticMethod_ReportsDiagnostic_CS(string substring, string asSpan)
        {
            string thing = @"
using System;

public class Thing
{
    public static void Consume(string text) { }
    public static void Consume(ReadOnlySpan<char> span) { }
}";
            string testStatements = WithKey($"Thing.Consume(foo.{substring})", 0) + ';';
            string fixedStatements = $"Thing.Consume(foo.{asSpan});";

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { thing, CS.WithBody(testStatements) },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { thing, CS.WithBody(fixedStatements) }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"Substring(1)", @"AsSpan(1)")]
        [InlineData(@"Substring(1, 2)", @"AsSpan(1, 2)")]
        [InlineData(@"Substring(startIndex:=1)", @"AsSpan(start:=1)")]
        [InlineData(@"Substring(startIndex:=1, 2)", @"AsSpan(start:=1, 2)")]
        [InlineData(@"Substring(1, length:=2)", @"AsSpan(1, length:=2)")]
        [InlineData(@"Substring(startIndex:=1, length:=2)", @"AsSpan(start:=1, length:=2)")]
        [InlineData(@"Substring(length:=2, startIndex:=1)", @"AsSpan(length:=2, start:=1)")]
        public Task SingleArgumentStaticMethod_ReportsDiagnostic_VB(string substring, string asSpan)
        {
            string thing = @"
using System;

public class Thing
{
    public static void Consume(string text) { }
    public static void Consume(ReadOnlySpan<char> span) { }
}";
            var thingProject = new ProjectState("ThingProject", LanguageNames.CSharp, "thing", "cs")
            {
                Sources = { thing }
            };
            string testStatements = WithKey($"Thing.Consume(foo.{substring})", 0);
            string fixedStatements = $"Thing.Consume(foo.{asSpan})";

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { VB.WithBody(testStatements) },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { VB.WithBody(fixedStatements) },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }
        #endregion

        #region Helpers
        private static class CS
        {
            public static string WithBody(string statements, bool includeUsings = true)
            {
                const string indent = "        ";
                string indentedStatements = indent + statements.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent);
                string usings = includeUsings ? $"using System;{Environment.NewLine}" : string.Empty;

                return $@"
{usings}
public class Program
{{
    private static void Run(string foo)
    {{
{indentedStatements}
    }}
}}";
            }

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyCS.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static class VB
        {
            public static string WithBody(string statements, bool includeImports = true)
            {
                const string indent = "        ";
                string indentedStatements = indent + statements.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent);
                string imports = includeImports ? $"Imports System{Environment.NewLine}" : string.Empty;

                return $@"
{imports}
Public Class Program

    Private Sub Run(foo As String)

{indentedStatements}
    End Sub
End Class";
            }

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyVB.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static string WithKey(string text, int markupKey) => $"{{|#{markupKey}:{text}|}}";
        private static DiagnosticDescriptor Rule => PreferAsSpanOverSubstring.Rule;
        #endregion
    }
}
