// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines.CSharpAvoidMultipleEnumerationsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicAvoidMultipleEnumerationsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    public class AvoidMultipleEnumerationsTests
    {
        private static Task VerifyCSharpAsync(
            string code,
            string customizedEnumerationMethods = null,
            string customizedLinqChainMethods = null,
            bool? assumeMethodEnumeratesParameters = null)
        {
            var enumerationMethods = customizedEnumerationMethods == null
                ? string.Empty
                : $"dotnet_code_quality.CA1851.enumeration_methods = {customizedEnumerationMethods}";
            var linqChainMethods = customizedLinqChainMethods == null
                ? string.Empty
                : $"dotnet_code_quality.CA1851.linq_chain_methods = {customizedLinqChainMethods}";
            var assumeMethodEnumeratesParametersConfig = assumeMethodEnumeratesParameters switch
            {
                true => $"dotnet_code_quality.CA1851.assume_method_enumerates_parameters = true",
                false => $"dotnet_code_quality.CA1851.assume_method_enumerates_parameters  = false",
                _ => string.Empty,
            };

            var test = new VerifyCS.Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                LanguageVersion = CSharp.LanguageVersion.Latest,
                TestState =
                {
                    Sources =
                    {
                        code
                    },
                    AnalyzerConfigFiles = { ("/.editorConfig", $@"root = true
[*]
{enumerationMethods}
{linqChainMethods}
{assumeMethodEnumeratesParametersConfig}
") },
                },
            };

            return test.RunAsync();
        }

        private static Task VerifyVisualBasicAsync(
            string code,
            string customizedEnumerationMethods = null,
            string customizedLinqChainMethods = null,
            bool? assumeMethodEnumeratesParameters = null)
        {
            var enumerationMethods = customizedEnumerationMethods == null
                ? string.Empty
                : $"dotnet_code_quality.CA1851.enumeration_methods = {customizedEnumerationMethods}";
            var linqChainMethods = customizedLinqChainMethods == null
                ? string.Empty
                : $"dotnet_code_quality.CA1851.linq_chain_methods = {customizedLinqChainMethods}";

            var assumeMethodEnumeratesParametersConfig = assumeMethodEnumeratesParameters switch
            {
                true => $"dotnet_code_quality.CA1851.assume_method_enumerates_parameters = true",
                false => $"dotnet_code_quality.CA1851.assume_method_enumerates_parameters = false",
                _ => string.Empty,
            };
            var test = new VerifyVB.Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                LanguageVersion = VisualBasic.LanguageVersion.Latest,
                TestState =
                {
                    Sources =
                    {
                        code
                    },
                    AnalyzerConfigFiles = { ("/.editorConfig", $@"root = true
[*]
{enumerationMethods}
{linqChainMethods}
{assumeMethodEnumeratesParametersConfig}
") },
                },
            };

            return test.RunAsync();
        }

        [Fact]
        public async Task TestMultipleInvocations()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int[] j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var c = [|i|].All(x => x == 100);
        var d = [|i|].Any();
        var e = j.Average();
        var f = j.Count();
        var g = [|i|].Average();
        var h = [|i|].Count();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As Integer())
            Dim i = Enumerable.Range(1, 10)
            Dim c = [|i|].All(Function(x) x = 100)
            Dim d = [|i|].Any()
            Dim e = j.Average()
            Dim f = j.Count()
            Dim g = [|i|].Average()
            Dim h = [|i|].Count()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestForLoop()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int[] j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        for (int k = 0; k < 100; k++)
        {
            [|i|].Aggregate((m, n) => m + n);
            j.Contains(100);
            [|i|].Contains(100);
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As Integer())
            Dim i = Enumerable.Range(1, 10)
            For index = 1 To 10
                [|i|].Aggregate(Function(m, n) m + n)
                j.Contains(100)
                [|i|].Contains(100)
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationsInForEachLoop()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        var i = Enumerable.Range(1, 10).ToArray();
        foreach (var c in Enumerable.Range(1, 10))
        {
            i.Count();
            [|j|].DefaultIfEmpty().MaxBy(k => k);
        }
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As Integer())
            Dim i = Enumerable.Range(1, 10)
            For Each index in Enumerable.Range(1, 10)
                [|i|].Aggregate(Function(m, n) m + n)
                j.Contains(100)
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationsAfterForEachLoop()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|])
        {
        }
        [|i|].Count();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i = Enumerable.Range(1, 10)

            For Each index in [|i|]
            Next
            [|i|].Count()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationsAfterForEachLoopForArray()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        var i = Enumerable.Range(1, 10).ToArray();
        foreach (var c in i.Select(p => p))
        {
        }
        i.Count();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i = Enumerable.Range(1, 10).ToArray()

            For Each index in i
            Next
            i.Count()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestWhileLoop()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        while (true)
        {
            [|i|].ElementAt(100);
            [|j|].ElementAtOrDefault(100);
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(of Integer))
            Dim i As IEnumerable(Of Integer)= Enumerable.Range(1, 10)

            While (true)
                [|i|].ElementAt(100)
                [|j|].ElementAtOrDefault(100)
            End While
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationAfterIfBranchWithElse()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int b, IEnumerable<int> j)
    {
        var i = Enumerable.Range(1, 10).ToArray();
        if (b == 1)
        {
            i.First();
            [|j|].FirstOrDefault();
        }
        else if (b == 3)
        {
            i.Last();
            [|j|].LastOrDefault();
        }
        else
        {
            i.LongCount();
            [|j|].Max();
        }

        i.Min();
        [|j|].Single();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(b As Integer, j As IEnumerable(Of Integer))
            Dim i = Enumerable.Range(1, 10).ToArray()
            If b = 1 Then
                i.First()
                [|j|].FirstOrDefault()
            ElseIf b = 3 Then
                i.Last()
                [|j|].LastOrDefault()
            Else
                i.LongCount()
                [|j|].Max()
            End If

            i.Min()
            [|j|].Single()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationAfterIfbranch()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h, int x)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (x == 1)
        {
            [|i|].Except([|h|]).ToList();
            [|i|].Intersect([|h|]).ToList();
        }

        [|i|].Max();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer), x As Integer)
            Dim i = Enumerable.Range(1, 10)
            If x = 1 Then
                [|i|].Except([|h|]).ToList()
                [|i|].Intersect([|h|]).ToList()
            End If

            [|i|].Max()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationBeforeIfbranch()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h, int x)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        [|i|].Max();
        if (x == 1)
        {
            [|i|].Except([|h|]).ToList();
            [|i|].Intersect([|h|]).ToList();
        }

        [|i|].Max();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer), x As Integer)
            Dim i = Enumerable.Range(1, 10)
            [|i|].Max()
            If x = 1 Then
                [|i|].Except([|h|]).ToList()
                [|i|].Intersect([|h|]).ToList()
            End If

            [|i|].Max()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationAfterIfbranchForArray()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h, int x)
    {
        var i = Enumerable.Range(1, 10).ToArray();
        if (x == 1)
        {
            i.Except([|h|]).ToList();
            i.Intersect([|h|]).ToList();
        }

        i.Max();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer), x As Integer)
            Dim i = Enumerable.Range(1, 10).ToArray()
            If x = 1 Then
                i.Except([|h|]).ToList()
                i.Intersect([|h|]).ToList()
            End If

            i.Max()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestNoInvocationAfterIfbranch()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h, int x)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (x == 1)
        {
            [|i|].Union([|h|]).ToList();
            [|i|].Join([|h|], n => n, n => n, (n, m) => n + m).ToList();
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer), x As Integer)
            Dim i = Enumerable.Range(1, 10)
            If x = 1 Then
                [|i|].Union([|h|]).ToList()
                [|i|].Join([|h|], Function(n) n, Function(n) n, Function(m, n) m + n).ToList()
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestOneInvocationAfterIfBranch1()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int b, int[] c)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            [|i|].SingleOrDefault();
            c.Max();
        }
        else if (b == 3)
        {
            [|i|].ToList();
            c.Min();
        }
        else
        {
            [|i|].Min();
            c.Average();
        }

        [|i|].Sum();
        c.First();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(b As Integer, c As Integer())
            Dim i = Enumerable.Range(1, 10)
            If b = 1 Then
                [|i|].SingleOrDefault()
                c.Max()
            Else If b = 3
                [|i|].ToList()
                c.Min()
            Else
                [|i|].Min()
                c.Average()
            End If

            [|i|].Sum()
            c.First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestOneInvocationAfterIfBranch2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int b, IEnumerable<int> j)
    {
        var i = Enumerable.Range(1, 10).ToArray();
        i.First();
        [|j|].SingleOrDefault();

        if (b == 1)
        {
            i.First();
            [|j|].First();
        }
        else if (b == 3)
        {
            i.First();
            [|j|].First();
        }
        else if (b == 5)
        {
            i.First();
            [|j|].First();
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(b As Integer, j As IEnumerable(Of Integer))
            Dim i = Enumerable.Range(1, 10).ToArray()
            i.First()
            [|j|].SingleOrDefault()

            If b = 1 Then
                i.First()
                [|j|].First()
            Else If b = 3
                i.First()
                [|j|].First()
            Else
                i.First()
                [|j|].First()
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestTwoInvocationsAfterIfElseBranch()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(bool flag, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var k = Enumerable.Range(1, 10).ToArray();
        if (flag)
        {
            [|i|].Single();
            [|j|].SingleOrDefault();
            k.Single();
        }
        else
        {
            [|i|].Sum();
            [|j|].ToArray();
            k.Last();
        }

        [|i|].ToDictionary(x => x);
        [|j|].First();
        k.LastOrDefault();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(flag As Boolean, j As IEnumerable(Of Integer))
            Dim i = Enumerable.Range(1, 10)
            Dim k = Enumerable.Range(1, 10).ToArray()

            If flag Then
                [|i|].Single()
                [|j|].SingleOrDefault()
                k.Single()
            Else
                [|i|].Sum()
                [|j|].ToArray()
                k.Last()
            End If

            [|i|].ToDictionary(Function(x) x)
            [|j|].First()
            k.LastOrDefault()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestExplicitDeclaration()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(bool flag)
    {
        IEnumerable<int> k = Enumerable.Range(1, 10).ToArray();
        if (flag)
        {
            k.Single();
        }
        else
        {
            k.Last();
        }

        k.LastOrDefault();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(flag As Boolean)
            Dim k As IEnumerable(Of Integer) = Enumerable.Range(1, 10).ToArray()

            If flag Then
                k.Single()
            Else
                k.Last()
            End If

            k.LastOrDefault()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestTwoInvocationsAfterIfBranch()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(bool flag, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var k = Enumerable.Range(1, 10).ToArray();
        if (flag)
        {
            [|i|].Single();
            [|j|].SingleOrDefault();
            k.Sum();
        }

        [|i|].ToDictionary(x => x);
        [|i|].Max();

        [|j|].First();
        [|j|].Min();

        k.Sum();
        k.Sum();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(flag As Boolean, j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Dim k = Enumerable.Range(1, 10).ToArray()

            If flag Then
                [|i|].Single()
                [|j|].SingleOrDefault()
                k.Sum()
            End If
        
            [|i|].ToDictionary(Function(x) x)
            [|i|].Max()

            [|j|].First()
            [|j|].Min()

            k.Sum()
            k.Sum()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDifferntInvocationsInIfBranch()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int b, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var k = Enumerable.Range(1, 10).ToArray();
        if (b == 0)
        {
            [|i|].Single();
            k.ElementAt(10);
        }
        else if (b == 1)
        {
            [|j|].SingleOrDefault();
            k.ElementAtOrDefault(10);
        }

        [|i|].ToDictionary(x => x);
        [|i|].Max();

        [|j|].Min();
        [|j|].First();

        k.ToHashSet();
        k.LongCount();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(b As Boolean, j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Dim k = Enumerable.Range(1, 10).ToArray()

            If b = 0 Then
                [|i|].Single()
                k.ElementAt(10)
            Else if b = 1 Then
                [|j|].SingleOrDefault()
                k.ElementAtOrDefault(10)
            End If
        
            [|i|].ToDictionary(Function(x) x)
            [|i|].Max()

            [|j|].Min()
            [|j|].First()

            k.ToHashSet()
            k.LongCount()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDifferntInvocationsInIfBranch2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int b, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 0)
        {
            [|i|].Single();
        }
        else if (b == 1)
        {
            [|j|].SingleOrDefault();
        }

        [|i|].ToDictionary(x => x);
        [|j|].First();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(b As Boolean, j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If b = 0 Then
                [|i|].Single()
            Else if b = 1 Then
                [|j|].SingleOrDefault()
            End If
        
            [|i|].ToDictionary(Function(x) x)
            [|j|].First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDifferntInvocationsInIfBranch3()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int b, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 0)
        {
            [|i|].Single();
        }
        else
        {
            [|j|].SingleOrDefault();
        }

        [|i|].ToDictionary(x => x);
        [|i|].ToDictionary(x => x);

        [|j|].First();
        [|j|].First();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(b As Integer, j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If b = 0 Then
                [|i|].Single()
            Else if b = 1 Then
                [|j|].SingleOrDefault()
            End If
        
            [|i|].ToDictionary(Function(x) x)
            [|i|].ToDictionary(Function(x) x)
            [|j|].First()
            [|j|].First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch1()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if ([|i|].Any())
        {
            [|i|].Single();
        }
        else
        {
            [|i|].SingleOrDefault();
        }
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If [|i|].Any() Then
                [|i|].Single()
            Else
                [|i|].SingleOrDefault()
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if ([|i|].Any())
        {
        }
        else if ([|i|].Max() == 10)
        {
        }

        var j = Enumerable.Range(1, 10).ToArray();
        if (j.Any())
        {
        }
        else if (j.Max() == 10)
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If [|i|].Any() Then
            Else if [|i|].Max() = 10 Then
            End If
            
            Dim j = Enumerable.Range(1, 10).ToArray()
            If j.Any() Then
            Else if j.Max() = 10 Then
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch3()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if ([|i|].Any())
        {
            [|i|].ToArray();
        }
        else if ([|i|].Max() == 10)
        {
        }

        var j = Enumerable.Range(1, 10).ToArray();
        if (j.Any())
        {
            j.ToDictionary(l => l);
        }
        else if (j.Max() == 10)
        {
        }
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If [|i|].Any() Then
                [|i|].ToArray()
            Else if [|i|].Max() = 10 Then
            End If
            
            Dim j = Enumerable.Range(1, 10).ToArray()
            If j.Any() Then
                j.ToDictionary(Function(l) l)
            Else if j.Max() = 10 Then
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch4()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if ([|i|].Any() && [|i|].Max() == 10)
        {
        }
        var j = Enumerable.Range(1, 10).ToArray();
        if (j.Any() && j.Max() == 10)
        {
        }
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If [|i|].Any() AndAlso [|i|].Max() = 10 Then
            End If

            If [|i|].Any() And [|i|].Max() = 10 Then
            End If
            
            Dim j = Enumerable.Range(1, 10).ToArray()
            If j.Any() AndAlso j.Max() = 10 Then
            End If

            If j.Any() And j.Max() = 10 Then
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch5()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if ([|i|].Any() || [|i|].Max() == 10)
        {
        }

        var j = Enumerable.Range(1, 10).ToArray();
        if (j.Any() || j.Max() == 10)
        {
        }
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If [|i|].Any() OrElse [|i|].Max() = 10 Then
            End If

            If [|i|].Any() Or [|i|].Max() = 10 Then
            End If
            
            Dim j = Enumerable.Range(1, 10).ToArray()
            If j.Any() OrElse j.Max() = 10 Then
            End If

            If j.Any() Or j.Max() = 10 Then
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch6()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        while ([|i|].Min() == 10)
        {
        }

        var j = Enumerable.Range(1, 10).ToArray();
        while (j.Min() == 10)
        {
        }
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            While [|i|].Min() = 10
            End While

            Dim j = Enumerable.Range(1, 10).ToArray()
            While j.Min() = 10
            End While
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationOnBranch7()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if ([|i|].Any())
        {
        }

        [|i|].Min();

        var j = Enumerable.Range(1, 10).ToArray();
        if (j.Any())
        {
        }

        j.Min();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            If [|i|].Any()
            End If

            [|i|].Min()

            Dim j = Enumerable.Range(1, 10).ToArray()
            If j.Any()
            End If
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop1()
        {
            var csharpCode = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|])
        {
        }

        [|i|].ToHashSet();

        foreach (var c2 in [|j|])
        {
        }

        [|j|].ToList();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            
            For Each c In [|i|]
            Next
            [|i|].ToHashSet()
            
            For Each c2 In [|j|]
            Next
            [|j|].ToHashSet()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|].Select(m => m + 1).Where(m => m != 100))
        {
        }

        [|i|].ToLookup(x => x);

        foreach (var c2 in [|j|].Select(m => m + 1).Where(m => m != 100))
        {
        }

        [|j|].ToImmutableArray();

        var k = Enumerable.Range(1, 10).ToArray();
        foreach (var c3 in k.Select(m => m + 1).Where(m => m != 100))
        {
        }
        k.ToImmutableArray();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            
            For Each c In [|i|].Select(Function(m) m + 1).Where(Function(m) m <> 100)
            Next
            [|i|].ToLookUp(Function(x) x)
            
            For Each c2 In [|j|].Select(Function(m) m + 1).Where(Function(m) m <> 100)
            Next
            [|j|].ToHashSet()

            Dim k = Enumerable.Range(1, 10).ToArray

            For Each c3 In k.Select(Function(m) m + 1).Where(Function(m) m <> 100)
            Next
            k.ToImmutableArray()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop3()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|].Select(m => m + 1).Where(m => m != 100))
        {
            [|i|].Where(x => x != 100).ToImmutableList();
        }

        [|i|].Select(k => k + 1).Skip(100).First();

        foreach (var c2 in [|j|].Select(m => m + 1).Where(m => m != 100))
        {
            [|j|].Where(x => x != 100).ToImmutableSortedDictionary(m => m, n => n);
        }

        [|j|].Select(k => k + 1).Skip(100).First();

        var o = Enumerable.Range(1, 10).ToArray();
        foreach (var c3 in o.Select(m => m + 1).Where(m => m != 100))
        {
            o.Where(x => x != 100).ToImmutableSortedDictionary(m => m, n => n);
        }

        o.Select(k => k + 1).Skip(100).First();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            
            For Each c In [|i|].Select(Function(m) m + 1).Where(Function(m) m <> 100)
                [|i|].Where(Function(x) x <> 100).ToImmutableList()
            Next
            [|i|].Select(Function(k) k + 1).Skip(100).First()
            
            For Each c2 In [|j|].Select(Function(m) m + 1).Where(Function(m) m <> 100)
                [|j|].Where(Function(x) x <> 100).ToImmutableSortedDictionary(Function(m) m, Function(n) n)
            Next
            [|j|].Select(Function(k) k + 1).Skip(100).First()

            Dim o = Enumerable.Range(1, 10).ToArray
            For Each c3 In o.Select(Function(m) m + 1).Where(Function(m) m <> 100)
                o.Where(Function(x) x <> 100).ToImmutableSortedDictionary(Function(m) m, Function(n) n)
            Next
            o.Select(Function(k) k + 1).Skip(100).First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationAcceptObject()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod(i);
        i.First();

        TestMethod(h);
        h.First();
    }

    public void TestMethod(object o)
    {
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            TestMethod(i)
            i.First()
            
            TestMethod(h)
            h.First()
        End Sub

  
        Public Sub TestMethod(o As Object)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationAcceptGenerics()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod(i);
        i.First();
        i.TestMethod2();

        TestMethod(h);
        h.First();
        h.TestMethod2();
    }

    public void TestMethod<T>(T o)
    {
    }
}

