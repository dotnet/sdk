// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer();
        }

        [Fact]
        public void CSharpCases()
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

            VerifyCSharp(code,
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
        public void BasicCases()
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
            VerifyBasic(code,
                GetBasicResultAt(54, 13),
                GetBasicResultAt(55, 13),
                GetBasicResultAt(57, 13),
                GetBasicResultAt(58, 13),
                GetBasicResultAt(74, 13),
                GetBasicResultAt(75, 13),
                GetBasicResultAt(76, 13));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.RuleId, MicrosoftNetCoreAnalyzersResources.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyMessage);
        }

        private static DiagnosticResult GetBasicResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.RuleId, MicrosoftNetCoreAnalyzersResources.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyMessage);
        }
    }
}