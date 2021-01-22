// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
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

        public enum BuilderPatternVariant
        {
            Correct,
            CorrectWithNameEndsInBuilder,
            IncorrectWithPublicOuterConstructor,
            IncorrectWithProtectedOuterConstructor,
            IncorrectNotNamedBuilder,
        }

        [Theory, WorkItem(3033, "https://github.com/dotnet/roslyn-analyzers/issues/3033")]
        [InlineData(BuilderPatternVariant.Correct)]
        [InlineData(BuilderPatternVariant.CorrectWithNameEndsInBuilder)]
        [InlineData(BuilderPatternVariant.IncorrectWithPublicOuterConstructor)]
        [InlineData(BuilderPatternVariant.IncorrectWithProtectedOuterConstructor)]
        [InlineData(BuilderPatternVariant.IncorrectNotNamedBuilder)]
        public async Task CA1034_BuilderPatternVariants(BuilderPatternVariant variant)
        {
            string builderName = "Builder";
            string outerClassCtorAccessibility = "private";
            DiagnosticResult[] csharpExpectedDiagnostics = Array.Empty<DiagnosticResult>();
            DiagnosticResult[] vbnetExpectedDiagnostics = Array.Empty<DiagnosticResult>();

            switch (variant)
            {
                case BuilderPatternVariant.Correct:
                    break;

                case BuilderPatternVariant.CorrectWithNameEndsInBuilder:
                    builderName = "PizzaBuilder";
                    break;

                case BuilderPatternVariant.IncorrectWithPublicOuterConstructor:
                case BuilderPatternVariant.IncorrectWithProtectedOuterConstructor:
                    csharpExpectedDiagnostics = new[] { GetCSharpCA1034ResultAt(13, 25, builderName), };
                    vbnetExpectedDiagnostics = new[] { GetBasicCA1034ResultAt(11, 33, builderName), };
                    outerClassCtorAccessibility = variant == BuilderPatternVariant.IncorrectWithProtectedOuterConstructor ? "protected" : "public";
                    break;

                case BuilderPatternVariant.IncorrectNotNamedBuilder:
                    builderName = "InnerClass";
                    csharpExpectedDiagnostics = new[] { GetCSharpCA1034ResultAt(13, 25, builderName), };
                    vbnetExpectedDiagnostics = new[] { GetBasicCA1034ResultAt(11, 33, builderName), };
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(variant));
            }

            await VerifyCS.VerifyAnalyzerAsync($@"
public class Pizza
{{
    public bool HasCheese {{ get; }}
    public bool HasBacon {{ get; }}

    {outerClassCtorAccessibility} Pizza(bool hasCheese, bool hasBacon)
    {{
        HasCheese = hasCheese;
        HasBacon = hasBacon;
    }}

    public sealed class {builderName}
    {{
        private bool hasCheese;
        private bool hasBacon;

        private {builderName}() {{ }}

        public static {builderName} Create()
        {{
            return new {builderName}();
        }}

        public {builderName} WithCheese()
        {{
            hasCheese = true;
            return this;
        }}

        public {builderName} WithBacon()
        {{
            hasBacon = true;
            return this;
        }}

        public Pizza ToPizza()
        {{
            return new Pizza(hasCheese, hasBacon);
        }}
    }}
}}", csharpExpectedDiagnostics);

            await VerifyVB.VerifyAnalyzerAsync($@"
Public Class Pizza
    Public ReadOnly Property HasCheese As Boolean
    Public ReadOnly Property HasBacon As Boolean

    {outerClassCtorAccessibility} Sub New(ByVal hasCheese As Boolean, ByVal hasBacon As Boolean)
        Me.HasCheese = hasCheese
        Me.HasBacon = hasBacon
    End Sub

    Public NotInheritable Class {builderName}
        Private hasCheese As Boolean
        Private hasBacon As Boolean

        Private Sub New()
        End Sub

        Public Shared Function Create() As {builderName}
            Return New {builderName}()
        End Function

        Public Function WithCheese() As {builderName}
            hasCheese = True
            Return Me
        End Function

        Public Function WithBacon() As {builderName}
            hasBacon = True
            Return Me
        End Function

        Public Function ToPizza() As Pizza
            Return New Pizza(hasCheese, hasBacon)
        End Function
    End Class
End Class
", vbnetExpectedDiagnostics);
        }

        [Fact, WorkItem(3033, "https://github.com/dotnet/roslyn-analyzers/issues/3033")]
        public async Task CA1034_BuilderPatternTooDeep_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Outer
{
    public class Pizza
    {
        public bool HasCheese { get; }
        public bool HasBacon { get; }

        protected Pizza(bool hasCheese, bool hasBacon)
        {
            HasCheese = hasCheese;
            HasBacon = hasBacon;
        }

        public sealed class Builder
        {
            private bool hasCheese;
            private bool hasBacon;

            private Builder() { }

            public static Builder Create()
            {
                return new Builder();
            }

            public Builder WithCheese()
            {
                hasCheese = true;
                return this;
            }

            public Builder WithBacon()
            {
                hasBacon = true;
                return this;
            }

            public Pizza ToPizza()
            {
                return new Pizza(hasCheese, hasBacon);
            }
        }
    }
}", GetCSharpCA1034ResultAt(4, 18, "Pizza"), GetCSharpCA1034ResultAt(15, 29, "Builder"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Outer
    Public Class Pizza
        Public ReadOnly Property HasCheese As Boolean
        Public ReadOnly Property HasBacon As Boolean

         Protected Sub New(ByVal hasCheese As Boolean, ByVal hasBacon As Boolean)
            Me.HasCheese = hasCheese
            Me.HasBacon = hasBacon
        End Sub

        Public NotInheritable Class Builder
            Private hasCheese As Boolean
            Private hasBacon As Boolean

            Private Sub New()
            End Sub

            Public Shared Function Create() As Builder
                Return New Builder()
            End Function

            Public Function WithCheese() As Builder
                hasCheese = True
                Return Me
            End Function

            Public Function WithBacon() As Builder
                hasBacon = True
                Return Me
            End Function

            Public Function ToPizza() As Pizza
                Return New Pizza(hasCheese, hasBacon)
            End Function
        End Class
    End Class
End Class
", GetBasicCA1034ResultAt(3, 18, "Pizza"), GetBasicCA1034ResultAt(12, 37, "Builder"));
        }

        private static DiagnosticResult GetCSharpCA1034ResultAt(int line, int column, string nestedTypeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(NestedTypesShouldNotBeVisibleAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(nestedTypeName);

        private static DiagnosticResult GetBasicCA1034ResultAt(int line, int column, string nestedTypeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(NestedTypesShouldNotBeVisibleAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(nestedTypeName);

        private static DiagnosticResult GetBasicCA1034ModuleResultAt(int line, int column, string nestedTypeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(NestedTypesShouldNotBeVisibleAnalyzer.VisualBasicModuleRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(nestedTypeName);
    }
}