// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private static IEnumerable<string> OperationCanceledExceptionCtors
        {
            get
            {
                yield return "OperationCanceledException()";
                yield return "OperationCanceledException(token)";
            }
        }

        #region Reports Diagnostic
        public static IEnumerable<object[]> Data_SimpleAffirmativeCheck_ReportedAndFixed_CS
        {
            get
            {
                static IEnumerable<string> ConditionalFormatStrings()
                {
                    yield return @"if ({0}) {1}";
                    yield return @"
if ({0})
    {1}";
                    yield return @"
if ({0})
{{
    {1}
}}";
                }

                return CartesianProduct(OperationCanceledExceptionCtors, ConditionalFormatStrings());
            }
        }

        [Theory]
        [MemberData(nameof(Data_SimpleAffirmativeCheck_ReportedAndFixed_CS))]
        public Task SimpleAffirmativeCheck_ReportedAndFixed_CS(string operationCanceledExceptionCtor, string simpleConditionalFormatString)
        {
            string testStatements = Markup(
                FormatInvariant(
                    simpleConditionalFormatString,
                    @"token.IsCancellationRequested",
                    $@"throw new {operationCanceledExceptionCtor};"), 0);
            string fixedStatements = @"token.ThrowIfCancellationRequested();";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements),
                FixedCode = CS.CreateBlock(fixedStatements),
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_SimpleAffirmativeCheck_ReportedAndFixed_VB
        {
            get
            {
                static IEnumerable<string> ConditionalFormatStrings()
                {
                    yield return @"If {0} Then {1}";
                    yield return @"
If {0} Then
    {1}
End If";
                }

                return CartesianProduct(OperationCanceledExceptionCtors, ConditionalFormatStrings());
            }
        }

        [Theory]
        [MemberData(nameof(Data_SimpleAffirmativeCheck_ReportedAndFixed_VB))]
        public Task SimpleAffirmativeCheck_ReportedAndFixed_VB(string operationCanceledExceptionCtor, string conditionalFormatString)
        {
            string testStatements = Markup(
                FormatInvariant(
                    conditionalFormatString,
                    "token.IsCancellationRequested",
                    $"Throw New {operationCanceledExceptionCtor}"),
                0);
            string fixedStatements = @"token.ThrowIfCancellationRequested()";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements),
                FixedCode = VB.CreateBlock(fixedStatements),
                ExpectedDiagnostics = { VB.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NegatedCheckWithElse_ReportedAndFixed_CS
        {
            get
            {
                static IEnumerable<string> ConditionalFormatStrings()
                {
                    yield return @"
if ({0}) {1}
else {2}";
                    yield return @"
if ({0})
    {1}
else
    {2}";
                    yield return @"
if ({0})
{{
    {1}
}}
else
{{
    {2}
}}";
                    yield return @"
if ({0})
    {1}
else
{{
    {2}
}}";
                    yield return @"
if ({0})
{{
    {1}
}}
else
    {2}";
                }

                return CartesianProduct(OperationCanceledExceptionCtors, ConditionalFormatStrings());
            }
        }

        [Fact]
        public Task SimpleAffirmativeCheckWithElseClause_ReportedAndFixed_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Threading;

public class C
{
    private CancellationToken token;

    public void M()
    {
        {|#0:if (token.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
        else
        {
            Frob();
        }|}
    }
    
    private void Frob() { }
}",
                FixedCode = @"
using System;
using System.Threading;

public class C
{
    private CancellationToken token;

    public void M()
    {
        token.ThrowIfCancellationRequested();
        Frob();
    }
    
    private void Frob() { }
}",
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task SimpleAffirmativeCheckWithElseClause_ReportedAndFixed_VB()
        {
            var test = new VerifyVB.Test
            {
                TestCode = @"
Imports System
Imports System.Threading

Public Class C
    Private token As CancellationToken

    Public Sub M()
        {|#0:If token.IsCancellationRequested Then
            Throw New OperationCanceledException()
        Else
            Frob()
        End If|}
    End Sub

    Private Sub Frob()
    End Sub
End Class",
                FixedCode = @"
Imports System
Imports System.Threading

Public Class C
    Private token As CancellationToken

    Public Sub M()
        token.ThrowIfCancellationRequested()
        Frob()
    End Sub

    Private Sub Frob()
    End Sub
End Class",
                ExpectedDiagnostics = { VB.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task TriviaInIfBlock_IsPreserved_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Threading;

public class C
{
    private CancellationToken token;

    public void M()
    {
        {|#0:if (token.IsCancellationRequested)
        {
            // Comment
            throw new OperationCanceledException();
        }|}
    }
}",
                FixedCode = @"
using System;
using System.Threading;

public class C
{
    private CancellationToken token;

    public void M()
    {
        // Comment
        token.ThrowIfCancellationRequested();
    }
}",
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_NegatedCheckWithElse_ReportedAndFixed_CS))]
        public Task NegatedCheckWithElse_ReportedAndFixed_CS(string operationCanceledExceptionCtor, string conditionalFormatString)
        {
            const string members = @"
private CancellationToken token;
private void DoSomething() { }";
            string testStatements = Markup(
                FormatInvariant(
                    conditionalFormatString,
                    "!token.IsCancellationRequested",
                    "DoSomething();",
                    $"throw new {operationCanceledExceptionCtor};"),
                0);
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

        public static IEnumerable<object[]> Data_NegatedCheckWithElse_ReportedAndFixed_VB
        {
            get
            {
                static IEnumerable<string> ConditionalFormatStrings()
                {
                    return Enumerable.Repeat(@"
If {0} Then
    {1}
Else
    {2}
End If", 1);
                }

                return CartesianProduct(OperationCanceledExceptionCtors, ConditionalFormatStrings());
            }
        }

        [Theory]
        [MemberData(nameof(Data_NegatedCheckWithElse_ReportedAndFixed_VB))]
        public Task NegatedCheckWithElse_ReportedAndFixed_VB(string operationCanceledExceptionCtor, string conditionalFormatString)
        {
            const string members = @"
Private token As CancellationToken
Private Sub DoSomething()
End Sub";
            string testStatements = Markup(
                FormatInvariant(
                    conditionalFormatString,
                    "Not token.IsCancellationRequested",
                    "DoSomething()",
                    $"Throw New {operationCanceledExceptionCtor}"),
                0);
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

        [Fact]
        public Task NegatedCheckWithElse_MultipleOperationsInTrueBranch_ReportedAndFixed_CS()
        {
            const string members = @"
private CancellationToken token;
private void Fooble() { }
private void Barble() { }";
            string testStatements = Markup(@"
if (!token.IsCancellationRequested)
{
    Fooble();
    Barble();
}
else
{
    throw new OperationCanceledException();
}", 0);
            string fixedStatements = @"
token.ThrowIfCancellationRequested();
Fooble();
Barble();";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements, members),
                FixedCode = CS.CreateBlock(fixedStatements, members),
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task NegatedCheckWithElse_MultpleOperationsInTrueBranch_ReportedAndFixed_VB()
        {
            const string members = @"
Private token As CancellationToken
Private Sub Fooble()
End Sub
Private Sub Barble()
End Sub";
            string testStatements = Markup(@"
If Not token.IsCancellationRequested Then
    Fooble()
    Barble()
Else
    Throw New OperationCanceledException()
End If", 0);
            string fixedStatements = @"
token.ThrowIfCancellationRequested()
Fooble()
Barble()";

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

        #region No Diagnostic
        [Fact]
        public Task MultipleConditions_NoDiagnostic_CS()
        {
            const string members = @"
private CancellationToken token;
private bool otherCondition;";
            const string testStatements = @"
if (token.IsCancellationRequested && otherCondition)
    throw new OperationCanceledException();";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task MultipleConditions_NoDiagnostic_VB()
        {
            const string members = @"
Private token As CancellationToken
Private otherCondition As Boolean";
            const string testStatements = @"
If token.IsCancellationRequested AndAlso otherCondition Then
    Throw New OperationCanceledException()
End If";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task OtherStatementsInSimpleAffirmativeCheck_NoDiagnostic_CS()
        {
            const string members = @"
private CancellationToken token;
private void SomeOtherAction() { }";
            const string testStatements = @"
if (token.IsCancellationRequested)
{
    SomeOtherAction();
    throw new OperationCanceledException();
}";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task OtherStatementsInSimpleAffirmativeCheck_NoDiagnostic_VB()
        {
            const string members = @"
Private token As CancellationToken
Private Sub SomeOtherAction()
End Sub";
            const string testStatements = @"
If token.IsCancellationRequested Then
    SomeOtherAction()
    Throw New OperationCanceledException()
End If";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_OperationCanceledExceptionCtorArguments
        {
            get
            {
                yield return new[] { "text" };
                yield return new[] { "text, token" };
                yield return new[] { "text, exception" };
                yield return new[] { "text, exception, token" };
            }
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtorArguments))]
        public Task OtherExceptionCtorOverloads_SimpleAffirmativeCheck_NoDiagnostic_CS(string ctorArguments)
        {
            const string members = @"
private CancellationToken token;
private string text;
private Exception exception;";
            string testStatements = @"
if (token.IsCancellationRequested)
    throw new OperationCanceledException(" + ctorArguments + @");";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtorArguments))]
        public Task OtherExceptionCtorOverloads_SimpleAffirmativeCheck_NoDiagnostic_VB(string ctorArguments)
        {
            const string members = @"
Private token As CancellationToken
Private text As String
private exception As Exception";
            string testStatements = @"
If token.IsCancellationRequested Then
    Throw New OperationCanceledException(" + ctorArguments + @")
End If";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtorArguments))]
        public Task OtherExceptionCtorOverloads_NegatedCheckWithElse_NoDiagnostic_CS(string ctorArguments)
        {
            const string members = @"
private CancellationToken token;
private string text;
private Exception exception;
private void DoSomething() { }";
            string testStatements = @"
if (!token.IsCancellationRequested)
    DoSomething();
else
    throw new OperationCanceledException(" + ctorArguments + @");";

            var test = new VerifyCS.Test
            {
                TestCode = CS.CreateBlock(testStatements, members),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_OperationCanceledExceptionCtorArguments))]
        public Task OtherExceptionCtorOverloads_NegatedCheckWithElse_NoDiagnostic_VB(string ctorArguments)
        {
            const string members = @"
Private token As CancellationToken
Private text As String
Private exception As Exception
Private Sub DoSomething()
End Sub";
            string testStatements = @"
If Not token.IsCancellationRequested Then
    DoSomething()
Else
    Throw New OperationCanceledException(" + ctorArguments + @")
End If";

            var test = new VerifyVB.Test
            {
                TestCode = VB.CreateBlock(testStatements, members),
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
            return indent + lines.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent, StringComparison.Ordinal);
        }

        private static string Markup(string text, int markupKey, bool removeLeadingWhitespace = true)
        {
            text = removeLeadingWhitespace ? text.TrimStart() : text;
            return $"{{|#{markupKey}:{text}|}}";
        }

        private static DiagnosticDescriptor Rule => UseCancellationTokenThrowIfCancellationRequested.Rule;

        private static IEnumerable<object[]> CartesianProduct(IEnumerable<object> left, IEnumerable<object> right)
        {
            return left.SelectMany(x => right.Select(y => new[] { x, y }));
        }

        private static string FormatInvariant(string format, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
        #endregion
    }
}
