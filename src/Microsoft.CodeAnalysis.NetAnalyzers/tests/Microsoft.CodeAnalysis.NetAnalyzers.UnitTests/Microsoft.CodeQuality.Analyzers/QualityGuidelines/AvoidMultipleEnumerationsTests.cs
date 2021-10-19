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
        var c = [|i|].First();
        var d = [|i|].First();
        var e = [|j|].First();
        var f = [|j|].First();
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
            [|i|].First();
            [|j|].First();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestForEachLoop()
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
            [|i|].First();
            [|j|].First();
        }
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
            [|i|].First();
            [|j|].First();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterUnreachableCode()
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
        if (false)
        {
            i.First();
            j.First();
        }

        i.First();
        j.First();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfBranch1()
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
            [|j|].First();
        }
        else if (b == 3)
        {
            [|i|].First();
            [|j|].First();
        }
        else
        {
            [|i|].First();
            [|j|].First();
        }

        [|i|].First();
        [|j|].First();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfBranch2()
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
            i.First();
            j.First();
        }
        else if (b == 3)
        {
            i.First();
            j.First();
        }
        else if (b == 5)
        {
        }

        i.First();
        j.First();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfBranch3()
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
            i.First();
            j.First();
        }
        else if (b == 3)
        {
            i.First();
            j.First();
        }
        else if (b == 5)
        {
            i.First();
            j.First();
        }

        i.First();
        j.First();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationAfterIfBranch4()
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
            [|i|].First();
            [|j|].First();
        }
        else
        {
            [|i|].First();
            [|j|].First();
        }

        [|i|].First();
        [|j|].First();
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

        [|i|].First();

        foreach (var c2 in [|j|])
        {
        }

        [|j|].First();
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

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|].Select(m => m + 1).Where(m => m != 100))
        {
        }

        [|i|].First();

        foreach (var c2 in [|j|].Select(m => m + 1).Where(m => m != 100))
        {
        }

        [|j|].First();
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

public class Bar
{
    public void Sub(IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|].Select(m => m + 1).Where(m => m != 100))
        {
        }

        [|i|].Select(k => k + 1).Skip(100).First();

        foreach (var c2 in [|j|].Select(m => m + 1).Where(m => m != 100))
        {
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
    }
}