public static class Ex 
{
    public static void TestMethod2<T>(this T o)
    {
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            TestMethod(i)
            i.First()
            i.TestMethod2()

            TestMethod(h)
            h.First()
            h.TestMethod2()
        End Sub

        Public Sub TestMethod(Of T)(o As T)
        End Sub
    End Class
End Namespace

Module Ex

    <Extension()>
    Public Sub TestMethod2(Of T)(q As T)
    End Sub
End Module
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationAcceptGenericsConstraintToIEnumerable()
        {
            var csharpCode = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod(i);
        i.First();

        TestMethod(h);
        h.First();
    }

    public void TestMethod<T>(T o) where T : IEnumerable<int>
    {
        var x = o;
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            TestMethod(i)
            i.First()
            
            TestMethod(h)
            h.First()
        End Sub

  
        Public Sub TestMethod(Of T As IEnumerable(Of Integer))(o As T)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestExplictInvocations()
        {
            var csharpCode = @"
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        Enumerable.First(predicate: x => x != 100, source: [|i|]);
        [|i|].ToImmutableSortedSet();

        Enumerable.First(predicate: x => x != 100, source: [|h|]);
        [|h|].First();
    }
}";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Enumerable.First(predicate:= Function(x) x <> 100, source:= [|i|])
            [|i|].ToImmutableSortedSet()
            
            Enumerable.First(predicate:= Function(x) x <> 100, source:= [|h|])
            [|h|].First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestTakesTwoIEnumerables1()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        [|i|].Concat([|h|]).ToArray();
        [|h|].Concat([|i|]).ToList();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            [|i|].Concat([|h|]).ToArray()
            [|h|].Concat([|i|]).ToList()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestTakesTwoIEnumerables2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        [|i|].SequenceEqual([|h|]);
        [|h|].SequenceEqual([|i|]);
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            [|i|].SequenceEqual([|h|])
            [|h|].SequenceEqual([|i|])
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestTakesTwoIEnumerables3()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> x = Enumerable.Range(1, 10);
        [|x|].GroupJoin([|h|], i => i, i => i, (i, ints) => i).First();
        [|x|].GroupJoin([|h|], i => i, i => i, (i, ints) => i).Last();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim x As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            [|x|].GroupJoin([|h|], Function(i) i, Function(i) i, Function(i, ints) i).First()
            [|x|].GroupJoin([|h|], Function(i) i, Function(i) i, Function(i, ints) i).Last()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDelayExecutions()
        {
            var csharpCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> x = Enumerable.Range(1, 10);
        var c1 = [|x|].Append(1).Cast<Object>().Distinct()
            .OfType<int>().OrderBy(i => i).OrderByDescending(i => i)
            .ThenBy(i => i).ThenByDescending(i => i)
            .Prepend(1).Reverse().Select(i => i + 1).Skip(100)
            .SkipWhile(i => i == 99).SkipLast(100).Take(1).TakeWhile(i => i == 100).TakeLast(100)
            .Where(i => i != 10).ToArray();

        var c2 = [|x|].Append(1).Cast<Object>().Distinct()
            .OfType<int>().OrderBy(i => i).OrderByDescending(i => i)
            .ThenBy(i => i).ThenByDescending(i => i)
            .Prepend(1).Reverse().Select(i => i + 1).Skip(100)
            .SkipWhile(i => i == 99).SkipLast(100).Take(1).TakeWhile(i => i == 100).TakeLast(100)
            .Where(i => i != 10).First();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim x As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Dim c1 = [|x|].Append(1).Cast(Of Object)().Distinct().
                OfType(Of Integer).OrderBy(Function(i) i).OrderByDescending(Function(i) i).
                ThenBy(Function(i) i).ThenByDescending(Function(i) i).
                Prepend(1).Reverse().Select(Function(i) i + 1).Skip(100).
                SkipWhile(Function(i) i = 99).SkipLast(100).Take(1).TakeWhile(Function(i) i = 100).TakeLast(100).
                Where(Function(i) i <> 10).ToArray()

            Dim c2 = [|x|].Append(1).Cast(Of Object)().Distinct().
                OfType(Of Integer).OrderBy(Function(i) i).OrderByDescending(Function(i) i).
                ThenBy(Function(i) i).ThenByDescending(Function(i) i).
                Prepend(1).Reverse().Select(Function(i) i + 1).Skip(100).
                SkipWhile(Function(i) i = 99).SkipLast(100).Take(1).TakeWhile(Function(i) i = 100).TakeLast(100).
                Where(Function(i) i <> 10).First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestGroupBy()
        {
            var csharpCode = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> x = Enumerable.Range(1, 10);
        var z = [|x|].GroupBy(i => i.GetHashCode()).ToArray();
        var z2 = [|x|].GroupBy(i => i.GetHashCode()).ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim x As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Dim z = [|x|].GroupBy(Function(i) i.GetHashCode()).ToArray()
            Dim z2= [|x|].GroupBy(Function(i) i.GetHashCode()).ToArray()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestSelectMany()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<IEnumerable<int>> y)
    {
        var a = [|y|].SelectMany(x => x).ToArray();
        var b = [|y|].SelectMany(x => x.ToArray()).ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(y As IEnumerable(Of IEnumerable(Of Integer)))
            Dim a = [|y|].SelectMany(Function(x) x).ToArray()
            Dim b = [|y|].SelectMany(Function(x) x.ToArray()).ToArray()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestIEnumerable()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable h)
    {
        [|h|].OfType<int>().ToArray();
        [|h|].Cast<int>().ToList();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable)
            [|h|].OfType(Of Integer).ToArray()
            [|h|].Cast(Of Integer).ToList()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestIOrderedEnumerable()
        {
            var csharpCode = @"
using System.Linq;

public class Bar
{
    public void Sub(IOrderedEnumerable<int> h)
    {
        [|h|].Where(i => i != 10).ToArray();
        [|h|].ToList();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IOrderedEnumerable(Of Integer))
            [|h|].Where(Function(i) i <> 10).ToArray()
            [|h|].ToList()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestExecutionInTheMiddle()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        [|h|].ToArray().Select(j => j + 1).Where(x => x != 100);
        [|h|].ToArray().Select(j => j + 1).Where(x => x != 100);
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            [|h|].ToArray().Select(Function(j) j + 1).Where(Function(x) x <> 100)
            [|h|].ToArray().Select(Function(j) j + 1).Where(Function(x) x <> 100)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestAssignmentAfterEnumeration()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        var d = [|h|].ToArray();
        var d2 = [|h|].ToArray();

        h = Enumerable.Range(1, 10);
        h.First();

        h = Enumerable.Range(1, 100);
        foreach (var i in h)
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim d = [|h|].ToArray()
            Dim d2 = [|h|].ToArray()
            
            h = Enumerable.Range(1, 10)
            h.First()

            h = Enumerable.Range(1, 100)
            For Each i In h
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestForEachLoopForLocalAssignment()
        {
            var csharpCode = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub()
    {
        var d = Enumerable.Range(1, 100);
        foreach (var i in [|d|])
        {
        }

        var e = d;
        foreach (var i in [|e|])
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim d = Enumerable.Range(1, 100)
            For Each i In [|d|]
            Next

            Dim e = d
            For Each i In [|e|]
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationLocalAssignment()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        [|h|].ToArray();
        var c = h;

        [|c|].First();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            [|h|].ToArray()
            Dim c = h
            [|c|].First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestInvocationWithAssignment()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(bool flag, IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var j = i;
        var k = j;

        var n = h;
        var m = n;
        
        if (flag)
        {
            foreach (var x in [|k|])
            {
            }

            foreach (var x in [|m|])
            {
            }
        }
        else
        {
            var d = [|i|].First();
            var d2 = [|h|].First();
        }
        
        foreach (var z in [|j|])
        {
        }

        foreach (var z in [|n|])
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(flag As Boolean, h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Dim j = i
            Dim k = j
            
            Dim n = h
            Dim m = n

            If Flag Then
                For Each x in [|k|]
                Next

                For Each x in [|m|]
                Next
            Else
                Dim d = [|i|].First()
                Dim d2 = [|h|].First()
            End If

            For Each z in [|j|]
            Next

            For Each z in [|n|]
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestForEachForIEnumerable()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable h)
    {
        foreach (var i in [|h|])
        {
        }

        foreach (var i in [|h|])
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable)
            For Each i in [|h|]
            Next

            For Each i in [|h|]
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestForEachForIOrderedEnumerable()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IOrderedEnumerable<int> h)
    {
        foreach (var i in [|h|])
        {
        }

        foreach (var i in [|h|])
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IOrderedEnumerable(Of Integer))
            For Each i in [|h|]
            Next

            For Each i in [|h|]
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestForEachLoop()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        foreach (var i in [|h|])
        {
        }

        foreach (var i in [|h|])
        {
        }
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(h As IEnumerable(Of Integer))
            For Each item In [|h|]
            Next

            For Each item In [|h|]
            Next
        End Sub
    End Class
End Namespace";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedParameterAfterLinqCallChain()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IEnumerable<int> j, IEnumerable<int> k)
    {
        var z = [|k|].Concat([|j|]).Concat(i);
        [|j|].ElementAt(10);
        z.ToArray();
        [|k|].ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), j As IEnumerable(Of Integer), k As IEnumerable(Of Integer))
            Dim z = [|k|].Concat([|j|]).Concat(i)
            [|j|].ElementAt(10)
            z.ToArray()
            [|k|].ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedLocalAfterLinqCallChain1()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IEnumerable<int> k)
    {
        var j = Enumerable.Range(1, 10);
        var z = i.Concat([|j|]).Concat([|k|]);
        [|j|].ElementAt(10);
        z.ToArray();
        [|k|].ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), k As IEnumerable(Of Integer))
            Dim j = Enumerable.Range(1, 10)
            Dim z = i.Concat([|j|]).Concat([|k|])
            [|j|].ElementAt(10)
            z.ToArray()
            [|k|].ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedLocalAfterLinqCallChain2()
        {
            var csharp = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IEnumerable<int> k)
    {
        var j = Enumerable.Range(1, 10).Except([|i|]).Except([|k|]);
        var z = [|i|].Concat([|k|]);
        j.ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharp);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), k As IEnumerable(Of Integer))
            Dim j = Enumerable.Range(1, 10).Except([|i|]).Except([|k|])
            Dim z = [|i|].Concat([|k|])
            j.ElementAt(10)
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedLocalAfterLinqCallChain3()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i)
    {
        var j = Enumerable.Range(1, 10);
        var p = j;
        var z = i.Concat([|j|]).Concat([|p|]);
        [|j|].ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer))
            Dim j = Enumerable.Range(1, 10)
            Dim p = j
            Dim z = i.Concat([|j|]).Concat([|p|])
            [|j|].ElementAt(10)
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestConcatOneParameterMultipleTimes()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IEnumerable<int> k)
    {
        var z = i.Except([|k|]).Concat([|k|]);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), k As IEnumerable(Of Integer))
            Dim z = i.Except([|k|]).Concat([|k|])
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedLocalWithMultipleAbstractLocations1()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IEnumerable<int> j, bool flag)
    {
        var a = flag ? i : j;
        var b = [|a|].Except([|i|]);

        [|a|].ElementAt(10);
        [|i|].ElementAt(10);
        b.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), j As IEnumerable(Of Integer), flag As Boolean)
            Dim a = If(flag, i, j)
            Dim b = [|a|].Except([|i|])

            [|a|].ElementAt(10)
            [|i|].ElementAt(10)
            b.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedLocalWithMultipleAbstractLocations2()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, int[] j, bool flag)
    {
        var a = flag ? i : j;
        var b = a.Except(j);

        a.ElementAt(10);
        i.ElementAt(10);
        b.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), j As Integer(), flag As Boolean)
            Dim a = If(flag, i, j)
            Dim b = a.Except(j)

            a.ElementAt(10)
            i.ElementAt(10)
            b.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestEnumeratedLocalWithMultipleAbstractLocations3()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int[] i, int[] j, bool flag)
    {
        var a = flag ? i : j;
        var b = a.Except(j);

        [|b|].ToArray();
        [|b|].ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As Integer(), j As Integer(), flag As Boolean)
            Dim a = If(flag, i, j)
            Dim b = a.Except(j)

            [|b|].ToArray()
            [|b|].ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDelayEnumerableFromArray()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, int[] j)
    {
        var z = j.Concat(i);
        j.ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), j As Integer())
            Dim z = j.Concat(i)

            j.ElementAt(10)
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDelayIOrderedEnumerable()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IOrderedEnumerable<int> j, IEnumerable<int> k)
    {
        var z = i.Concat([|j|]).Concat(k);
        [|j|].ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), j As IOrderedEnumerable(Of Integer), k As IEnumerable(Of Integer))
            Dim z = i.Concat([|j|]).Concat(k)
            [|j|].ElementAt(10)
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestNestedDelayIEnumerable()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i, IOrderedEnumerable<int> j, IEnumerable<int> k)
    {
        var z = i.Concat(k.Concat([|j|]).Select(p => p).Where(p => p != 100)).Distinct().Reverse();
        [|j|].ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer), j As IOrderedEnumerable(Of Integer), k As IEnumerable(Of Integer))
            Dim z = i.Concat(k.Concat([|j|])).Select(Function(p) p).Where(Function(p) p <> 100).Distinct().Reverse()
            [|j|].ElementAt(10)
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestImplictExplictedFromArrayToIEnumerable()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i)
    {
        IEnumerable<int> j = Enumerable.Range(1, 10).ToArray();
        var z = i.Zip(j, (a, b) => (a, b));
        j.ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer))
            Dim j As IEnumerable(Of Integer) = Enumerable.Range(1, 10).ToArray()
            Dim z = i.Zip(j, Function(a, b) (a, b))
            j.ElementAt(10)
            z.ToArray()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestCallExtensionMethodAsOrdinaryMethod()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i)
    {
        IEnumerable<int> j = Enumerable.Range(1, 10);
        var z = Enumerable.Concat(i, [|j|]);
        Enumerable.ElementAt([|j|], 10);
        Enumerable.ToArray(z);
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer))
            Dim j As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            Dim z = Enumerable.Concat(i, [|j|])
            Enumerable.ElementAt([|j|], 10)
            Enumerable.ToArray(z)
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestAssignFromTranslatedQuery()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> i)
    {
        var k = from a in [|i|]
                select a + 1;

        k.ElementAt(10);
        [|i|].ElementAt(100);
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer))
            Dim k = From a In [|i|]
                    Select a + 1
            [|i|].ElementAt(10)
            k.ElementAt(100)
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestVBAggregateQuery()
        {
            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As IEnumerable(Of Integer))
            Dim z = From q in [|i|]
                    Select q + 1

            Dim j = Aggregate k in [|z|]
                    Into Average(k)

            Dim j2 = Aggregate k in [|z|]
                    Into Sum(k)
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestAsEnumerable1()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(int[] i)
    {
        var j = Enumerable.Range(1, 10);
        var z = [|j|].AsEnumerable();
        z.AsEnumerable().First();
        z.First();

        var x = i.AsEnumerable();
        i.AsEnumerable().First();
        i.First();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo(i As Integer())
            Dim j = Enumerable.Range(1, 10)
            Dim z = [|j|].AsEnumerable()
            z.AsEnumerable().ElementAt(10)
            z.ElementAt(10)
            
            Dim x = i.AsEnumerable()
            x.AsEnumerable().ElementAt(10)
            x.ElementAt(10)
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestAsEnumerable2()
        {
            var csharpCode = @"
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        var j = Enumerable.Range(1, 10);
        var z = [|j|].AsEnumerable();
        z.First();
        z.First();
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace NS
    Public Class Bar
        Public Sub Goo()
            Dim j = Enumerable.Range(1, 10)
            Dim z = [|j|].AsEnumerable()
            z.First()
            z.First()
        End Sub
    End Class
End Namespace";

            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestZip()
        {
            var csharpCode1 = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var k = [|i|].Zip(j, (a, b) => (a, b));
        k.First();
        [|i|].First();
    }
}";
            await VerifyCSharpAsync(csharpCode1);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim i = Enumerable.Range(1, 10)
            Dim k = [|i|].Zip(j, Function(a, b) (a, b))
            k.First()
            [|i|].First()
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Theory]
        [InlineData("MaxBy")]
        [InlineData("MinBy")]
        public async Task TestNet6AddedEnumeratedMethods(string methodName)
        {
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> j)
    {{
        var a = [|j|].{methodName}(p => p);
        [|j|].ElementAt(10);
    }}
}}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim a = [|j|].{methodName}(Function (p) p)
            [|j|].ElementAt(10)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Theory]
        [InlineData("ExceptBy")]
        [InlineData("IntersectBy")]
        [InlineData("UnionBy")]
        public async Task TestNet6AddedLinqChainMethods(string methodName)
        {
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> j, IEnumerable<int> i)
    {{
        var a = [|j|].{methodName}(i, p => p).Count();
        [|j|].ElementAt(10);
    }}
}}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer), i As IEnumerable(Of Integer))
            Dim a = [|j|].{methodName}(i, Function (p) p).Count()
            [|j|].ElementAt(10)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestDistinctBy()
        {
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> j)
    {{
        var a = [|j|].DistinctBy(p => p).Count();
        [|j|].ElementAt(10);
    }}
}}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim a = [|j|].DistinctBy(Function (p) p).Count()
            [|j|].ElementAt(10)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestChunk()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        var a = [|j|].Chunk(1000).Count();
        [|j|].ElementAt(10);
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim a = [|j|].Chunk(100).Count()
            [|j|].ElementAt(10)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestTryGetNonEnumeratedCount()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        if (j.TryGetNonEnumeratedCount(out var count))
        {
        }

        j.ElementAt(10);
    }
}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer))
            Dim count = 0
            If j.TryGetNonEnumeratedCount(count) then

            End If
            j.ElementAt(10)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task TestAssumeMethodEnumeratesParametersFromEditorConfig(bool? assumeMethodEnumeratesParameters)
        {
            var diagnostic = assumeMethodEnumeratesParameters == true ? "[|j|]" : "j";

            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> j, IEnumerable<int> i)
    {{
        {diagnostic}.UnionBy(i, p => p).ElementAt(10);
        Method({diagnostic});
    }}

    private void Method(IEnumerable<int> k)
    {{
        foreach (var p in k) {{ }}
    }}
}}";
            await VerifyCSharpAsync(csharpCode, assumeMethodEnumeratesParameters: assumeMethodEnumeratesParameters);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(j As IEnumerable(Of Integer), i As IEnumerable(Of Integer))
            {diagnostic}.UnionBy(i, Function(p) p).ElementAt(10)
            Method({diagnostic})
        End Sub

        Private Sub Method(j As IEnumerable(Of Integer))
            For Each kk in j
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode, assumeMethodEnumeratesParameters: assumeMethodEnumeratesParameters);
        }

        [Theory]
        [InlineData("M:Bar.Method1*")]
        [InlineData("")]
        public async Task TestEnumerationMethodFromEditorConfig(string editorConfig)
        {
            var diagnositcReference = string.IsNullOrEmpty(editorConfig) ? "j" : "[|j|]";
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> j, IEnumerable<int> i)
    {{
        Method1({diagnositcReference});
        {diagnositcReference}.UnionBy(i, p => p).ElementAt(10);
    }}

    private void Method1(IEnumerable<int> k)
    {{
    }}
}}";
            await VerifyCSharpAsync(csharpCode, customizedEnumerationMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Public Class Bar
    Public Sub Goo(j As IEnumerable(Of Integer), i As IEnumerable(Of Integer))
        Method1({diagnositcReference})
        {diagnositcReference}.UnionBy(i, Function(p) p).ElementAt(10)
    End Sub

    Private Sub Method1(j As IEnumerable(Of Integer))
    End Sub
End Class
";
            await VerifyVisualBasicAsync(vbCode, customizedEnumerationMethods: editorConfig);
        }

        [Theory]
        [InlineData("M:Bar.Chain*")]
        [InlineData("")]
        public async Task TestLinqChainMethodFromEditorConfig(string editorConfig)
        {
            var diagnositcReference = string.IsNullOrEmpty(editorConfig) ? "j" : "[|j|]";
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> j, IEnumerable<int> i)
    {{
        {diagnositcReference}.UnionBy(i, p => p).ElementAt(10);
        Chain({diagnositcReference}).ElementAt(100);
    }}

    private IEnumerable<int> Chain(IEnumerable<int> k)
    {{
        return k;
    }}
}}";
            await VerifyCSharpAsync(csharpCode, customizedLinqChainMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Public Class Bar
    Public Sub Goo(j As IEnumerable(Of Integer), i As IEnumerable(Of Integer))
        {diagnositcReference}.UnionBy(i, Function(p) p).ElementAt(10)
        Chain({diagnositcReference}).ElementAt(10)
    End Sub

    Private Function Chain(j As IEnumerable(Of Integer)) As IEnumerable(Of Integer)
        Return j
    End Function
End Class
";
            await VerifyVisualBasicAsync(vbCode, customizedLinqChainMethods: editorConfig);
        }

        [Fact]
        public async Task TestGenericConstraints1()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub<T>(T t) where T : IEnumerable<int>
    {
        var x = [|t|].Select(p => p).ElementAt(10000);
        var y = [|t|].Where(p => p != 100).ElementAt(10000);
    }

}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(Of T As R, R As V, V As IEnumerable(Of Integer))(q As T)
            Dim x = [|q|].Select(Function(p) p).ElementAt(100)
            Dim y = [|q|].Where(Function(p) p <> 100).ElementAt(100)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestGenericConstraints2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub<T, R, V>(T t) where T : R where R : V where V: IEnumerable<int>
    {
        var x = [|t|].Select(p => p).ElementAt(10000);
        var y = [|t|].Where(p => p != 100).ElementAt(10000);
    }

}";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(Of T As R, R As V, V As IEnumerable(Of Integer))(q As T)
            Dim x = [|q|].Select(Function(p) p).ElementAt(100)
            Dim y = [|q|].Where(Function(p) p <> 10).ElementAt(100)
        End Sub
    End Class
