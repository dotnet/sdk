// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyTests
    {
        [Fact]
        public async Task CSharpCasesAsync()
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
        Use([|x2.Last()|]);
        Use([|Enumerable.Last(x2)|]);
        D x3 = null;
        Use([|x3.Last()|]);
        Use([|Enumerable.Last(x3)|]);
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
        Use([|x2.LastOrDefault()|]);
        Use([|x2.FirstOrDefault()|]);
        Use([|x2.First()|]);
        Use([|x2.Count()|]);
    }
}
";
            var fixedCode = @"
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
        Use(x2[x2.Count - 1]);
        Use(x2[x2.Count - 1]);
        D x3 = null;
        Use(x3[x3.Count - 1]);
        Use(x3[x3.Count - 1]);
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
        Use([|x2.LastOrDefault()|]);
        Use([|x2.FirstOrDefault()|]);
        Use(x2[0]);
        Use(x2.Count);
    }
}
";
            await new VerifyCS.Test
            {
                TestCode = code,
                FixedState =
                {
                    Sources = { fixedCode },
                    MarkupHandling = MarkupMode.Allow,
                }
            }.RunAsync();
        }

        [Fact]
        public async Task BasicCasesAsync()
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
        Use([|x2.Last()|])
        Use([|Enumerable.Last(x2)|])
        Dim x3 As D = Nothing
        Use([|x3.Last()|])
        Use([|Enumerable.Last(x3)|])
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
        Use([|x2.LastOrDefault()|])
        Use([|x2.FirstOrDefault()|])
        Use([|x2.First()|])
        Use(x2.Count())
    End Sub
End Class
";

            var fixedCode = @"
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
        Use(x2(x2.Count - 1))
        Use(x2(x2.Count - 1))
        Dim x3 As D = Nothing
        Use([|x3.Last()|])
        Use([|Enumerable.Last(x3)|])
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
        Use([|x2.LastOrDefault()|])
        Use([|x2.FirstOrDefault()|])
        Use(x2(0))
        Use(x2.Count())
    End Sub
End Class
";
            await new VerifyVB.Test
            {
                TestCode = code,
                FixedState =
                {
                    Sources = { fixedCode },
                    MarkupHandling = MarkupMode.Allow,
                }
            }.RunAsync();
        }

        [Theory, WorkItem(1817, "https://github.com/dotnet/roslyn-analyzers/issues/1817")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA1826.exclude_ordefault_methods = true")]
        [InlineData("dotnet_code_quality.exclude_ordefault_methods = true")]
        [InlineData("dotnet_code_quality.CA1826.exclude_ordefault_methods = false")]
        [InlineData("dotnet_code_quality.exclude_ordefault_methods = false")]
        public async Task CA1826_EditorConfig_ExcludeOrDefaultMethodsAsync(string editorConfigText)
        {
            string csharpFirstOrDefaultAndLastOrDefault;
            if (!editorConfigText.EndsWith("true", System.StringComparison.Ordinal))
            {
                csharpFirstOrDefaultAndLastOrDefault = @"
        [|l.FirstOrDefault()|];
        [|l.LastOrDefault()|];
";
            }
            else
            {
                csharpFirstOrDefaultAndLastOrDefault = @"
        l.FirstOrDefault();
        l.LastOrDefault();
";
            }

            var csharpSource = $@"
using System;
using System.Collections.Generic;
using System.Linq;

public class C
{{
    public C(IReadOnlyList<int> l)
    {{
        [|l.First()|];
        [|l.Last()|];
        [|l.Count()|];
        {csharpFirstOrDefaultAndLastOrDefault}
    }}
}}";
            var csharpFixedSource = $@"
using System;
using System.Collections.Generic;
using System.Linq;

public class C
{{
    public C(IReadOnlyList<int> l)
    {{
        {{|CS0201:l[0]|}};
        {{|CS0201:l[l.Count - 1]|}};
        {{|CS0201:l.Count|}};
        {csharpFirstOrDefaultAndLastOrDefault}
    }}
}}"; ;

            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { csharpSource, },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
                FixedState =
                {
                    Sources = { csharpFixedSource, },
                    MarkupHandling = MarkupMode.Allow,
                }
            };

            await csharpTest.RunAsync();

            string vbFirstOrDefaultAndLastOrDefault;
            if (!editorConfigText.EndsWith("true", System.StringComparison.Ordinal))
            {
                vbFirstOrDefaultAndLastOrDefault = @"
        Use([|l.FirstOrDefault()|])
        Use([|l.LastOrDefault()|])
";
            }
            else
            {
                vbFirstOrDefaultAndLastOrDefault = @"
        l.FirstOrDefault()
        l.LastOrDefault()
";
            }

            var vbSource = $@"
Imports System
Imports System.Collections.Generic
Imports System.Linq

Public Class C
    Public Sub New(ByVal l As IReadOnlyList(Of Integer))
        Use([|l.First()|])
        Use([|l.Last()|])
        {vbFirstOrDefaultAndLastOrDefault}
    End Sub

    Private Sub Use(Of U)(p As U)
    End Sub
End Class";
            var vbFixedSource = $@"
Imports System
Imports System.Collections.Generic
Imports System.Linq

Public Class C
    Public Sub New(ByVal l As IReadOnlyList(Of Integer))
        Use(l(0))
        Use(l(l.Count - 1))
        {vbFirstOrDefaultAndLastOrDefault}
    End Sub

    Private Sub Use(Of U)(p As U)
    End Sub
End Class"; ;
            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { vbSource, },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
                FixedState =
                {
                    Sources = { vbFixedSource, },
                    MarkupHandling = MarkupMode.Allow,
                },
            };

            await vbTest.RunAsync();
        }
    }
}