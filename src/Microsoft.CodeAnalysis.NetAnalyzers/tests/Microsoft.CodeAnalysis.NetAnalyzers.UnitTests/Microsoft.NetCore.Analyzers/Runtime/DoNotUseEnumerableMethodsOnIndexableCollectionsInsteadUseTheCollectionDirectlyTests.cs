// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyTests
    {
        [Fact]
        public async Task CSharpCases()
        {
            var code = @"
using System;
using System.Collections.Generic;
using System.Linq;

class D : IReadOnlyList<int>
{
    public int this[int index]
    {
        get { throw new NotImplementedException(); }
    }

    public int Count
    {
        get { throw new NotImplementedException(); }
    }

    public IEnumerator<int> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}

class C
{
    void Use<U>(U p) { } 

    void Test<T>()
    {
        T[] x1 = null;
        Use(x1.Last());
        Use(Enumerable.Last(x1));
        IReadOnlyList<T> x2 = null;
        Use(x2.Last());
        Use(Enumerable.Last(x2));
        D x3 = null;
        Use(x3.Last());
        Use(Enumerable.Last(x3));
        List<T> x4 = null;
        Use(x4.Last());
        Use(Enumerable.Last(x4));

        // Don't flag the version which takes a predicate
        Use(x1.Last(x => true));
        Use(Enumerable.Last(x1, (x => true)));
        Use(x2.Last(x => true));
        Use(Enumerable.Last(x2, (x => true)));
        Use(x3.Last(x => true));
        Use(Enumerable.Last(x3, (x => true)));
        Use(x4.Last(x => true));
        Use(Enumerable.Last(x4, (x => true)));

        // Make sure we flag other bad LINQ methods
        Use(x2.LastOrDefault());
        Use(x2.FirstOrDefault());
        Use(x2.First());
        Use(x2.Count());
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(39, 13),
                GetCSharpResultAt(40, 13),
                GetCSharpResultAt(42, 13),
                GetCSharpResultAt(43, 13),
                GetCSharpResultAt(59, 13),
                GetCSharpResultAt(60, 13),
                GetCSharpResultAt(61, 13),
                GetCSharpResultAt(62, 13));
        }

        [Fact]
        public async Task BasicCases()
        {
            var code = @"
Imports System
Imports System.Collections.Generic
Imports System.Linq

Class D
    Implements IReadOnlyList(Of Integer)
    Default Public ReadOnly Property Item(index As Integer) As Integer
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public ReadOnly Property Count() As Integer
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IReadOnlyList_Item(index As Integer) As Integer Implements IReadOnlyList(Of Integer).Item
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IReadOnlyCollection_Count As Integer Implements IReadOnlyCollection(Of Integer).Count
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Function GetEnumerator() As IEnumerator(Of Integer)
        Throw New NotImplementedException()
    End Function

    Private Function System_Collections_IEnumerable_GetEnumerator() As System.Collections.IEnumerator Implements System.Collections.IEnumerable.GetEnumerator
        Throw New NotImplementedException()
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class

Class C
    Private Sub Use(Of U)(p As U)
    End Sub

    Private Sub Test(Of T)()
        Dim x1 As T() = Nothing
        Use(x1.Last())
        Use(Enumerable.Last(x1))
        Dim x2 As IReadOnlyList(Of T) = Nothing
        Use(x2.Last())
        Use(Enumerable.Last(x2))
        Dim x3 As D = Nothing
        Use(x3.Last())
        Use(Enumerable.Last(x3))
        Dim x4 As List(Of T) = Nothing
        Use(x4.Last())
        Use(Enumerable.Last(x4))

        ' Don't flag the version which takes a predicate
        Use(x1.Last(Function(x) True))
        Use(Enumerable.Last(x1, (Function(x) True)))
        Use(x2.Last(Function(x) True))
        Use(Enumerable.Last(x2, (Function(x) True)))
        Use(x3.Last(Function(x) True))
        Use(Enumerable.Last(x3, (Function(x) True)))
        Use(x4.Last(Function(x) True))
        Use(Enumerable.Last(x4, (Function(x) True)))

        ' Make sure we flag other bad LINQ methods
        Use(x2.LastOrDefault())
        Use(x2.FirstOrDefault())
        Use(x2.First())
        Use(x2.Count())
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicResultAt(54, 13),
                GetBasicResultAt(55, 13),
                GetBasicResultAt(57, 13),
                GetBasicResultAt(58, 13),
                GetBasicResultAt(74, 13),
                GetBasicResultAt(75, 13),
                GetBasicResultAt(76, 13));
        }

        [Theory, WorkItem(1817, "https://github.com/dotnet/roslyn-analyzers/issues/1817")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA1826.exclude_ordefault_methods = true")]
        [InlineData("dotnet_code_quality.exclude_ordefault_methods = true")]
        [InlineData("dotnet_code_quality.CA1826.exclude_ordefault_methods = false")]
        [InlineData("dotnet_code_quality.exclude_ordefault_methods = false")]
        public async Task CA1826_EditorConfig_ExcludeOrDefaultMethods(string editorConfigText)
        {
            var csharpSource = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class C
{
    public C(IReadOnlyList<int> l)
    {
        l.First();
        l.Last();
        l.Count();
        l.FirstOrDefault();
        l.LastOrDefault();
    }
}";

            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { csharpSource, },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(10, 9),
                        GetCSharpResultAt(11, 9),
                        GetCSharpResultAt(12, 9),
                    },
                }
            };

            if (!editorConfigText.EndsWith("true", System.StringComparison.Ordinal))
            {
                csharpTest.ExpectedDiagnostics.Add(GetCSharpResultAt(13, 9));
                csharpTest.ExpectedDiagnostics.Add(GetCSharpResultAt(14, 9));
            }

            await csharpTest.RunAsync();

            var vbSource = @"
Imports System
Imports System.Collections.Generic
Imports System.Linq

Public Class C
    Public Sub New(ByVal l As IReadOnlyList(Of Integer))
        Use(l.First())
        Use(l.Last())
        Use(l.FirstOrDefault())
        Use(l.LastOrDefault())
    End Sub

    Private Sub Use(Of U)(p As U)
    End Sub
End Class";

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { vbSource, },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                    ExpectedDiagnostics =
                    {
                        GetBasicResultAt(8, 13),
                        GetBasicResultAt(9, 13),
                    },
                }
            };

            if (!editorConfigText.EndsWith("true", System.StringComparison.Ordinal))
            {
                vbTest.ExpectedDiagnostics.Add(GetBasicResultAt(10, 13));
                vbTest.ExpectedDiagnostics.Add(GetBasicResultAt(11, 13));
            }

            await vbTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}