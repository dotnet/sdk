// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.NestedTypesShouldNotBeVisibleAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.NestedTypesShouldNotBeVisibleAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class NestedTypesShouldNotBeVisibleTests
    {
        [Fact]
        public async Task CSharpDiagnosticPublicNestedClass()
        {
            var code = @"
public class Outer
{
    public class Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpCA1034ResultAt(4, 18, "Inner"));
        }

        [Fact]
        public async Task BasicDiagnosticPublicNestedClass()
        {
            var code = @"
Public Class Outer
    Public Class Inner
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicCA1034ResultAt(3, 18, "Inner"));
        }

        [Fact]
        public async Task CSharpDiagnosticPublicNestedStruct()
        {
            var code = @"
public class Outer
{
    public struct Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpCA1034ResultAt(4, 19, "Inner"));
        }

        [Fact]
        public async Task BasicDiagnosticPublicNestedStructure()
        {
            var code = @"
Public Class Outer
    Public Structure Inner
    End Structure
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicCA1034ResultAt(3, 22, "Inner"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticPublicNestedEnum()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticPublicNestedEnum()
        {
            var code = @"
Public Class Outer
    Public Enum Inner
        None = 0
    End Enum
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicDiagnosticPublicTypeNestedInModule()
        {
            var code = @"
Public Module Outer
    Public Class Inner
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicCA1034ModuleResultAt(3, 18, "Inner"));
        }

        [Fact, WorkItem(1347, "https://github.com/dotnet/roslyn-analyzers/issues/1347")]
        public async Task CSharpDiagnosticPublicNestedDelegate()
        {
            var code = @"
public class Outer
{
    public delegate void Inner();
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(1347, "https://github.com/dotnet/roslyn-analyzers/issues/1347")]
        public async Task BasicDiagnosticPublicNestedDelegate()
        {
            var code = @"
Public Class Outer
    Delegate Sub Inner()
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticPrivateNestedType()
        {
            var code = @"
public class Outer
{
    private class Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticPrivateNestedType()
        {
            var code = @"
Public Class Outer
    Private Class Inner
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticProtectedNestedType()
        {
            var code = @"
public class Outer
{
    protected class Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticProtectedNestedType()
        {
            var code = @"
Public Class Outer
    Protected Class Inner
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticInternalNestedType()
        {
            var code = @"
public class Outer
{
    internal class Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticFriendNestedType()
        {
            var code = @"
Public Class Outer
    Friend Class Inner
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticProtectedOrInternalNestedType()
        {
            var code = @"
public class Outer
{
    protected internal class Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticProtectedOrFriendNestedType()
        {
            var code = @"
Public Class Outer
    Protected Friend Class Inner
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticNonPublicTypeNestedInModule()
        {
            var code = @"
Public Module Outer
    Friend Class Inner
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticPublicNestedEnumerator()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticPublicTypeNestedInInternalType()
        {
            var code = @"
internal class Outer
{
    public class Inner
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticPublicTypeNestedInFriendType()
        {
            var code = @"
Friend Class Outer
    Public Class Inner
    End Class
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticPublicNestedEnumerator()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticDataSetSpecialCases()
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticDataSetSpecialCases()
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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpDiagnosticDataSetWithOtherNestedClass()
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
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpCA1034ResultAt(17, 18, "Inner"));
        }

        [Fact]
        public async Task BasicDiagnosticDataSetWithOtherNestedClass()
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
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicCA1034ResultAt(19, 18, "Inner"));
        }

        [Fact]
        public async Task CSharpDiagnosticNestedDataClassesWithinOtherClass()
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
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpCA1034ResultAt(6, 18, "MyDataTable"),
                GetCSharpCA1034ResultAt(10, 18, "MyDataRow"));
        }

        [Fact]
        public async Task BasicDiagnosticNestedDataClassesWithinOtherClass()
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
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicCA1034ResultAt(5, 18, "MyDataTable"),
                GetBasicCA1034ResultAt(9, 18, "MyDataRow"));
        }

        private DiagnosticResult GetCSharpCA1034ResultAt(int line, int column, string nestedTypeName)
            => new DiagnosticResult(NestedTypesShouldNotBeVisibleAnalyzer.DefaultRule)
                .WithLocation(line, column)
                .WithArguments(nestedTypeName);

        private DiagnosticResult GetBasicCA1034ResultAt(int line, int column, string nestedTypeName)
            => new DiagnosticResult(NestedTypesShouldNotBeVisibleAnalyzer.DefaultRule)
                .WithLocation(line, column)
                .WithArguments(nestedTypeName);

        private DiagnosticResult GetBasicCA1034ModuleResultAt(int line, int column, string nestedTypeName)
            => new DiagnosticResult(NestedTypesShouldNotBeVisibleAnalyzer.VisualBasicModuleRule)
                .WithLocation(line, column)
                .WithArguments(nestedTypeName);
    }
}