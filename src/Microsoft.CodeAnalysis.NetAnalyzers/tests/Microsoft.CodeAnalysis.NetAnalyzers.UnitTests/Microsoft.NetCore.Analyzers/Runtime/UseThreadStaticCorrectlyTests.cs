// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseThreadStaticCorrectly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseThreadStaticCorrectly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseThreadStaticsCorrectlyTests
    {
        [Theory]
        [InlineData("public", "object")]
        [InlineData("public", "int")]
        [InlineData("internal", "object")]
        [InlineData("internal", "int")]
        [InlineData("private", "object")]
        [InlineData("private", "int")]
        [InlineData("", "object")]
        [InlineData("", "int")]
        public async Task ValidThreadStatic_NoDiagnostics_CSharp(string visibility, string type)
        {
            await VerifyCS.VerifyAnalyzerAsync(
                @$"
                using System;

                class C
                {{
                    [ThreadStatic]
                    {visibility} static {type} t_value;

                    [field: ThreadStatic]
                    {visibility} static {type} Prop {{ get; set; }}

                    [field: ThreadStatic]
                    {visibility} static event EventHandler Ev;
                }}
                ");
        }

        [Theory]
        [InlineData("Public", "Object")]
        [InlineData("Public", "Integer")]
        [InlineData("Friend", "Object")]
        [InlineData("Friend", "Integer")]
        [InlineData("Private", "Object")]
        [InlineData("Private", "Integer")]
        [InlineData("", "Object")]
        [InlineData("", "Integer")]
        public async Task ValidThreadStatic_NoDiagnostics_VB(string visibility, string type)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                @$"
                Imports System

                Class C
                    <ThreadStatic>
                    {visibility} Shared t_value As {type}
                End Class
                ");
        }

        [Theory]
        [InlineData("public", "object")]
        [InlineData("public", "int")]
        [InlineData("internal", "object")]
        [InlineData("internal", "int")]
        [InlineData("private", "object")]
        [InlineData("private", "int")]
        [InlineData("", "object")]
        [InlineData("", "int")]
        public async Task InstanceField_Diagnostic_CSharp(string visibility, string type)
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @$"
                using System;

                class C
                {{
                    [ThreadStatic]
                    {visibility} {type} {{|CA2259:t_value|}};

                    [field: ThreadStatic]
                    string {{|CA2259:Prop|}} {{ get; set; }}
                }}

                record R([field: ThreadStatic] string {{|CA2259:Value|}});
                "
            }.RunAsync();
        }

        [Theory]
        [InlineData("Public", "Object")]
        [InlineData("Public", "Integer")]
        [InlineData("Friend", "Object")]
        [InlineData("Friend", "Integer")]
        [InlineData("Private", "Object")]
        [InlineData("Private", "Integer")]
        public async Task InstanceField_Diagnostic_VB(string visibility, string type)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                @$"
                Imports System

                Class C
                    <ThreadStatic>
                    {visibility} {{|CA2259:t_value|}} As {type}
                End Class
                ");
        }

        [Theory]
        [InlineData("object", "new object()")]
        [InlineData("object", "default")]
        [InlineData("object", "null")]
        [InlineData("int", "42")]
        [InlineData("int", "default")]
        [InlineData("int", "0")]
        public async Task FieldInitializer_Diagnostic_CSharp(string type, string initializer)
        {
            await VerifyCS.VerifyAnalyzerAsync(
                @$"
                using System;

                class C
                {{
                    [ThreadStatic]
                    private static {type} t_value {{|CA2019:= {initializer}|}};
                }}
                ");
        }

        [Theory]
        [InlineData("Object", "New Object()")]
        [InlineData("Object", "Nothing")]
        [InlineData("Integer", "42")]
        [InlineData("Integer", "0")]
        public async Task FieldInitializer_Diagnostic_VB(string type, string initializer)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                @$"
                Imports System

                Class C
                    <ThreadStatic>
                    Private Shared t_value As {type} {{|CA2019:= {initializer}|}}
                End Class
                ");
        }
    }
}