// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequested,
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequestedFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequested,
    Microsoft.NetCore.Analyzers.Runtime.UseCancellationTokenThrowIfCancellationRequestedFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseCancellationTokenThrowIfCancellationRequestedTests
    {
        public static IEnumerable<object[]> Data_OperationCanceledExceptionCtors
        {
            get
            {
                yield return new[] { "OperationCanceledException()" };
                yield return new[] { "OperationCanceledException(token)" };
            }
        }

        #region Reports Diagnostics
        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtors))]
        public Task SimpleAffirmativeCheck_ReportedAndFixed_CS(string operationCanceledExceptionCtor)
        {
            string testStatements = Markup($@"
if (token.IsCancellationRequested)
    throw new {operationCanceledExceptionCtor};", 0);
            string fixedStatements = @"
token.ThrowIfCancellationRequested();";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements),
                FixedCode = CS.CreateBlock(fixedStatements),
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtors))]
        public Task SimpleAffirmativeCheck_ReportedAndFixed_VB(string operationCanceledExceptionCtor)
        {
            string testStatements = Markup($@"
If token.IsCancellationRequested Then
    Throw New {operationCanceledExceptionCtor}
End If", 0);
            string fixedStatements = @"
token.ThrowIfCancellationRequested()";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements),
                FixedCode = VB.CreateBlock(fixedStatements),
                ExpectedDiagnostics = { VB.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtors))]
        public Task NegatedCheckWithElse_ReportedAndFixed_CS(string operationCanceledExceptionCtor)
        {
            const string members = @"
private CancellationToken token;
private void DoSomething() { }";
            string testStatements = Markup($@"
if (!token.IsCancellationRequested)
    DoSomething();
else
    throw new {operationCanceledExceptionCtor};", 0);
            string fixedStatements = @"
token.ThrowIfCancellationRequested();
DoSomething();";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements, members),
                FixedCode = CS.CreateBlock(fixedStatements, members),
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtors))]
        public Task NegatedCheckWithElse_ReportedAndFixed_VB(string operationCanceledExceptionCtor)
        {
            const string members = @"
Private token As CancellationToken
Private Sub DoSomething()
End Sub";
            string testStatements = Markup($@"
If Not token.IsCancellationRequested Then
    DoSomething()
Else
    Throw New {operationCanceledExceptionCtor}
End If", 0);
            string fixedStatements = @"
token.ThrowIfCancellationRequested()
DoSomething()";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements, members),
                FixedCode = VB.CreateBlock(fixedStatements, members),
                ExpectedDiagnostics = { VB.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }
        #endregion

        #region Helpers
        private static class CS
        {
            public const string Usings = @"
using System;
using System.Threading;";

            public static string CreateBlock(string statements, string members)
            {
                return Usings + @"
public partial class Body
{
" + IndentLines(members, "    ") + @"
    public void Run()
    {
" + IndentLines(statements, "        ") + @"
    }
}";
            }

            /// <summary>
            /// Creates a test class with a single private CancellationToken member called 'token'.
            /// </summary>
            public static string CreateBlock(string statements) => CreateBlock(statements, @"private CancellationToken token;");

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyCS.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static class VB
        {
            public const string Usings = @"
Imports System
Imports System.Threading";

            public static string CreateBlock(string statements, string members)
            {
                return Usings + @"
Partial Public Class Body

" + IndentLines(members, "    ") + @"
    Public Sub Run()

" + IndentLines(statements, "        ") + @"
    End Sub
End Class";
            }

            public static string CreateBlock(string statements) => CreateBlock(statements, @"Private token As CancellationToken");

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyVB.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static string IndentLines(string lines, string indent)
        {
            return indent + lines.TrimStart().Replace(Environment.NewLine, indent + Environment.NewLine, StringComparison.Ordinal);
        }

        private static string Markup(string text, int markupKey, bool removeLeadingWhitespace = true)
        {
            text = removeLeadingWhitespace ? text.TrimStart() : text;
            return $"{{|#{markupKey}:{text}|}}";
        }

        private static DiagnosticDescriptor Rule => UseCancellationTokenThrowIfCancellationRequested.Rule;
        #endregion
    }
}
