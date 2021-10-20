// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines.CSharpAvoidMultipleEnumerationsAnalyzer,
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
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var c = [|i|].All(x => x == 100);
        var d = [|i|].Any();
        var e = [|j|].Average();
        var f = [|j|].Count();
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
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        for (int k = 0; k < 100; k++)
        {
            [|i|].Aggregate((m, n) => m + n);
            [|j|].Contains(100);
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestForEachLoop1()
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
        foreach (var c in Enumerable.Range(1, 10))
        {
            [|i|].Count();
            [|j|].DefaultIfEmpty();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestForEachLoop2()
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
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            [|i|].First();
            [|j|].FirstOrDefault();
        }
        else if (b == 3)
        {
            [|i|].Last();
            [|j|].LastOrDefault();
        }
        else
        {
            [|i|].LongCount();
            [|j|].Max();
        }

        [|i|].Min();
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
    public void Sub(int b)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            [|i|].ToArray();
        }
        else if (b == 3)
        {
            [|i|].ToList();
        }
        else
        {
            [|i|].Min();
        }

        [|i|].Sum();
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
        IEnumerable<int> i = Enumerable.Range(1, 10);
        [|i|].First();
        [|j|].First();

        if (b == 1)
        {
            [|i|].First();
            [|j|].First();
        }
        else if (b == 3)
        {
            [|i|].First();
            [|j|].First();
        }
        else if (b == 5)
        {
            [|i|].First();
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
        if (flag)
        {
            [|i|].Single();
            [|j|].SingleOrDefault();
        }
        else
        {
            [|i|].Sum();
            [|j|].ToArray();
        }

        [|i|].ToDictionary(x => x);
        [|j|].First();
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
        if (flag)
        {
            [|i|].Single();
            [|j|].SingleOrDefault();
        }

        [|i|].ToDictionary(x => x);
        [|i|].Max();

        [|j|].First();
        [|j|].Min();
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
        if (b == 0)
        {
            [|i|].Single();
        }
        else if (b == 1)
        {
            [|j|].SingleOrDefault();
        }

        [|i|].ToDictionary(x => x);
        [|i|].Max();

        [|j|].Min();
        [|j|].First();
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
            i.Single();
        }
        else if (b == 1)
        {
            j.SingleOrDefault();
        }

        i.ToDictionary(x => x);
        j.First();
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
        if ([|i|].Any() || [|i|].Max() == 10)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch8()
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
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationOnBranch9()
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
        }

        [|i|].Select(k => k + 1).Skip(100).ToImmutableDictionary(x => x);

        foreach (var c2 in [|j|].Select(m => m + 1).Where(m => m != 100))
        {
        }

        [|j|].Select(k => k + 1).Skip(100).ToImmutableHashSet();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop4()
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
        public async Task TestTakesTwoIEnumerables()
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
    }
}