End Namespace
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Theory]
        [InlineData("M:Bar.LinqChain1*|Ex.LinqChain2*")]
        [InlineData("")]
        public async Task TestGenericConstraints3(string editorConfig)
        {
            var diagnosticReference = string.IsNullOrEmpty(editorConfig) ? "t" : "[|t|]";
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub()
    {{
        var t = Enumerable.Range(1, 100).OrderBy(i => i);
        var x = LinqChain1<IEnumerable<int>, IOrderedEnumerable<int>>({diagnosticReference}).ElementAt(10000);
        var y = {diagnosticReference}.LinqChain2<IEnumerable<int>, IOrderedEnumerable<int>>().ElementAt(10000);
    }}

    public T LinqChain1<T, U>(U u) where U : T where T : IEnumerable<int>
    {{
        return u;
    }}
}}

public static class Ex
{{
    public static T LinqChain2<T, U>(this U u) where U : T where T : IEnumerable<int>
    {{
        return u;
    }}
}}
";
            await VerifyCSharpAsync(csharpCode, customizedLinqChainMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Public Class Bar
    Public Sub Goo()
        Dim t = Enumerable.Range(1, 100).OrderBy(Function(i) i)
        Dim x = LinqChain1(Of IEnumerable(Of Integer), IOrderedEnumerable(Of Integer))({diagnosticReference}).ElementAt(100)
        Dim y = {diagnosticReference}.LinqChain2().ElementAt(100)
    End Sub

    Public Shared Function LinqChain1(Of T As IEnumerable(Of Integer), U As T)(q As U) As T
        Return q
    End Function
End Class

Module Ex

    <Extension()>
    Public Function LinqChain2(Of T As IEnumerable(Of Integer))(q As T) As T
        Return q
    End Function
End Module
";
            await VerifyVisualBasicAsync(vbCode, customizedLinqChainMethods: editorConfig);
        }

        [Theory]
        [InlineData("M:Bar.LinqChain1*|Ex.LinqChain2*")]
        [InlineData("")]
        public async Task TestGenericConstraints4(string editorConfig)
        {
            var diagnosticReference = string.IsNullOrEmpty(editorConfig) ? "t" : "[|t|]";
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub()
    {{
        var t = Enumerable.Range(1, 100).OrderBy(i => i);
        var x = LinqChain1({diagnosticReference}).ElementAt(10000);
        var y = {diagnosticReference}.LinqChain2().ElementAt(10000);
    }}

    public T LinqChain1<T>(T u)
    {{
        return u;
    }}
}}

