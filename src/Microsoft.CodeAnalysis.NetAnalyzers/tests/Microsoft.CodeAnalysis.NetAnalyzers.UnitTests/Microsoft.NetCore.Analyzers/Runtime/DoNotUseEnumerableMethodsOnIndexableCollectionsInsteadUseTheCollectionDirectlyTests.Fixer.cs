// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
        [Fact]
        public async Task CA1826FixEnumerableFirstExtensionCallCSharp()
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

        [Fact]
        public async Task CA1826FixEnumerableFirstExtensionCallBasic()
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

        [Fact]
        public async Task CA1826FixEnumerableFirstStaticCallCSharp()
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

        [Fact]
        public async Task CA1826FixEnumerableFirstStaticCallBasic()
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

        [Fact]
        public async Task CA1826FixEnumerableFirstMethodChainCallCSharp()
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

        var s = [|GetList()
            .First()|]
            .ToString();
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

        var s = GetList()[0]
            .ToString();
    }

    IReadOnlyList<int> GetList()
    {
        return new List<int> { 1, 2, 3 };
    }
}
");
        }

        [Fact]
        public async Task CA1826FixEnumerableFirstInvalidStatementCSharp()
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