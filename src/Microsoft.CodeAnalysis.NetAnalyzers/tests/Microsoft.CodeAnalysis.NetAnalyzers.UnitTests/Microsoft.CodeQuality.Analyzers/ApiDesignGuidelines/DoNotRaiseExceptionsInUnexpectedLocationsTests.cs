// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotRaiseExceptionsInUnexpectedLocationsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicDoNotRaiseExceptionsInUnexpectedLocationsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpDoNotRaiseExceptionsInUnexpectedLocationsAnalyzer();
        }

        #region Property and Event Tests

        [Fact]
        public void CSharpPropertyNoDiagnostics()
        {
            var code = @"
using System;

public class C
{
    public int PropWithNoException { get { return 10; } set { } }
    public int PropWithSetterException { get { return 10; } set { throw new NotSupportedException(); } }
    public int PropWithAllowedException { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
    public int this[int x] { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
}

class NonPublic
{
    public int PropWithException { get { throw new Exception(); } set { throw new NotSupportedException(); } }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void CSharpPropertyWithDerivedExceptionNoDiagnostics()
        {
            var code = @"
using System;

public class C
{
    public int this[int x] { get { throw new ArgumentOutOfRangeException(); } set { throw new ArgumentOutOfRangeException(); } }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicPropertyNoDiagnostics()
        {
            var code = @"
Imports System

Public Class C
    Public Property PropWithNoException As Integer
        Get
           Return 10
        End Get
        Set 
        End Set
    End Property
    Public Property PropWithSetterException As Integer
        Get
           Return 10
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
    Public Property PropWithAllowedException As Integer
        Get
           Throw New NotSupportedException()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
    Default Public Property Item(x As Integer) As Integer
        Get
           Throw New NotSupportedException()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
End Class

Class NonPublic
    Public Property PropWithInvalidException As Integer
        Get
           Throw New Exception() 'Doesn't fire because it's not visible outside assembly
        End Get
        Set 
        End Set
    End Property
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void BasicPropertyWithDerivedExceptionNoDiagnostics()
        {
            var code = @"
Imports System

Public Class C
    Default Public Property Item(x As Integer) As Integer
        Get
           Throw New ArgumentOutOfRangeException()
        End Get
        Set
            Throw New ArgumentOutOfRangeException()
        End Set
    End Property
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpPropertyWithInvalidExceptions()
        {
            var code = @"
using System;

public class C
{
    public int Prop1 { get { throw new Exception(); } set { throw new NotSupportedException(); } }
    public int this[int x] { get { throw new Exception(); } set { throw new NotSupportedException(); } }
    public event EventHandler Event1 { add { throw new Exception(); } remove { throw new Exception(); } }
}
";
            VerifyCSharp(code,
                         GetCSharpPropertyResultAt(6, 30, "get_Prop1", "Exception"),
                         GetCSharpPropertyResultAt(7, 36, "get_Item", "Exception"),
                         GetCSharpAllowedExceptionsResultAt(8, 46, "add_Event1", "Exception"),
                         GetCSharpAllowedExceptionsResultAt(8, 80, "remove_Event1", "Exception"));
        }

        [Fact]
        public void BasicPropertyWithInvalidExceptions()
        {
            var code = @"
Imports System

Public Class C
    Public Property Prop1 As Integer
        Get
           Throw New Exception()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
    Default Public Property Item(x As Integer) As Integer
        Get
           Throw New Exception()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property

    Public Custom Event Event1 As EventHandler
        AddHandler(ByVal value As EventHandler)
            Throw New Exception()
        End AddHandler
 
        RemoveHandler(ByVal value As EventHandler)
            Throw New Exception()
        End RemoveHandler
 
        ' RaiseEvent accessors are considered private and we won't flag this exception.
        RaiseEvent(ByVal sender As Object, ByVal e As System.EventArgs)
            Throw New Exception() 
        End RaiseEvent
    End Event
End Class
";
            VerifyBasic(code,
                        GetBasicPropertyResultAt(7, 12, "get_Prop1", "Exception"),
                        GetBasicPropertyResultAt(15, 12, "get_Item", "Exception"),
                        GetBasicAllowedExceptionsResultAt(24, 13, "add_Event1", "Exception"),
                        GetBasicAllowedExceptionsResultAt(28, 13, "remove_Event1", "Exception"));
        }

        [Fact, WorkItem(1842, "https://github.com/dotnet/roslyn-analyzers/issues/1842")]
        public void CSharpIndexer_KeyNotFoundException_NoDiagnostics()
        {
            var code = @"
using System.Collections.Generic;

public class C
{
    public int this[int x] { get { throw new KeyNotFoundException(); } }
}";
            VerifyCSharp(code);
        }

        #endregion

        #region Equals, GetHashCode, Dispose and ToString Tests

        [Fact]
        public void CSharpEqualsAndGetHashCodeWithExceptions()
        {
            var code = @"
using System;

public class C
{
    public override bool Equals(object obj)
    {
        throw new Exception();
    }
    public override int GetHashCode()
    {
        throw new ArgumentException("""");
    }
}
";

            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Equals", "Exception"),
                         GetCSharpNoExceptionsResultAt(12, 9, "GetHashCode", "ArgumentException"));
        }

        [Fact]
        public void BasicEqualsAndGetHashCodeWithExceptions()
        {
            var code = @"
Imports System

Public Class C
    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New Exception()
    End Function
    Public Overrides Function GetHashCode() As Integer
        Throw New ArgumentException("""")
    End Function
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(6, 9, "Equals", "Exception"),
                        GetBasicNoExceptionsResultAt(9, 9, "GetHashCode", "ArgumentException"));
        }

        [Fact]
        public void CSharpEqualsAndGetHashCodeNoDiagnostics()
        {
            var code = @"
using System;

public class C
{
    public new bool Equals(object obj)
    {
        throw new Exception();
    }
    public new int GetHashCode()
    {
        throw new ArgumentException("""");
    }
}
";

            VerifyCSharp(code);
        }

        [Fact]
        public void BasicEqualsAndGetHashCodeNoDiagnostics()
        {
            var code = @"
Imports System

Public Class C
    Public Shadows Function Equals(obj As Object) As Boolean
        Throw New Exception()
    End Function
    Public Shadows Function GetHashCode() As Integer
        Throw New ArgumentException("""")
    End Function
End Class
";

            VerifyBasic(code);
        }

        [Fact]
        public void CSharpIEquatableEqualsWithExceptions()
        {
            var code = @"
using System;

public class C : IEquatable<C>
{
    public bool Equals(C obj)
    {
        throw new Exception();
    }
}
";
            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Equals", "Exception"));
        }

        [Fact]
        public void BasicIEquatableEqualsExceptions()
        {
            var code = @"
Imports System

Public Class C
    Implements IEquatable(Of C)
    Public Function Equals(obj As C) As Boolean Implements IEquatable(Of C).Equals
        Throw New Exception()
    End Function
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(7, 9, "Equals", "Exception"));
        }

        [Fact]
        public void CSharpIHashCodeProviderGetHashCode()
        {
            var code = @"
using System;
using System.Collections;
public class C : IHashCodeProvider
{
    public int GetHashCode(object obj)
    {
        throw new Exception();
    }
}

public class D : IHashCodeProvider
{
    public int GetHashCode(object obj)
    {
        throw new ArgumentException(""obj""); // this is fine.
    }
}
";
            VerifyCSharp(code,
                         GetCSharpAllowedExceptionsResultAt(8, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public void BasicIHashCodeProviderGetHashCode()
        {
            var code = @"
Imports System
Imports System.Collections
Public Class C
    Implements IHashCodeProvider
    Public Function GetHashCode(obj As Object) As Integer Implements IHashCodeProvider.GetHashCode
        Throw New Exception()
    End Function
End Class

Public Class D
    Implements IHashCodeProvider
    Public Function GetHashCode(obj As Object) As Integer Implements IHashCodeProvider.GetHashCode
        Throw New ArgumentException() ' This is fine.
    End Function
End Class
";

            VerifyBasic(code,
                        GetBasicAllowedExceptionsResultAt(7, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public void CSharpIEqualityComparer()
        {
            var code = @"
using System;
using System.Collections.Generic;
public class C : IEqualityComparer<C>
{
    public bool Equals(C obj1, C obj2)
    {
        throw new Exception();
    }
    public int GetHashCode(C obj)
    {
        throw new Exception();
    }
}
";
            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Equals", "Exception"),
                         GetCSharpAllowedExceptionsResultAt(12, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public void BasicIEqualityComparer()
        {
            var code = @"
Imports System
Imports System.Collections.Generic
Public Class C
    Implements IEqualityComparer(Of C)
    Public Function Equals(obj1 As C, obj2 As C) As Boolean Implements IEqualityComparer(Of C).Equals
        Throw New Exception()
    End Function
    Public Function GetHashCode(obj As C) As Integer Implements IEqualityComparer(Of C).GetHashCode
        Throw New Exception()
    End Function
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(7, 9, "Equals", "Exception"),
                        GetBasicAllowedExceptionsResultAt(10, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public void CSharpIDisposable()
        {
            var code = @"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        throw new Exception();
    }
}
";
            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Dispose", "Exception"));
        }

        [Fact]
        public void BasicIDisposable()
        {
            var code = @"
Imports System

Public Class C
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New Exception()
    End Sub
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(7, 9, "Dispose", "Exception"));
        }

        [Fact]
        public void CSharpToStringWithExceptions()
        {
            var code = @"
using System;

public class C
{
    public override string ToString()
    {
        throw new Exception();
    }
}
";

            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "ToString", "Exception"));
        }

        [Fact]
        public void BasicToStringWithExceptions()
        {
            var code = @"
Imports System

Public Class C
    Public Overrides Function ToString() As String
        Throw New Exception()
    End Function
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(6, 9, "ToString", "Exception"));
        }

        #endregion

        #region Constructor and Destructor tests
        [Fact]
        public void CSharpStaticConstructorWithExceptions()
        {
            var code = @"
using System;
    
class NonPublic
{
    static NonPublic()
    {
        throw new Exception();
    }
}
";
            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, ".cctor", "Exception"));
        }

        [Fact]
        public void BasicStaticConstructorWithExceptions()
        {
            var code = @"
Imports System

Class NonPublic
    Shared Sub New()
        Throw New Exception()
    End Sub
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(6, 9, ".cctor", "Exception"));
        }

        [Fact]
        public void CSharpFinalizerWithExceptions()
        {
            var code = @"
using System;
    
class NonPublic
{
    ~NonPublic()
    {
        throw new Exception();
    }
}
";
            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Finalize", "Exception"));
        }

        [Fact]
        public void BasicFinalizerWithExceptions()
        {
            var code = @"
Imports System

Class NonPublic
    Protected Overrides Sub Finalize()
        Throw New Exception()
    End Sub
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(6, 9, "Finalize", "Exception"));
        }
        #endregion

        #region Operator tests
        [Fact]
        public void CSharpEqualityOperatorWithExceptions()
        {
            var code = @"
using System;

public class C
{
    public static C operator ==(C c1, C c2)
    {
        throw new Exception();
    }
    public static C operator !=(C c1, C c2)
    {
        throw new Exception();
    }
}
";

            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "op_Equality", "Exception"),
                         GetCSharpNoExceptionsResultAt(12, 9, "op_Inequality", "Exception"));
        }

        [Fact]
        public void BasicEqualityOperatorWithExceptions()
        {
            var code = @"
Imports System

Public Class C
    Public Shared Operator =(c1 As C, c2 As C) As C
        Throw New Exception()
    End Operator
    Public Shared Operator <>(c1 As C, c2 As C) As C
        Throw New Exception()
    End Operator
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(6, 9, "op_Equality", "Exception"),
                        GetBasicNoExceptionsResultAt(9, 9, "op_Inequality", "Exception"));
        }

        [Fact]
        public void CSharpImplicitOperatorWithExceptions()
        {
            var code = @"
using System;

public class C
{
    public static implicit operator int(C c1)
    {   
        throw new Exception();
    }
    public static explicit operator double(C c1)
    {
        throw new Exception(); // This is fine.
    }
}
";

            VerifyCSharp(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "op_Implicit", "Exception"));
        }

        [Fact]
        public void BasicImplicitOperatorWithExceptions()
        {
            var code = @"
Imports System

Public Class C
    Public Shared Widening Operator CType(x As Integer) As C
        Throw New Exception()
    End Operator
    Public Shared Narrowing Operator CType(x As Double) As C
        Throw New Exception()
    End Operator
End Class
";

            VerifyBasic(code,
                        GetBasicNoExceptionsResultAt(6, 9, "op_Implicit", "Exception"));
        }
        #endregion

        private DiagnosticResult GetCSharpPropertyResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.PropertyGetterRule, methodName, exceptionName);
        }
        private DiagnosticResult GetCSharpAllowedExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.HasAllowedExceptionsRule, methodName, exceptionName);
        }
        private DiagnosticResult GetCSharpNoExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.NoAllowedExceptionsRule, methodName, exceptionName);
        }

        private DiagnosticResult GetBasicPropertyResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetBasicResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.PropertyGetterRule, methodName, exceptionName);
        }
        private DiagnosticResult GetBasicAllowedExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetBasicResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.HasAllowedExceptionsRule, methodName, exceptionName);
        }
        private DiagnosticResult GetBasicNoExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetBasicResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.NoAllowedExceptionsRule, methodName, exceptionName);
        }
    }
}