public static class Ex
{{
    public static T LinqChain2<T>(this T u)
    {{
        return u;
    }}
}}
";
            await VerifyCSharpAsync(csharpCode, customizedLinqChainMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Public Class Bar
    Public Sub Goo()
        Dim t = Enumerable.Range(1, 100).OrderBy(Function(i) i)
        Dim x = LinqChain1({diagnosticReference}).ElementAt(100)
        Dim y = {diagnosticReference}.LinqChain2().ElementAt(100)
    End Sub

    Public Function LinqChain1(Of T)(q As T) As T
        Return q
    End Function
End Class

Module Ex

    <Extension()>
    Public Function LinqChain2(Of T)(q As T) As T
        Return q
    End Function
End Module
";
            await VerifyVisualBasicAsync(vbCode, customizedLinqChainMethods: editorConfig);
        }

        [Theory]
        [InlineData("M:Bar.LinqChain1*|Ex.LinqChain2*")]
        [InlineData("")]
        public async Task TestGenericConstraints5(string editorConfig)
        {
            var diagnosticReference1 = string.IsNullOrEmpty(editorConfig) ? "t" : "[|t|]";
            var diagnosticReference2 = string.IsNullOrEmpty(editorConfig) ? "a" : "[|a|]";
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> a)
    {{
        var t = Enumerable.Range(1, 100).OrderBy(i => i);
        var x = LinqChain1({diagnosticReference1}, {diagnosticReference2}).ElementAt(10000);
        var y = {diagnosticReference1}.LinqChain2({diagnosticReference2}).ElementAt(10000);
    }}

    public T LinqChain1<T>(T u, T v)
    {{
        return u;
    }}
}}

