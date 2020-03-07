// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UseEventsWhereAppropriateTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new UseEventsWhereAppropriateAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new UseEventsWhereAppropriateAnalyzer();
        }

        #region No Diagnostic Tests

        [WorkItem(380, "https://github.com/dotnet/roslyn-analyzers/issues/380")]
        [Fact]
        public void NoDiagnostic_NamingCases()
        {
            VerifyCSharp(@"
using System;

public class EventsClass1
{
    public event EventHandler RaiseFileEvent;

    public string RaiseAnotherProperty => null;

    public void SomeMethodThatDoesNotStartWithRaise()
    {
    }

    public void RaisedRoutine()
    {
    }

    public void AddOneAssembly()
    {
    }

    public void Remover()
    {
    }
}
");

            VerifyBasic(@"
Imports System

Public Class EventsClass1
    Public Event RaiseFileEvent As EventHandler

    Public ReadOnly Property RaiseAnotherProperty() As String
        Get
            Return Nothing
        End Get
    End Property

    Public Sub SomeMethodThatDoesNotStartWithRaise()
    End Sub

    Public Sub RaisedRoutine()
    End Sub

    Public Sub AddOneAssembly()
    End Sub

    Public Sub Remover()
    End Sub
End Class
");
        }

        [WorkItem(380, "https://github.com/dotnet/roslyn-analyzers/issues/380")]
        [Fact]
        public void NoDiagnostic_InterfaceMemberImplementation()
        {
            VerifyCSharp(@"
using System;

public class InterfaceImplementation : I
{
    // Explicit interface implementation - Rule does not fire.
    void I.FireOnSomething_InterfaceMethod1()
    {
        throw new NotImplementedException();
    }

    // Implicit interface implementation - Rule does not fire.
    public void FireOnSomething_InterfaceMethod2()
    {
        throw new NotImplementedException();
    }
}

#pragma warning disable CA1030 // We are only testing no violations in InterfaceImplementation in this test, so suppress issues reported in the interface.
public interface I
{
    // Interface methods - Rule fires.
    void FireOnSomething_InterfaceMethod1();
    void FireOnSomething_InterfaceMethod2();
}
#pragma warning restore CA1030
");

            VerifyBasic(@"
Imports System

Public Class InterfaceImplementation
	Implements I
	' Explicit interface implementation - Rule does not fire.
	Private Sub FireOnSomething_InterfaceMethod1() Implements I.FireOnSomething_InterfaceMethod1
		Throw New NotImplementedException()
	End Sub

	' Implicit interface implementation - Rule does not fire.
	Public Sub FireOnSomething_InterfaceMethod2() Implements I.FireOnSomething_InterfaceMethod2
		Throw New NotImplementedException()
	End Sub
End Class

#Disable Warning CA1030 ' We are only testing no violations in InterfaceImplementation in this test, so suppress issues reported in the interface.
Public Interface I
	' Interface methods - Rule fires.
	Sub FireOnSomething_InterfaceMethod1()
	Sub FireOnSomething_InterfaceMethod2()
End Interface
#Enable Warning CA1030
");
        }

        [WorkItem(380, "https://github.com/dotnet/roslyn-analyzers/issues/380")]
        [Fact]
        public void NoDiagnostic_UnflaggedMethodKinds()
        {
            VerifyCSharp(@"
using System;

public class FireOnSomethingDerivedClass : BaseClass
{
    // Constructor - Rule does not fire.
    public FireOnSomethingDerivedClass()
    {
    }

    // Finalizer - Rule does not fire.
    ~FireOnSomethingDerivedClass()
    {
    }

    // Overridden methods - Rule does not fire.
    public override void FireOnSomething()
    {
        throw new NotImplementedException();
    }

    public override void RaiseOnSomething()
    {
        throw new NotImplementedException();
    }

    public override void AddOnSomething()
    {
        throw new NotImplementedException();
    }

    public override void RemoveOnSomething()
    {
        throw new NotImplementedException();
    }
}

#pragma warning disable CA1030 // We are only testing no violations in FireOnSomethingDerivedClass in this test, so suppress issues reported in the BaseClass.
public abstract class BaseClass
{
    // Abstract method - Rule fires.
    public abstract void FireOnSomething();

    // Abstract method - Rule fires.
    public abstract void RaiseOnSomething();

    // Abstract method - Rule fires.
    public abstract void AddOnSomething();

    // Abstract method - Rule fires.
    public abstract void RemoveOnSomething();
}
#pragma warning restore CA1030
");

            VerifyBasic(@"
Imports System

Public Class FireOnSomethingDerivedClass
	Inherits BaseClass
	' Constructor - Rule does not fire.
	Public Sub New()
	End Sub

	' Finalizer - Rule does not fire.
	Protected Overrides Sub Finalize()
		Try
		Finally
			MyBase.Finalize()
		End Try
	End Sub

	' Overridden methods - Rule does not fire.
	Public Overrides Sub FireOnSomething()
		Throw New NotImplementedException()
	End Sub

	Public Overrides Sub RaiseOnSomething()
		Throw New NotImplementedException()
	End Sub

	Public Overrides Sub AddOnSomething()
		Throw New NotImplementedException()
	End Sub

	Public Overrides Sub RemoveOnSomething()
		Throw New NotImplementedException()
	End Sub
End Class

#Disable Warning CA1030 ' We are only testing no violations in FireOnSomethingDerivedClass in this test, so suppress issues reported in the BaseClass.
Public MustInherit Class BaseClass
	' Abstract method - Rule fires.
	Public MustOverride Overloads Sub FireOnSomething()

	' Abstract method - Rule fires.
	Public MustOverride Overloads Sub RaiseOnSomething()

	' Abstract method - Rule fires.
	Public MustOverride Overloads Sub AddOnSomething()

	' Abstract method - Rule fires.
	Public MustOverride Overloads Sub RemoveOnSomething()
End Class
#Enable Warning CA1030
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void NoDiagnostic_FlaggedMethodKinds_NotExternallyVisible()
        {
            VerifyCSharp(@"
using System;

internal interface InterfaceWithViolations
{
    // Interface methods.
    void FireOnSomething_InterfaceMethod1();
    void FireOnSomething_InterfaceMethod2();
}

public abstract class ClassWithViolations
{
    private class InnerClass
    {
        // Static method.
        public static void RaiseOnSomething_StaticMethod()
        {
        }
    }

    internal class InnerClass2
    {
        // Virtual method.
        public virtual void FireOnSomething_VirtualMethod()
        {
        }
    }

    // Private method.
    private static void RaiseOnSomething_StaticMethod()
    {
    }

    // Abstract method.
    internal abstract void FireOnSomething_AbstractMethod();

    // Abstract method.
    internal abstract void RaiseOnSomething_AbstractMethod();

    // Abstract method.
    internal abstract void AddOnSomething_AbstractMethod();

    // Abstract method.
    internal abstract void RemoveOnSomething_AbstractMethod();
}
");

            VerifyBasic(@"
Friend Interface InterfaceWithViolations
    ' Interface methods.
    Sub FireOnSomething_InterfaceMethod1()
    Sub FireOnSomething_InterfaceMethod2()
End Interface

Public MustInherit Class ClassWithViolations
    Private Class InnerClass
        ' Static method.
        Public Shared Sub RaiseOnSomething_StaticMethod()
        End Sub
    End Class

    Friend Class InnerClass2
        ' Virtual method.
        Public Overridable Sub FireOnSomething_VirtualMethod()
        End Sub
    End Class

    ' Private method.
    Private Shared Sub RaiseOnSomething_StaticMethod()
    End Sub

    ' Abstract method.
    Friend MustOverride Sub FireOnSomething_AbstractMethod()

    ' Abstract method.
    Friend MustOverride Sub RaiseOnSomething_AbstractMethod()

    ' Abstract method.
    Friend MustOverride Sub AddOnSomething_AbstractMethod()

    ' Abstract method.
    Friend MustOverride Sub RemoveOnSomething_AbstractMethod()
End Class
");
        }

        #endregion

        #region Diagnostic Tests

        [WorkItem(380, "https://github.com/dotnet/roslyn-analyzers/issues/380")]
        [Fact]
        public void Diagnostic_FlaggedMethodKinds()
        {
            VerifyCSharp(@"
using System;

public interface InterfaceWithViolations
{
    // Interface methods - Rule fires.
    void FireOnSomething_InterfaceMethod1();
    void FireOnSomething_InterfaceMethod2();
}

public abstract class ClassWithViolations
{
    // Static method - Rule fires.
    public static void RaiseOnSomething_StaticMethod()
    {
    }

    // Virtual method - Rule fires.
    public virtual void FireOnSomething_VirtualMethod()
    {
    }

    // Abstract method - Rule fires.
    public abstract void FireOnSomething_AbstractMethod();

    // Abstract method - Rule fires.
    public abstract void RaiseOnSomething_AbstractMethod();

    // Abstract method - Rule fires.
    public abstract void AddOnSomething_AbstractMethod();

    // Abstract method - Rule fires.
    public abstract void RemoveOnSomething_AbstractMethod();
}
",
      // Test0.cs(7,10): warning CA1030: Consider making 'FireOnSomething_InterfaceMethod1' an event.
      GetCSharpResultAt(7, 10, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_InterfaceMethod1"),
      // Test0.cs(8,10): warning CA1030: Consider making 'FireOnSomething_InterfaceMethod2' an event.
      GetCSharpResultAt(8, 10, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_InterfaceMethod2"),
      // Test0.cs(14,24): warning CA1030: Consider making 'RaiseOnSomething_StaticMethod' an event.
      GetCSharpResultAt(14, 24, UseEventsWhereAppropriateAnalyzer.Rule, "RaiseOnSomething_StaticMethod"),
      // Test0.cs(19,25): warning CA1030: Consider making 'FireOnSomething_VirtualMethod' an event.
      GetCSharpResultAt(19, 25, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_VirtualMethod"),
      // Test0.cs(24,26): warning CA1030: Consider making 'FireOnSomething_AbstractMethod' an event.
      GetCSharpResultAt(24, 26, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_AbstractMethod"),
      // Test0.cs(27,26): warning CA1030: Consider making 'RaiseOnSomething_AbstractMethod' an event.
      GetCSharpResultAt(27, 26, UseEventsWhereAppropriateAnalyzer.Rule, "RaiseOnSomething_AbstractMethod"),
      // Test0.cs(30,26): warning CA1030: Consider making 'AddOnSomething_AbstractMethod' an event.
      GetCSharpResultAt(30, 26, UseEventsWhereAppropriateAnalyzer.Rule, "AddOnSomething_AbstractMethod"),
      // Test0.cs(33,26): warning CA1030: Consider making 'RemoveOnSomething_AbstractMethod' an event.
      GetCSharpResultAt(33, 26, UseEventsWhereAppropriateAnalyzer.Rule, "RemoveOnSomething_AbstractMethod"));

            VerifyBasic(@"
Public Interface InterfaceWithViolations
	' Interface methods - Rule fires.
	Sub FireOnSomething_InterfaceMethod1()
	Sub FireOnSomething_InterfaceMethod2()
End Interface

Public MustInherit Class ClassWithViolations
	' Static method - Rule fires.
	Public Shared Sub RaiseOnSomething_StaticMethod()
	End Sub

	' Virtual method - Rule fires.
	Public Overridable Sub FireOnSomething_VirtualMethod()
	End Sub

	' Abstract method - Rule fires.
	Public MustOverride Sub FireOnSomething_AbstractMethod()

	' Abstract method - Rule fires.
	Public MustOverride Sub RaiseOnSomething_AbstractMethod()

	' Abstract method - Rule fires.
	Public MustOverride Sub AddOnSomething_AbstractMethod()

	' Abstract method - Rule fires.
	Public MustOverride Sub RemoveOnSomething_AbstractMethod()
End Class
",
      // Test0.vb(4,6): warning CA1030: Consider making 'FireOnSomething_InterfaceMethod1' an event.
      GetBasicResultAt(4, 6, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_InterfaceMethod1"),
      // Test0.vb(5,6): warning CA1030: Consider making 'FireOnSomething_InterfaceMethod2' an event.
      GetBasicResultAt(5, 6, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_InterfaceMethod2"),
      // Test0.vb(10,20): warning CA1030: Consider making 'RaiseOnSomething_StaticMethod' an event.
      GetBasicResultAt(10, 20, UseEventsWhereAppropriateAnalyzer.Rule, "RaiseOnSomething_StaticMethod"),
      // Test0.vb(14,25): warning CA1030: Consider making 'FireOnSomething_VirtualMethod' an event.
      GetBasicResultAt(14, 25, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_VirtualMethod"),
      // Test0.vb(18,26): warning CA1030: Consider making 'FireOnSomething_AbstractMethod' an event.
      GetBasicResultAt(18, 26, UseEventsWhereAppropriateAnalyzer.Rule, "FireOnSomething_AbstractMethod"),
      // Test0.vb(21,26): warning CA1030: Consider making 'RaiseOnSomething_AbstractMethod' an event.
      GetBasicResultAt(21, 26, UseEventsWhereAppropriateAnalyzer.Rule, "RaiseOnSomething_AbstractMethod"),
      // Test0.vb(24,26): warning CA1030: Consider making 'AddOnSomething_AbstractMethod' an event.
      GetBasicResultAt(24, 26, UseEventsWhereAppropriateAnalyzer.Rule, "AddOnSomething_AbstractMethod"),
      // Test0.vb(27,26): warning CA1030: Consider making 'RemoveOnSomething_AbstractMethod' an event.
      GetBasicResultAt(27, 26, UseEventsWhereAppropriateAnalyzer.Rule, "RemoveOnSomething_AbstractMethod"));
        }

        [WorkItem(380, "https://github.com/dotnet/roslyn-analyzers/issues/380")]
        [Fact]
        public void Diagnostic_PascalCasedMethodNames()
        {
            VerifyCSharp(@"
public class EventsClassPascalCased
{
    public void Fire() { }

    public void Raise() { }

    public void RaiseFileEvent() { }

    public void FireFileEvent() { }

    public void AddOnFileEvent() { }

    public void RemoveOnFileEvent() { }

    public void Add_OnFileEvent() { }

    public void Remove_OnFileEvent() { }
}
",
      // Test0.cs(4,17): warning CA1030: Consider making 'Fire' an event.
      GetCSharpResultAt(4, 17, UseEventsWhereAppropriateAnalyzer.Rule, "Fire"),
      // Test0.cs(6,17): warning CA1030: Consider making 'Raise' an event.
      GetCSharpResultAt(6, 17, UseEventsWhereAppropriateAnalyzer.Rule, "Raise"),
      // Test0.cs(8,17): warning CA1030: Consider making 'RaiseFileEvent' an event.
      GetCSharpResultAt(8, 17, UseEventsWhereAppropriateAnalyzer.Rule, "RaiseFileEvent"),
      // Test0.cs(10,17): warning CA1030: Consider making 'FireFileEvent' an event.
      GetCSharpResultAt(10, 17, UseEventsWhereAppropriateAnalyzer.Rule, "FireFileEvent"),
      // Test0.cs(12,17): warning CA1030: Consider making 'AddOnFileEvent' an event.
      GetCSharpResultAt(12, 17, UseEventsWhereAppropriateAnalyzer.Rule, "AddOnFileEvent"),
      // Test0.cs(14,17): warning CA1030: Consider making 'RemoveOnFileEvent' an event.
      GetCSharpResultAt(14, 17, UseEventsWhereAppropriateAnalyzer.Rule, "RemoveOnFileEvent"),
      // Test0.cs(16,17): warning CA1030: Consider making 'Add_OnFileEvent' an event.
      GetCSharpResultAt(16, 17, UseEventsWhereAppropriateAnalyzer.Rule, "Add_OnFileEvent"),
      // Test0.cs(18,17): warning CA1030: Consider making 'Remove_OnFileEvent' an event.
      GetCSharpResultAt(18, 17, UseEventsWhereAppropriateAnalyzer.Rule, "Remove_OnFileEvent"));

            VerifyBasic(@"
Public Class EventsClassPascalCased
	Public Sub Fire()
	End Sub

	Public Sub Raise()
	End Sub

	Public Sub RaiseFileEvent()
	End Sub

	Public Sub FireFileEvent()
	End Sub

	Public Sub AddOnFileEvent()
	End Sub

	Public Sub RemoveOnFileEvent()
	End Sub

	Public Sub Add_OnFileEvent()
	End Sub

	Public Sub Remove_OnFileEvent()
	End Sub
End Class
",
      // Test0.vb(3,13): warning CA1030: Consider making 'Fire' an event.
      GetBasicResultAt(3, 13, UseEventsWhereAppropriateAnalyzer.Rule, "Fire"),
      // Test0.vb(6,13): warning CA1030: Consider making 'Raise' an event.
      GetBasicResultAt(6, 13, UseEventsWhereAppropriateAnalyzer.Rule, "Raise"),
      // Test0.vb(9,13): warning CA1030: Consider making 'RaiseFileEvent' an event.
      GetBasicResultAt(9, 13, UseEventsWhereAppropriateAnalyzer.Rule, "RaiseFileEvent"),
      // Test0.vb(12,13): warning CA1030: Consider making 'FireFileEvent' an event.
      GetBasicResultAt(12, 13, UseEventsWhereAppropriateAnalyzer.Rule, "FireFileEvent"),
      // Test0.vb(15,13): warning CA1030: Consider making 'AddOnFileEvent' an event.
      GetBasicResultAt(15, 13, UseEventsWhereAppropriateAnalyzer.Rule, "AddOnFileEvent"),
      // Test0.vb(18,13): warning CA1030: Consider making 'RemoveOnFileEvent' an event.
      GetBasicResultAt(18, 13, UseEventsWhereAppropriateAnalyzer.Rule, "RemoveOnFileEvent"),
      // Test0.vb(21,13): warning CA1030: Consider making 'Add_OnFileEvent' an event.
      GetBasicResultAt(21, 13, UseEventsWhereAppropriateAnalyzer.Rule, "Add_OnFileEvent"),
      // Test0.vb(24,13): warning CA1030: Consider making 'Remove_OnFileEvent' an event.
      GetBasicResultAt(24, 13, UseEventsWhereAppropriateAnalyzer.Rule, "Remove_OnFileEvent"));

        }

        [WorkItem(380, "https://github.com/dotnet/roslyn-analyzers/issues/380")]
        [Fact]
        public void Diagnostic_LowerCaseMethodNames()
        {
            VerifyCSharp(@"
public class EventsClassLowercase
{
    public void fire() { }

    public void raise() { }

    public void raiseFileEvent() { }

    public void fireFileEvent() { }

    public void addOnFileEvent() { }

    public void removeOnFileEvent() { }

    public void add_onFileEvent() { }

    public void remove_onFileEvent() { }
}
",
      // Test0.cs(4,17): warning CA1030: Consider making 'fire' an event.
      GetCSharpResultAt(4, 17, UseEventsWhereAppropriateAnalyzer.Rule, "fire"),
      // Test0.cs(6,17): warning CA1030: Consider making 'raise' an event.
      GetCSharpResultAt(6, 17, UseEventsWhereAppropriateAnalyzer.Rule, "raise"),
      // Test0.cs(8,17): warning CA1030: Consider making 'raiseFileEvent' an event.
      GetCSharpResultAt(8, 17, UseEventsWhereAppropriateAnalyzer.Rule, "raiseFileEvent"),
      // Test0.cs(10,17): warning CA1030: Consider making 'fireFileEvent' an event.
      GetCSharpResultAt(10, 17, UseEventsWhereAppropriateAnalyzer.Rule, "fireFileEvent"),
      // Test0.cs(12,17): warning CA1030: Consider making 'addOnFileEvent' an event.
      GetCSharpResultAt(12, 17, UseEventsWhereAppropriateAnalyzer.Rule, "addOnFileEvent"),
      // Test0.cs(14,17): warning CA1030: Consider making 'removeOnFileEvent' an event.
      GetCSharpResultAt(14, 17, UseEventsWhereAppropriateAnalyzer.Rule, "removeOnFileEvent"),
      // Test0.cs(16,17): warning CA1030: Consider making 'add_onFileEvent' an event.
      GetCSharpResultAt(16, 17, UseEventsWhereAppropriateAnalyzer.Rule, "add_onFileEvent"),
      // Test0.cs(18,17): warning CA1030: Consider making 'remove_onFileEvent' an event.
      GetCSharpResultAt(18, 17, UseEventsWhereAppropriateAnalyzer.Rule, "remove_onFileEvent"));

            VerifyBasic(@"
Public Class EventsClassLowercase
	Public Sub fire()
	End Sub

	Public Sub raise()
	End Sub

	Public Sub raiseFileEvent()
	End Sub

	Public Sub fireFileEvent()
	End Sub

	Public Sub addOnFileEvent()
	End Sub

	Public Sub removeOnFileEvent()
	End Sub

	Public Sub add_onFileEvent()
	End Sub

	Public Sub remove_onFileEvent()
	End Sub
End Class
",
      // Test0.vb(3,13): warning CA1030: Consider making 'fire' an event.
      GetBasicResultAt(3, 13, UseEventsWhereAppropriateAnalyzer.Rule, "fire"),
      // Test0.vb(6,13): warning CA1030: Consider making 'raise' an event.
      GetBasicResultAt(6, 13, UseEventsWhereAppropriateAnalyzer.Rule, "raise"),
      // Test0.vb(9,13): warning CA1030: Consider making 'raiseFileEvent' an event.
      GetBasicResultAt(9, 13, UseEventsWhereAppropriateAnalyzer.Rule, "raiseFileEvent"),
      // Test0.vb(12,13): warning CA1030: Consider making 'fireFileEvent' an event.
      GetBasicResultAt(12, 13, UseEventsWhereAppropriateAnalyzer.Rule, "fireFileEvent"),
      // Test0.vb(15,13): warning CA1030: Consider making 'addOnFileEvent' an event.
      GetBasicResultAt(15, 13, UseEventsWhereAppropriateAnalyzer.Rule, "addOnFileEvent"),
      // Test0.vb(18,13): warning CA1030: Consider making 'removeOnFileEvent' an event.
      GetBasicResultAt(18, 13, UseEventsWhereAppropriateAnalyzer.Rule, "removeOnFileEvent"),
      // Test0.vb(21,13): warning CA1030: Consider making 'add_onFileEvent' an event.
      GetBasicResultAt(21, 13, UseEventsWhereAppropriateAnalyzer.Rule, "add_onFileEvent"),
      // Test0.vb(24,13): warning CA1030: Consider making 'remove_onFileEvent' an event.
      GetBasicResultAt(24, 13, UseEventsWhereAppropriateAnalyzer.Rule, "remove_onFileEvent"));

        }

        #endregion
    }
}