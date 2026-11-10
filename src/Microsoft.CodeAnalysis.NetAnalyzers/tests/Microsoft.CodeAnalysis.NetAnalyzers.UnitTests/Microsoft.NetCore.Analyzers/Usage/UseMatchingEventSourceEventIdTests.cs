// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    using VerifyCS = CSharpCodeFixVerifier<UseMatchingEventSourceEventIdAnalyzer, UseMatchingEventSourceEventIdFixer>;
    using VerifyVB = VisualBasicCodeFixVerifier<UseMatchingEventSourceEventIdAnalyzer, UseMatchingEventSourceEventIdFixer>;

    [TestClass]
    public sealed class UseMatchingEventSourceEventIdTests
    {
        private static DiagnosticResult CSResult(int markup, int expected, int actual)
            => VerifyCS.Diagnostic().WithLocation(markup).WithArguments(expected, actual);

        private static DiagnosticResult VBResult(int markup, int expected, int actual)
            => VerifyVB.Diagnostic().WithLocation(markup).WithArguments(expected, actual);

        [TestMethod]
        public async Task MismatchedLiteral_Diagnostic_CS()
        {
            await VerifyCS.VerifyCodeFixAsync("""
                using System.Diagnostics.Tracing;

                class MySource : EventSource
                {
                    [Event(1)]
                    public void Started(string name) => WriteEvent({|#0:2|}, name);

                    [Event(2)]
                    public void Stopped() { this.WriteEvent({|#1:3|}); }

                    [Event(3)]
                    public void Failed(int code) { base.WriteEvent(/* id */ {|#2:4|} /* end */, code); }
                }
                """,
                new[] { CSResult(0, 1, 2), CSResult(1, 2, 3), CSResult(2, 3, 4) },
                """
                using System.Diagnostics.Tracing;

                class MySource : EventSource
                {
                    [Event(1)]
                    public void Started(string name) => WriteEvent(1, name);

                    [Event(2)]
                    public void Stopped() { this.WriteEvent(2); }

                    [Event(3)]
                    public void Failed(int code) { base.WriteEvent(/* id */ 3 /* end */, code); }
                }
                """);
        }

        [TestMethod]
        public async Task NamedArgumentAndConstants_Diagnostic_CS()
        {
            await VerifyCS.VerifyCodeFixAsync("""
                using System.Diagnostics.Tracing;

                class MySource : EventSource
                {
                    private const int StartedId = 1;
                    private const int StoppedId = 2;

                    [Event(StartedId)]
                    public void Started(string name) => WriteEvent(eventId: {|#0:StoppedId|}, arg1: name);

                    [Event(StoppedId)]
                    public void Stopped() => WriteEvent({|#1:StartedId + 2|});
                }
                """,
                new[] { CSResult(0, 1, 2), CSResult(1, 2, 3) },
                """
                using System.Diagnostics.Tracing;

                class MySource : EventSource
                {
                    private const int StartedId = 1;
                    private const int StoppedId = 2;

                    [Event(StartedId)]
                    public void Started(string name) => WriteEvent(eventId: 1, arg1: name);

                    [Event(StoppedId)]
                    public void Stopped() => WriteEvent(2);
                }
                """);
        }

        [TestMethod]
        public async Task IndirectInheritance_Diagnostic_CS()
        {
            await VerifyCS.VerifyCodeFixAsync("""
                using System.Diagnostics.Tracing;

                abstract class BaseSource : EventSource { }

                sealed class MySource : BaseSource
                {
                    [Event(5)]
                    public void Started() => WriteEvent({|#0:6|});
                }
                """,
                CSResult(0, 5, 6),
                """
                using System.Diagnostics.Tracing;

                abstract class BaseSource : EventSource { }

                sealed class MySource : BaseSource
                {
                    [Event(5)]
                    public void Started() => WriteEvent(5);
                }
                """);
        }

        [TestMethod]
        public async Task NoDiagnosticCases_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Diagnostics.Tracing;

                class MySource : EventSource
                {
                    public static readonly MySource Log = new MySource();
                    private int _id = 2;

                    [Event(1)]
                    public void Matching(string name) => WriteEvent(1, name);

                    [Event(2)]
                    public void Computed() => WriteEvent(_id);

                    [Event(3)]
                    public void OtherInstance() => Log.WriteEvent(4);

                    [Event(4)]
                    public void InLambda()
                    {
                        Action a = () => WriteEvent(5);
                        a();
                    }

                    [Event(5)]
                    public void InLocalFunction()
                    {
                        Local();
                        void Local() => WriteEvent(6);
                    }

                    [Event(6)]
                    public void ViaHelper() => Helper(7);

                    private void Helper(int id) => WriteEvent(id);

                    public void NoAttribute() => WriteEvent(8);

                    [NonEvent]
                    public void NonEventMethod() => WriteEvent(9);
                }

                class NotAnEventSource
                {
                    [Event(1)]
                    public void Started() => WriteEvent(2);

                    private void WriteEvent(int eventId) { }
                }
                """);
        }

        [TestMethod]
        public async Task HiddenWriteEvent_NoDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Diagnostics.Tracing;

                class MySource : EventSource
                {
                    [Event(1)]
                    public void Started() => WriteEvent(2);

                    private new void WriteEvent(int eventId) { }
                }
                """);
        }

        [TestMethod]
        public async Task MismatchedLiteral_Diagnostic_VB()
        {
            await VerifyVB.VerifyCodeFixAsync("""
                Imports System.Diagnostics.Tracing

                Class MySource
                    Inherits EventSource

                    <[Event](1)>
                    Public Sub Started(name As String)
                        WriteEvent({|#0:2|}, name)
                    End Sub

                    <[Event](2)>
                    Public Sub Stopped()
                        Me.WriteEvent(eventId:={|#1:3|})
                    End Sub

                    <[Event](3)>
                    Public Sub Failed(code As Integer)
                        MyBase.WriteEvent({|#2:4|}, code)
                    End Sub
                End Class
                """,
                new[] { VBResult(0, 1, 2), VBResult(1, 2, 3), VBResult(2, 3, 4) },
                """
                Imports System.Diagnostics.Tracing

                Class MySource
                    Inherits EventSource

                    <[Event](1)>
                    Public Sub Started(name As String)
                        WriteEvent(1, name)
                    End Sub

                    <[Event](2)>
                    Public Sub Stopped()
                        Me.WriteEvent(eventId:=2)
                    End Sub

                    <[Event](3)>
                    Public Sub Failed(code As Integer)
                        MyBase.WriteEvent(3, code)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task NoDiagnosticCases_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System
                Imports System.Diagnostics.Tracing

                Class MySource
                    Inherits EventSource

                    Public Shared ReadOnly Log As New MySource()
                    Private _id As Integer = 2

                    <[Event](1)>
                    Public Sub Matching()
                        WriteEvent(1)
                    End Sub

                    <[Event](2)>
                    Public Sub Computed()
                        WriteEvent(_id)
                    End Sub

                    <[Event](3)>
                    Public Sub OtherInstance()
                        Log.WriteEvent(4)
                    End Sub

                    <[Event](4)>
                    Public Sub InLambda()
                        Dim a As Action = Sub() WriteEvent(5)
                        a()
                    End Sub

                    Public Sub NoAttribute()
                        WriteEvent(8)
                    End Sub
                End Class
                """);
        }
    }
}
