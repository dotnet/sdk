// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
        [Fact]
        public async Task TestMultipleInvocations()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestForLoop()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationsInForEachLoop()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        var i = Enumerable.Range(1, 10).ToArray();
        foreach (var c in Enumerable.Range(1, 10))
        {
            i.Count();
            [|j|].DefaultIfEmpty();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationsAfterForEachLoop()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationsAfterForEachLoopForArray()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestWhileLoop()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfBranchWithElse()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfbranch()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationBeforeIfbranch()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfbranchForArray()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestNoInvocationAfterIfbranch()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestOneInvocationAfterIfBranch1()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestOneInvocationAfterIfBranch2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestTwoInvocationsAfterIfElseBranch()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestExplicitDeclaration()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestTwoInvocationsAfterIfBranch()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestDifferntInvocationsInIfBranch()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestDifferntInvocationsInIfBranch2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestDifferntInvocationsInIfBranch3()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch1()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch3()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch4()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch5()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch6()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch7()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop1()
        {
            var code = @"
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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop3()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAcceptObject()
        {
            var code = @"
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

    public void TestMethod(object o)
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAcceptGenerics()
        {
            var code = @"
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

    public void TestMethod<T>(T o)
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAcceptGenericsConstraintToIEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod([|i|]);
        [|i|].First();

        TestMethod([|h|]);
        [|h|].First();
    }

    public void TestMethod<T>(T o) where T : IEnumerable<int>
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestExplicteInvocations()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

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

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestTakesTwoIEnumerables1()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        [|i|].Concat([|h|]).ToArray();
        [|h|].Concat([|i|]).ToList();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestTakesTwoIEnumerables2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        [|i|].SequenceEqual([|h|]);
        [|h|].SequenceEqual([|i|]);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestTakesTwoIEnumerables3()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        IEnumerable<int> x = Enumerable.Range(1, 10);
        [|x|].GroupJoin([|h|], i => i, i => i, (i, ints) => i).First();
        [|x|].GroupJoin([|h|], i => i, i => i, (i, ints) => i).Last();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestDelayExecutions()
        {
            // SkipLast and TakeLast are unavailable for NET472
#if NETCOREAPP2_0_OR_GREATER
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
#endif

#if NET472
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub()
    {
        IEnumerable<int> x = Enumerable.Range(1, 10);
        var c1 = [|x|].Append(1).Cast<Object>().Distinct()
            .OfType<int>().OrderBy(i => i).OrderByDescending(i => i)
            .ThenBy(i => i).ThenByDescending(i => i)
            .Prepend(1).Reverse().Select(i => i + 1).Skip(100)
            .SkipWhile(i => i == 99).Take(1).TakeWhile(i => i == 100)
            .Where(i => i != 10).ToArray();

        var c2 = [|x|].Append(1).Cast<Object>().Distinct()
            .OfType<int>().OrderBy(i => i).OrderByDescending(i => i)
            .ThenBy(i => i).ThenByDescending(i => i)
            .Prepend(1).Reverse().Select(i => i + 1).Skip(100)
            .SkipWhile(i => i == 99).Take(1).TakeWhile(i => i == 100)
            .Where(i => i != 10).First();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
#endif
        }

        [Fact]
        public async Task TestGroupBy()
        {
            var code = @"
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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestSelectMany()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<IEnumerable<int>> y)
    {
        var a = [|y|].SelectMany(x => x).ToArray();
        var b = [|y|].SelectMany(x => x.ToArray()).ToArray();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestIEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable h)
    {
        [|h|].OfType<int>().ToArray();
        [|h|].Cast<int>().ToList();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestIOrderedEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;

public class Bar
{
    public void Sub(IOrderedEnumerable<int> h)
    {
        [|h|].Where(i => i != 10).ToArray();
        [|h|].ToList();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestExecutionInTheMiddle()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        [|h|].ToArray().Select(j => j + 1).Where(x => x != 100);
        [|h|].ToArray().Select(j => j + 1).Where(x => x != 100);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestAssignmentAfterEnumeration()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);

        }

        [Fact]
        public async Task TestForEachLoopForLocalAssignment()
        {
            var code = @"
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
            await VerifyCS.VerifyAnalyzerAsync(code);

        }

        [Fact]
        public async Task TestInvocationLocalAssignment()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        [|h|].ToArray();
        var c = h;

        [|c|].First();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationLocalAssignmentWithDeferredMethodCall()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> h)
    {
        [|h|].ToArray();
        var c = [|h|].Select(i => i + 1);

        c.First();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationWithAssignment()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestForEachForIEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestForEachForIOrderedEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestVBForEachLoop()
        {
            var code = @"
Imports System
Imports System.Linq
Imports System.Collections
Imports System.Collections.Generic

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
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedParameterAfterLinqCallChain()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedLocalAfterLinqCallChain1()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedLocalAfterLinqCallChain2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedLocalAfterLinqCallChain3()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }


        [Fact]
        public async Task TestConcatOneParameterMultipleTimes()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> i, IEnumerable<int> k)
    {
        var z = i.Except([|k|]).Concat([|k|]);
        z.ToArray();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedLocalWithMultipleAbstractLocations1()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedLocalWithMultipleAbstractLocations2()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestEnumeratedLocalWithMultipleAbstractLocations3()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestDelayEnumerableFromArray()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> i, int[] j)
    {
        var z = j.Concat(i);
        j.ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestDelayIOrderedEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> i, IOrderedEnumerable<int> j, IEnumerable<int> k)
    {
        var z = i.Concat([|j|]).Concat(k);
        [|j|].ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestNestedDelayIEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> i, IOrderedEnumerable<int> j, IEnumerable<int> k)
    {
        var z = i.Concat(k.Concat([|j|]));
        [|j|].ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestImplictExplictedFromArrayToIEnumerable()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Bar
{
    public void Sub(IEnumerable<int> i)
    {
        IEnumerable<int> j = Enumerable.Range(1, 10).ToArray();
        var z = i.Concat(j);
        j.ElementAt(10);
        z.ToArray();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
