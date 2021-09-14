// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
    public class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixerTests
    {
        [Fact, WorkItem(1932, "https://github.com/dotnet/roslyn-analyzers/issues/1932")]
        public async Task CA1826_CSharp_EnumerableFirstExtensionCall()
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
        public async Task CA1826_Basic_EnumerableFirstExtensionCall()
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
        public async Task CA1826_CSharp_EnumerableLastExtensionCall()
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
        public async Task CA1826_Basic_EnumerableLastExtensionCall()
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
        public async Task CA1826_CSharp_EnumerableCountExtensionCall()
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
        public async Task CA1826_CSharp_EnumerableFirstStaticCall()
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
        public async Task CA1826_Basic_EnumerableFirstStaticCall()
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
        public async Task CA1826_CSharp_EnumerableLastStaticCall()
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
        public async Task CA1826_Basic_EnumerableLastStaticCall()
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
        public async Task CA1826_CSharp_EnumerableCountStaticCall()
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
        public async Task CA1826_Basic_EnumerableCountStaticCall()
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
        public async Task CA1826_CSharp_ChainCall()
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
        public async Task CA1826_CSharp_InvalidStatement()
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
    }
}