public static class Ex
{{
    public static T LinqChain2<T>(this T u, T v)
    {{
        return u;
    }}
}}
";
            await VerifyCSharpAsync(csharpCode, customizedLinqChainMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Public Class Bar
    Public Sub Goo(a As IEnumerable(Of Integer))
        Dim t = Enumerable.Range(1, 100).OrderBy(Function(i) i)
        Dim x = LinqChain1({diagnosticReference1}, {diagnosticReference2}).ElementAt(100)
        Dim y = {diagnosticReference1}.LinqChain2({diagnosticReference2}).ElementAt(100)
    End Sub

    Public Function LinqChain1(Of T)(q As T, p As T) As T
        Return q
    End Function
End Class

Module Ex

    <Extension()>
    Public Function LinqChain2(Of T)(q As T, p As T) As T
        Return q
    End Function
End Module
";
            await VerifyVisualBasicAsync(vbCode, customizedLinqChainMethods: editorConfig);
        }

        [Theory]
        [InlineData("M:Bar.TestMethod*")]
        [InlineData("")]
        public async Task TestGenericConstraints6(string editorConfig)
        {
            var diagnosticReference1 = string.IsNullOrEmpty(editorConfig) ? "i" : "[|i|]";
            var diagnosticReference2 = string.IsNullOrEmpty(editorConfig) ? "h" : "[|h|]";
            var csharpCode = $@"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{{
    public void Sub(IEnumerable<int> h)
    {{
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod({diagnosticReference1});
        {diagnosticReference1}.First();

        TestMethod({diagnosticReference2});
        {diagnosticReference2}.First();
    }}

    public void TestMethod<T>(T o) where T : IEnumerable<int>
    {{
        var x = o;
    }}
}}";

            await VerifyCSharpAsync(csharpCode, customizedEnumerationMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Public Class Bar
    Public Sub Goo(h As IEnumerable(Of Integer))
        Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
        TestMethod({diagnosticReference1})
        {diagnosticReference1}.First()
        
        TestMethod({diagnosticReference2})
        {diagnosticReference2}.First()
    End Sub


    Public Sub TestMethod(Of T As IEnumerable(Of Integer))(o As T)
    End Sub
End Class
";
            await VerifyVisualBasicAsync(vbCode, customizedEnumerationMethods: editorConfig);
        }

        [Fact]
        public async Task TestGenericConstraints7()
        {
            var csharpCode = @"
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        i.TestMethod(h);
        i.Concat(h).ElementAt(100);
    }
}

