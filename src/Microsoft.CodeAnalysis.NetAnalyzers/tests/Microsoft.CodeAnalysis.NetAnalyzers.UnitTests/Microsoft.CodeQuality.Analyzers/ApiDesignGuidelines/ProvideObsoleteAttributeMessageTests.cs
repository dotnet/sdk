// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class ProvideObsoleteAttributeMessageTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ProvideObsoleteAttributeMessageAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ProvideObsoleteAttributeMessageAnalyzer();
        }

        [Fact]
        public void CSharpSimpleCases()
        {
            VerifyCSharp(@"
using System;

[Obsolete]
public class A
{
    [Obsolete]
    public A() { }
    [Obsolete("""")]
    public int field;
    [Obsolete]
    public int Property { get; set; }
    [Obsolete]
    public void Method() {}
    [Obsolete]
    public event EventHandler<int> event1;
}
[Obsolete]
public interface I {}
[Obsolete]
public delegate void del(int x);
",
            GetCSharpResultAt(4, 2, "A"),
            GetCSharpResultAt(7, 6, ".ctor"),
            GetCSharpResultAt(9, 6, "field"),
            GetCSharpResultAt(11, 6, "Property"),
            GetCSharpResultAt(13, 6, "Method"),
            GetCSharpResultAt(15, 6, "event1"),
            GetCSharpResultAt(18, 2, "I"),
            GetCSharpResultAt(20, 2, "del"));
        }

        [Fact]
        public void BasicSimpleCases()
        {
            VerifyBasic(@"
Imports System

<Obsolete>
Public Class A
    <Obsolete>
    Public Sub New()
    End Sub
    <Obsolete("""")>
    Public field As Integer
    <Obsolete>
    Public Property prop As Integer
    <Obsolete>
    Public Sub Method()
    End Sub
    <Obsolete>
    Public Event event1 As EventHandler(Of Integer)
End Class
<Obsolete>
Public Interface I
End Interface
<Obsolete>
Public Delegate Sub del(x As Integer)
",
            GetBasicResultAt(4, 2, "A"),
            GetBasicResultAt(6, 6, ".ctor"),
            GetBasicResultAt(9, 6, "field"),
            GetBasicResultAt(11, 6, "prop"),
            GetBasicResultAt(13, 6, "Method"),
            GetBasicResultAt(16, 6, "event1"),
            GetBasicResultAt(19, 2, "I"),
            GetBasicResultAt(22, 2, "del"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharpNoDiagnosticsForInternal()
        {
            VerifyCSharp(@"
using System;

[Obsolete]
class A
{
    [Obsolete]
    A() { }
    [Obsolete("""")]
    int field;
    [Obsolete]
    int Property { get; set; }
    [Obsolete]
    void Method() {}
    [Obsolete]
    event EventHandler<int> event1;
}
[Obsolete]
interface I {}
[Obsolete]
delegate void del(int x);
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void BasicNoDiagnosticsForInternal()
        {
            VerifyBasic(@"
Imports System

<Obsolete>
Class A
    <Obsolete>
    Sub New()
    End Sub
    <Obsolete("""")>
    Dim field As Integer
    <Obsolete>
    Property prop As Integer
    <Obsolete>
    Sub Method()
    End Sub
    <Obsolete>
    Event event1 As EventHandler(Of Integer)
End Class
<Obsolete>
Interface I
End Interface
<Obsolete>
Delegate Sub del(x As Integer)
");
        }

        [Fact]
        public void CSharpNoDiagnostics()
        {
            VerifyCSharp(@"
using System;

[Obsolete(""message"")]
class A
{
    [Obsolete(""valid"")]
    A() { }
    [Obsolete(""valid"")]
    int field;
    [Obsolete(""valid"", true)]
    int Property { get; set; }
    [Obsolete(""valid"", false)]
    void Method() {}
}
");
        }

        [Fact]
        public void BasicNoDiagnostics()
        {
            VerifyBasic(@"
Imports System

<Obsolete(""valid"")>
Class A
    <Obsolete(""valid"")>
    Sub New()
    End Sub
    <Obsolete(""valid"", True)>
    Dim field As Integer
    <Obsolete(""valid"", False)>
    Property prop As Integer
    <Obsolete(""valid"", False)>
    Sub Method()
    End Sub
End Class
");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
        {
            return GetCSharpResultAt(line, column, ProvideObsoleteAttributeMessageAnalyzer.Rule, symbolName);
        }
        private DiagnosticResult GetBasicResultAt(int line, int column, string symbolName)
        {
            return GetBasicResultAt(line, column, ProvideObsoleteAttributeMessageAnalyzer.Rule, symbolName);
        }
    }
}