// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class NestedTypesShouldNotBeVisibleTests : DiagnosticAnalyzerTestBase
    {
        [Fact]
        public void CSharpDiagnosticPublicNestedClass()
        {
            var code = @"
public class Outer
{
    public class Inner
    {
    }
}
";
            VerifyCSharp(code, GetCSharpCA1034ResultAt(4, 18, "Inner"));
        }

        [Fact]
        public void BasicDiagnosticPublicNestedClass()
        {
            var code = @"
Public Class Outer
    Public Class Inner
    End Class
End Class
";
            VerifyBasic(code, GetBasicCA1034ResultAt(3, 18, "Inner"));
        }

        [Fact]
        public void CSharpDiagnosticPublicNestedStruct()
        {
            var code = @"
public class Outer
{
    public struct Inner
    {
    }
}
";
            VerifyCSharp(code, GetCSharpCA1034ResultAt(4, 19, "Inner"));
        }

        [Fact]
        public void BasicDiagnosticPublicNestedStructure()
        {
            var code = @"
Public Class Outer
    Public Structure Inner
    End Structure
End Class
";
            VerifyBasic(code, GetBasicCA1034ResultAt(3, 22, "Inner"));
        }

        [Fact]
        public void CSharpNoDiagnosticPublicNestedEnum()
        {
            var code = @"
public class Outer
{
    public enum Inner
    {
        None = 0
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticPublicNestedEnum()
        {
            var code = @"
Public Class Outer
    Public Enum Inner
        None = 0
    End Enum
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void BasicDiagnosticPublicTypeNestedInModule()
        {
            var code = @"
Public Module Outer
    Public Class Inner
    End Class
End Module
";
            VerifyBasic(code, GetBasicCA1034ModuleResultAt(3, 18, "Inner"));
        }

        [Fact, WorkItem(1347, "https://github.com/dotnet/roslyn-analyzers/issues/1347")]
        public void CSharpDiagnosticPublicNestedDelegate()
        {
            var code = @"
public class Outer
{
    public delegate void Inner();
}
";
            VerifyCSharp(code);
        }

        [Fact, WorkItem(1347, "https://github.com/dotnet/roslyn-analyzers/issues/1347")]
        public void BasicDiagnosticPublicNestedDelegate()
        {
            var code = @"
Public Class Outer
    Delegate Sub Inner()
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticPrivateNestedType()
        {
            var code = @"
public class Outer
{
    private class Inner
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticPrivateNestedType()
        {
            var code = @"
Public Class Outer
    Private Class Inner
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticProtectedNestedType()
        {
            var code = @"
public class Outer
{
    protected class Inner
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticProtectedNestedType()
        {
            var code = @"
Public Class Outer
    Protected Class Inner
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticInternalNestedType()
        {
            var code = @"
public class Outer
{
    internal class Inner
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticFriendNestedType()
        {
            var code = @"
Public Class Outer
    Friend Class Inner
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticProtectedOrInternalNestedType()
        {
            var code = @"
public class Outer
{
    protected internal class Inner
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticProtectedOrFriendNestedType()
        {
            var code = @"
Public Class Outer
    Protected Friend Class Inner
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void BasicNoDiagnosticNonPublicTypeNestedInModule()
        {
            var code = @"
Public Module Outer
    Friend Class Inner
    End Class
End Module
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticPublicNestedEnumerator()
        {
            var code = @"
using System.Collections;

public class Outer
{
    public class MyEnumerator: IEnumerator
    {
        public bool MoveNext() { return true; }
        public object Current { get; } = null;
        public void Reset() {}
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void CSharpNoDiagnosticPublicTypeNestedInInternalType()
        {
            var code = @"
internal class Outer
{
    public class Inner
    {
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticPublicTypeNestedInFriendType()
        {
            var code = @"
Friend Class Outer
    Public Class Inner
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void BasicNoDiagnosticPublicNestedEnumerator()
        {
            var code = @"
Imports System
Imports System.Collections

Public Class Outer
    Public Class MyEnumerator
        Implements IEnumerator

        Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
            Return True
        End Function

        Public ReadOnly Property Current As Object Implements IEnumerator.Current
            Get
                Return Nothing
            End Get
        End Property

        Public Sub Reset() Implements IEnumerator.Reset
        End Sub
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpNoDiagnosticDataSetSpecialCases()
        {
            var code = @"
using System.Data;

public class MyDataSet : DataSet
{
    public class MyDataTable : DataTable
    {
    }

    public class MyDataRow : DataRow
    {
        public MyDataRow(DataRowBuilder builder) : base(builder)
        {
        }
    }
}
";
            VerifyCSharp(code);
        }

        [Fact]
        public void BasicNoDiagnosticDataSetSpecialCases()
        {
            var code = @"
Imports System.Data

Public Class MyDataSet
    Inherits DataSet

    Public Class MyDataTable
        Inherits DataTable
    End Class

    Public Class MyDataRow
        Inherits DataRow

        Public Sub New(builder As DataRowBuilder)
            MyBase.New(builder)
        End Sub
    End Class
End Class
";
            VerifyBasic(code);
        }

        [Fact]
        public void CSharpDiagnosticDataSetWithOtherNestedClass()
        {
            var code = @"
using System.Data;

public class MyDataSet : DataSet
{
    public class MyDataTable : DataTable
    {
    }

    public class MyDataRow : DataRow
    {
        public MyDataRow(DataRowBuilder builder) : base(builder)
        {
        }
    }

    public class Inner
    {
    }
}
";
            VerifyCSharp(code, GetCSharpCA1034ResultAt(17, 18, "Inner"));
        }

        [Fact]
        public void BasicDiagnosticDataSetWithOtherNestedClass()
        {
            var code = @"
Imports System.Data

Public Class MyDataSet
    Inherits DataSet

    Public Class MyDataTable
        Inherits DataTable
    End Class

    Public Class MyDataRow
        Inherits DataRow

        Public Sub New(builder As DataRowBuilder)
            MyBase.New(builder)
        End Sub
    End Class

    Public Class Inner
    End Class
End Class
";
            VerifyBasic(code, GetBasicCA1034ResultAt(19, 18, "Inner"));
        }

        [Fact]
        public void CSharpDiagnosticNestedDataClassesWithinOtherClass()
        {
            var code = @"
using System.Data;

public class Outer
{
    public class MyDataTable : DataTable
    {
    }

    public class MyDataRow : DataRow
    {
        public MyDataRow(DataRowBuilder builder) : base(builder)
        {
        }
    }
}
";
            VerifyCSharp(code,
                GetCSharpCA1034ResultAt(6, 18, "MyDataTable"),
                GetCSharpCA1034ResultAt(10, 18, "MyDataRow"));
        }

        [Fact]
        public void BasicDiagnosticNestedDataClassesWithinOtherClass()
        {
            var code = @"
Imports System.Data

Public Class Outer
    Public Class MyDataTable
        Inherits DataTable
    End Class

    Public Class MyDataRow
        Inherits DataRow

        Public Sub New(builder As DataRowBuilder)
            MyBase.New(builder)
        End Sub
    End Class
End Class
";
            VerifyBasic(code,
                GetBasicCA1034ResultAt(5, 18, "MyDataTable"),
                GetBasicCA1034ResultAt(9, 18, "MyDataRow"));
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new NestedTypesShouldNotBeVisibleAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NestedTypesShouldNotBeVisibleAnalyzer();
        }

        private DiagnosticResult GetCSharpCA1034ResultAt(int line, int column, string nestedTypeName)
        {
            return GetCSharpResultAt(line, column, NestedTypesShouldNotBeVisibleAnalyzer.DefaultRule, nestedTypeName);
        }

        private DiagnosticResult GetBasicCA1034ResultAt(int line, int column, string nestedTypeName)
        {
            return GetBasicResultAt(line, column, NestedTypesShouldNotBeVisibleAnalyzer.DefaultRule, nestedTypeName);
        }

        private DiagnosticResult GetBasicCA1034ModuleResultAt(int line, int column, string nestedTypeName)
        {
            return GetBasicResultAt(line, column, NestedTypesShouldNotBeVisibleAnalyzer.VisualBasicModuleRule, nestedTypeName);
        }
    }
}