public static class Ex
{
    public static void TestMethod<T, U>(this T u, U v) where U : IEnumerable<int>
    {
    }
}
";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            i.TestMethod(h)
            i.Concat(h).ElementAt(100)
        End Sub

    End Class
End Namespace

Module Ex

    <Extension()>
    Public Sub TestMethod(Of T, U As IEnumerable(Of Integer))(o As T, o2 As U)
    End Sub
End Module
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestGenericConstraints8()
        {
            var csharpCode = @"
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        i.TestMethod(h);
        i.Concat(h).ElementAt(100);
    }
}

public static class Ex
{
    public static void TestMethod<T, U>(this T u, U v) where T : IEnumerable<int>
    {
    }
}
";

            await VerifyCSharpAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Namespace Ns
    Public Class Hoo
        Public Sub Goo(h As IEnumerable(Of Integer))
            Dim i As IEnumerable(Of Integer) = Enumerable.Range(1, 10)
            i.TestMethod(h)
            i.Concat(h).ElementAt(100)
        End Sub

    End Class
End Namespace

Module Ex

    <Extension()>
    Public Sub TestMethod(Of U, T As IEnumerable(Of Integer))(o As T, o2 As U)
    End Sub
End Module
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Theory]
        [InlineData("HashSet")]
        [InlineData("LinkedList")]
        [InlineData("List")]
        [InlineData("Queue")]
        [InlineData("SortedSet")]
        [InlineData("Stack")]
        public async Task TestObjectCreationOperation1(string typeName)
        {
            var csharpCode = $@"
using System.Linq;
using System.Collections.Generic;

public class Bar
{{
    public void Sub<T>(IEnumerable<int> collection1, IEnumerable<T> collection2)
    {{
        var s = new {typeName}<int>([|collection1|]);
        var result = [|collection1|].Count();
    }}
}}
";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Module Program
    Sub Bar(Of T)(collection1 As IEnumerable(Of T))
        Dim x = New {typeName}(Of T)([|collection1|])
        Dim z = [|collection1|].Count()
    End Sub
End Module
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestObjectCreationOperation2()
        {
            var csharpCode = @"
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub<T>(IEnumerable<KeyValuePair<int, int>> collection1)
    {
        var s = new Dictionary<int, int>([|collection1|]);
        var result = [|collection1|].Count();
    }
}
";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Module Program
    Sub Bar(Of T)(collection1 As IEnumerable(Of KeyValuePair(Of Integer, Integer)))
        Dim x = New Dictionary(Of Integer, Integer)([|collection1|])
        Dim z = [|collection1|].Count()
    End Sub
End Module
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Fact]
        public async Task TestObjectCreationOperation3()
        {
            var csharpCode = @"
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub<T>(IEnumerable<(int, int)> collection1)
    {
        var s = new PriorityQueue<int, int>([|collection1|]);
        var result = [|collection1|].Count();
    }
}
";
            await VerifyCSharpAsync(csharpCode);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq

