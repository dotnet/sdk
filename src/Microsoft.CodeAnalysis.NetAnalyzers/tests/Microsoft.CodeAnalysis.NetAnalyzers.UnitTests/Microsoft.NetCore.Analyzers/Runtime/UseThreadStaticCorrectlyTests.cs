// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseThreadStaticCorrectly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseThreadStaticCorrectly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class UseThreadStaticsCorrectlyTests
    {
        [TestMethod]
        [DataRow("public", "object")]
        [DataRow("public", "int")]
        [DataRow("internal", "object")]
        [DataRow("internal", "int")]
        [DataRow("private", "object")]
        [DataRow("private", "int")]
        [DataRow("", "object")]
        [DataRow("", "int")]
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

        [TestMethod]
        [DataRow("Public", "Object")]
        [DataRow("Public", "Integer")]
        [DataRow("Friend", "Object")]
        [DataRow("Friend", "Integer")]
        [DataRow("Private", "Object")]
        [DataRow("Private", "Integer")]
        [DataRow("", "Object")]
        [DataRow("", "Integer")]
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

        [TestMethod]
        [DataRow("public", "object")]
        [DataRow("public", "int")]
        [DataRow("internal", "object")]
        [DataRow("internal", "int")]
        [DataRow("private", "object")]
        [DataRow("private", "int")]
        [DataRow("", "object")]
        [DataRow("", "int")]
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("Public", "Object")]
        [DataRow("Public", "Integer")]
        [DataRow("Friend", "Object")]
        [DataRow("Friend", "Integer")]
        [DataRow("Private", "Object")]
        [DataRow("Private", "Integer")]
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

        [TestMethod]
        [DataRow("object", "new object()")]
        [DataRow("object", "default")]
        [DataRow("object", "null")]
        [DataRow("int", "42")]
        [DataRow("int", "default")]
        [DataRow("int", "0")]
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

        [TestMethod]
        [DataRow("Object", "New Object()")]
        [DataRow("Object", "Nothing")]
        [DataRow("Integer", "42")]
        [DataRow("Integer", "0")]
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