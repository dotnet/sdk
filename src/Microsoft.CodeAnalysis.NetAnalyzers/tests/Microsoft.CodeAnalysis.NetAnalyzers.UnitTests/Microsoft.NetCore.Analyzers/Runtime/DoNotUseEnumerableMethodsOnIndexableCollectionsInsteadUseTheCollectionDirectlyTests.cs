// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer>;

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
}}";

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
                },
                FixedState =
                {
                    Sources = { vbFixedSource, },
                    MarkupHandling = MarkupMode.Allow,
                },
            };

            await vbTest.RunAsync();
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableFirstExtensionCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = [|list.First()|];
        var f2 = [|GetList().First()|];
        var f3 = [|matrix[0].First()|];
        Console.WriteLine([|list.First()|]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = list[0];
        var f2 = GetList()[0];
        var f3 = matrix[0][0];
        Console.WriteLine(list[0]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_Basic_EnumerableFirstExtensionCallAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = [|list.First()|]
        Dim f2 = [|GetList().First()|]
        Dim f3 = [|matrix(0).First()|]
        Console.WriteLine([|list.First()|])
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
", @"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = list(0)
        Dim f2 = GetList()(0)
        Dim f3 = matrix(0)(0)
        Console.WriteLine(list(0))
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableLastExtensionCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = [|list.Last()|];
        var f2 = [|GetList().Last()|];
        var f3 = [|matrix[0].Last()|];
        Console.WriteLine([|list.Last()|]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = list[list.Count - 1];
        var f2 = GetList()[GetList().Count - 1];
        var f3 = matrix[0][matrix[0].Count - 1];
        Console.WriteLine(list[list.Count - 1]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_Basic_EnumerableLastExtensionCallAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = [|list.Last()|]
        Dim f2 = [|GetList().Last()|]
        Dim f3 = [|matrix(0).Last()|]
        Console.WriteLine([|list.Last()|])
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
", @"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = list(list.Count - 1)
        Dim f2 = GetList()(GetList().Count - 1)
        Dim f3 = matrix(0)(matrix(0).Count - 1)
        Console.WriteLine(list(list.Count - 1))
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableCountExtensionCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = [|list.Count()|];
        var f2 = [|GetList().Count()|];
        var f3 = [|matrix[0].Count()|];
        Console.WriteLine([|list.Count()|]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = list.Count;
        var f2 = GetList().Count;
        var f3 = matrix[0].Count;
        Console.WriteLine(list.Count);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableFirstStaticCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = [|Enumerable.First(list)|];
        var f2 = [|Enumerable.First(GetList())|];
        var f3 = [|Enumerable.First(matrix[0])|];
        Console.WriteLine([|Enumerable.First(list)|]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = list[0];
        var f2 = GetList()[0];
        var f3 = matrix[0][0];
        Console.WriteLine(list[0]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_Basic_EnumerableFirstStaticCallAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = [|Enumerable.First(list)|]
        Dim f2 = [|Enumerable.First(GetList())|]
        Dim f3 = [|Enumerable.First(matrix(0))|]
        Console.WriteLine([|Enumerable.First(list)|])
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
", @"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = list(0)
        Dim f2 = GetList()(0)
        Dim f3 = matrix(0)(0)
        Console.WriteLine(list(0))
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableLastStaticCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = [|Enumerable.Last(list)|];
        var f2 = [|Enumerable.Last(GetList())|];
        var f3 = [|Enumerable.Last(matrix[0])|];
        Console.WriteLine([|Enumerable.Last(list)|]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = list[list.Count - 1];
        var f2 = GetList()[GetList().Count - 1];
        var f3 = matrix[0][matrix[0].Count - 1];
        Console.WriteLine(list[list.Count - 1]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_Basic_EnumerableLastStaticCallAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = [|Enumerable.Last(list)|]
        Dim f2 = [|Enumerable.Last(GetList())|]
        Dim f3 = [|Enumerable.Last(matrix(0))|]
        Console.WriteLine([|Enumerable.Last(list)|])
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
", @"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = list(list.Count - 1)
        Dim f2 = GetList()(GetList().Count - 1)
        Dim f3 = matrix(0)(matrix(0).Count - 1)
        Console.WriteLine(list(list.Count - 1))
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableCountStaticCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = [|Enumerable.Count(list)|];
        var f2 = [|Enumerable.Count(GetList())|];
        var f3 = [|Enumerable.Count(matrix[0])|];
        Console.WriteLine([|Enumerable.Count(list)|]);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System;
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var list = GetList();
        var matrix = new[] { list };
        var f1 = list.Count;
        var f2 = GetList().Count;
        var f3 = matrix[0].Count;
        Console.WriteLine(list.Count);
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_Basic_EnumerableCountStaticCallAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = [|Enumerable.Count(list)|]
        Dim f2 = [|Enumerable.Count(GetList())|]
        Dim f3 = [|Enumerable.Count(matrix(0))|]
        Console.WriteLine([|Enumerable.Count(list)|])
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
", @"
Imports System
Imports System.Collections.Generic
#Disable Warning BC50001 'Unused import statement
Imports System.Linq
#Enable Warning BC50001

Class C
    Sub M()
        Dim list = GetList()
        Dim matrix = {list}
        Dim f1 = list.Count
        Dim f2 = GetList().Count
        Dim f3 = matrix(0).Count
        Console.WriteLine(list.Count)
    End Sub

    Function GetList() As IReadOnlyList(Of Integer)
        Return New List(Of Integer) From {1, 2, 3}
    End Function
End Class
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_ChainCallAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var f = [|GetList()
            .First()|];

        var fts = [|GetList()
            .First()|]
            .ToString();

        var l = [|GetList()
            .Last()|];

        var lts = [|GetList()
            .Last()|]
            .ToString();

        var c = [|GetList()
            .Count()|];
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System.Collections.Generic;
#pragma warning disable CS8019 //Unnecessary using directive
using System.Linq;
#pragma warning restore CS8019
class C
{
    void M()
    {
        var f = GetList()[0];

        var fts = GetList()[0]
            .ToString();

        var l = GetList()[GetList().Count - 1];

        var lts = GetList()[GetList().Count - 1]
            .ToString();

        var c = GetList().Count;
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_InvalidStatementAsync()
        {
            //this unit test documents a problematic edge case
            //the fixed code triggers an error - CS0201: Only assignment, call, increment, decrement, and new object expressions can be used as a statement
            //the decision was to let this fly because even though the initial code itself is syntactically correct, it doesn't really make much sense due to the return value of the 'First' method call not being used

            await VerifyCS.VerifyCodeFixAsync(@"
using System.Linq;
using System.Collections.Generic;
class C
{
    void M()
    {
        [|GetList().First()|];
        [|GetList().Last()|];
        [|GetList().Count()|];
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
", @"
using System.Linq;
using System.Collections.Generic;
class C
{
    void M()
    {
        {|CS0201:GetList()[0]|};
        {|CS0201:GetList()[GetList().Count - 1]|};
        {|CS0201:GetList().Count|};
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact, WorkItem(5795, "https://github.com/dotnet/roslyn-analyzers/issues/5795")]
        public async Task CA1826_CSharp_NullForgivingOperator()
        {
            var source = @"
#nullable enable

using System.Collections.Generic;
using System.Linq;

public class Test
{
    public static IReadOnlyList<string>? Strings { get; }

    public static string Method()
    {
        return [|Strings!.Last()|];
    }
}
";
            var fixedSource = @"
#nullable enable

using System.Collections.Generic;
using System.Linq;

public class Test
{
    public static IReadOnlyList<string>? Strings { get; }

    public static string Method()
    {
        return Strings![Strings.Count - 1];
    }
}
";
            // The fixed code has extra unnecessary parentheses.
            // This is fixed on Roslyn side in https://github.com/dotnet/roslyn/pull/58903
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }
    }
}