Module Program
    Sub Bar(Of T)(collection1 As IEnumerable(Of (A As Integer, B As Integer)))
        Dim x = New PriorityQueue(Of Integer, Integer)([|collection1|])
        Dim z = [|collection1|].Count()
    End Sub
End Module
";
            await VerifyVisualBasicAsync(vbCode);
        }

        [Theory]
        [InlineData("M:Bar.LinqChain1*|Ex.LinqChain2*")]
        [InlineData("")]
        public async Task TestLinqChainEnumeratedTheArgument(string editorConfig)
        {
            var diagnosticReference1 = string.IsNullOrEmpty(editorConfig) ? "t" : "[|t|]";
            var diagnosticReference2 = string.IsNullOrEmpty(editorConfig) ? "a" : "[|a|]";
            var csharpCode = $@"
using System.Collections.Generic;
using System.Linq;

public class Bar
{{
    public void Sub(IEnumerable<int> a)
    {{
        var t = Enumerable.Range(1, 100).OrderBy(i => i);
        var x = LinqChain1({diagnosticReference1}, {diagnosticReference2});
        var y = {diagnosticReference1}.LinqChain2({diagnosticReference2});
    }}

    public T LinqChain1<T>(T u, T v)
    {{
        return u;
    }}
}}

public static class Ex
{{
    public static T LinqChain2<T>(this T u, T v)
    {{
        return u;
    }}
}}
";
            await VerifyCSharpAsync(csharpCode,
                customizedEnumerationMethods: editorConfig,
                customizedLinqChainMethods: editorConfig);

            var vbCode = $@"
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices

Public Class Bar
    Public Sub Goo(a As IEnumerable(Of Integer))
        Dim t = Enumerable.Range(1, 100).OrderBy(Function(i) i)
        Dim x = LinqChain1({diagnosticReference1}, {diagnosticReference2})
        Dim y = {diagnosticReference1}.LinqChain2({diagnosticReference2})
    End Sub

    Public Function LinqChain1(Of T)(q As T, p As T) As T
        Return q
    End Function
End Class

Module Ex

    <Extension()>
    Public Function LinqChain2(Of T)(q As T, p As T) As T
        Return q
    End Function
End Module
";
            await VerifyVisualBasicAsync(vbCode,
                customizedEnumerationMethods: editorConfig,
                customizedLinqChainMethods: editorConfig);
        }

        [Fact]
        public async Task TestDomainMerge()
        {
            var csharpCode1 = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> collection1, IEnumerable<int> collection2, IEnumerable<int> collection3)
    {
        var d = collection2.Count();
        foreach (var i in collection1)
        {
            [|collection3|].Count();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(csharpCode1);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(collection1 As IEnumerable(Of Integer), collection2 As IEnumerable(Of Integer), collection3 As IEnumerable(Of Integer))
            Dim d = collection2.Count()
            For Each i In collection1
                [|collection3|].Count()
            Next
        End Sub
    End Class
End Namespace
";
            await VerifyVB.VerifyAnalyzerAsync(vbCode);
        }

        [Fact]
        public async Task TestValueReset1()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        for (int i = 0; i < 100; i++)
        {
            var x = Create();
            var d = x.ElementAt(10);
        }
    }

    private IEnumerable<int> Create() => Enumerable.Range(1, 10);
}";
            await VerifyCS.VerifyAnalyzerAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            For i As Integer = 1 To 100
                Dim x = Create()
                Dim d = x.ElementAt(10)
            Next
        End Sub

        Private Function Create() As IEnumerable(Of Integer)
            Return Enumerable.Range(1, 10)
        End Function
    End Class
