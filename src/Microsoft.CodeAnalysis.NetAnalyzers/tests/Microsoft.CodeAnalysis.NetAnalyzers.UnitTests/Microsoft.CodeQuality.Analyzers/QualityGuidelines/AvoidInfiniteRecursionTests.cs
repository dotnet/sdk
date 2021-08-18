// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidInfiniteRecursion,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidInfiniteRecursion,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public class AvoidInfiniteRecursionTests
    {
        [Fact]
        public async Task PropertySetterRecursion_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public int Abc
    {
        set
        {
            this.Abc = value;
        }
    }
}",
                GetCSharpResultAt(8, 13, AvoidInfiniteRecursion.Rule, "Abc"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            Me.Abc = value
        End Set
    End Property
End Class",
                GetBasicResultAt(5, 13, AvoidInfiniteRecursion.Rule, "Abc"));
        }

        [Fact]
        public async Task PropertySetterRecursionWithinIf_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public int Abc
    {
        set
        {
            if (value > 42)
            {
                Abc = value;
            }
        }
    }
}",
                GetCSharpResultAt(10, 17, AvoidInfiniteRecursion.MaybeRule, "Abc"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            If value > 42 Then
                Abc = value
            End If
        End Set
    End Property
End Class",
                GetBasicResultAt(6, 17, AvoidInfiniteRecursion.MaybeRule, "Abc"));
        }

        [Fact]
        public async Task PropertySetterRecursionWithinIf_FalsePositive_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public int Abc
    {
        set
        {
            if (false)
            {
                Abc = value;
            }
        }
    }
}",
                GetCSharpResultAt(10, 17, AvoidInfiniteRecursion.MaybeRule, "Abc"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            If false Then
                Abc = value
            End If
        End Set
    End Property
End Class",
                GetBasicResultAt(6, 17, AvoidInfiniteRecursion.MaybeRule, "Abc"));
        }

        [Fact]
        public async Task PropertySetterNoRecursionWithField_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    private int abc;

    public int Abc
    {
        set
        {
            this.abc = value;
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Private abcField As Integer

    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            Me.abcField = value
        End Set
    End Property
End Class");
        }

        [Fact]
        public async Task PropertySetterNoRecursionWithOtherProperty_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public int SomeProperty { get; set; }

    public int Abc
    {
        set
        {
            this.SomeProperty = value;
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Public Property SomeProperty As Integer

    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            Me.SomeProperty = value
        End Set
    End Property
End Class");
        }

        [Fact]
        public async Task PropertySetterRecursionInLambda_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public int Abc
    {
        set
        {
            Action act = () => this.Abc = value;
            act();
        }
    }
}",
                GetCSharpResultAt(10, 32, AvoidInfiniteRecursion.MaybeRule, "Abc"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            Dim act As Action = Sub()
                                    Me.Abc = value
                                End Sub
            act()
        End Set
    End Property
End Class",
                GetBasicResultAt(8, 37, AvoidInfiniteRecursion.MaybeRule, "Abc"));
        }

        [Fact]
        public async Task PropertySetterRecursionInLambda_FalsePositive_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public int Abc
    {
        set
        {
            Action act = () => this.Abc = value; // dead code - the action isn't called
        }
    }
}",
                GetCSharpResultAt(10, 32, AvoidInfiniteRecursion.MaybeRule, "Abc"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            Dim act As Action = Sub()
                                    Me.Abc = value ' dead code - the action isn't called
                                End Sub
        End Set
    End Property
End Class",
                GetBasicResultAt(8, 37, AvoidInfiniteRecursion.MaybeRule, "Abc"));
        }

        [Fact]
        public async Task PropertySetterRecursionInLambda_FalseNegative_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    private Action<int> act;

    public A()
    {
        act = i => this.Abc = i;
    }

    public int Abc
    {
        set
        {
            act(value); // this will cause an infinite loop
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Private act As Action(Of Integer)

    Public Sub New()
        act = Sub(i)
                Me.Abc = i
              End Sub
    End Sub

    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            act(value)
        End Set
    End Property
End Class");
        }

        [Fact]
        public async Task PropertySetterRecursionInLocalFunction_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class A
{
    public int Abc
    {
        set
        {
            SomeMethod();

            void SomeMethod()
            {
                this.Abc = value;
            }
        }
    }
}",
                GetCSharpResultAt(14, 17, AvoidInfiniteRecursion.MaybeRule, "Abc"));
        }

        [Fact]
        public async Task PropertySetterNoRecursionNotOnThis_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    private A _field;

    public int Abc
    {
        set
        {
            _field.Abc = value;
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
    Private _field As A

    Public WriteOnly Property Abc As Integer
        Set(ByVal value As Integer)
            _field.Abc = value
        End Set
    End Property
End Class");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);
    }
}