End Namespace
";
            await VerifyVB.VerifyAnalyzerAsync(vbCode);
        }

        [Fact]
        public async Task TestValueReset2()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub()
    {
        var x = Create();
        for (int i = 0; i < 100; i++)
        {
            var a = Create();
            var y = x;
            var d = [|y|].ElementAt(10);
        }
    }

    private IEnumerable<int> Create() => Enumerable.Range(1, 10);
}";
            await VerifyCS.VerifyAnalyzerAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo()
            Dim x = Create()
            For i As Integer = 1 To 100
                Dim a = Create()
                Dim y = x
                Dim d = [|y|].ElementAt(10)
            Next
        End Sub

        Private Function Create() As IEnumerable(Of Integer)
            Return Enumerable.Range(1, 10)
        End Function
    End Class
End Namespace
";
            await VerifyVB.VerifyAnalyzerAsync(vbCode);
        }

        [Fact]
        public async Task TestValueReset3()
        {
            var csharpCode = @"
using System.Collections.Generic;
using System.Linq;

public class Bar
{
    public void Sub(IEnumerable<int> n)
    {
        for (int i = 0; i < 100; i++)
        {
            var x = Create().Concat([|n|]);
            var d = x.ElementAt(10);
        }
    }

    private IEnumerable<int> Create() => Enumerable.Range(1, 10);
}";
            await VerifyCS.VerifyAnalyzerAsync(csharpCode);

            var vbCode = @"
Imports System.Collections.Generic
Imports System.Linq

Namespace Ns
    Public Class Hoo
        Public Sub Goo(n As IEnumerable(Of Integer))
            For i As Integer = 1 To 100
                Dim x = Create().Concat([|n|])
                Dim d = x.ElementAt(10)
            Next
        End Sub

        Private Function Create() As IEnumerable(Of Integer)
            Return Enumerable.Range(1, 10)
        End Function
    End Class
End Namespace
";
            await VerifyVB.VerifyAnalyzerAsync(vbCode);
        }
    }
}
