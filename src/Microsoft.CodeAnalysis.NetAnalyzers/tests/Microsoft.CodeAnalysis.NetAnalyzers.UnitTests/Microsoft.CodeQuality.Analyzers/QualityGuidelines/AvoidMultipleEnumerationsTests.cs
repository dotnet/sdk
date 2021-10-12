// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerations,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerations,
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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var c = [|i|].First();
        var d = [|i|].First();
        [|array[0]|].First();
        [|array[0]|].First();
        var c = [|j|].First();
        var d = [|j|].First();
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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        for (int j = 0; j < 100; j++)
        {
            [|i|].First();
            [|array[0]|].First();
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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in Enumerable.Range(1, 10))
        {
            [|i|].First();
            [|array[0]|].First();
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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        while (true)
        {
            [|i|].First();
            [|array[0]|].First();
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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (false)
        {
            i.First();
            j.First();
            array[0].First();
        }

        i.First();
        j.First();
        array[0].First();
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
    public void Sub(int b, IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            [|i|].First();
            [|array[0]|].First();
            [|j|].First();
        }
        else if (b == 3)
        {
            [|i|].First();
            [|array[0]|].First();
            [|j|].First();
        }
        else if (b == 5)
        {
            [|i|].First();
            [|array[0]|].First();
            [|j|].First();
        }

        [|i|].First();
        [|array[0]|].First();
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
    public void Sub(int b, IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            i.First();
            array[0].First();
            j.First();
        }
        else if (b == 3)
        {
            i.First();
            array[0].First();
            j.First();
        }
        else if (b == 5)
        {
            i.First();
            array[0].First();
            j.First();
        }

        i.First();
        array[0].First();
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
    public void Sub(bool flag, IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (flag)
        {
            [|i|].First();
        }
        else if (!flag)
        {
            [|i|].First();
        }

        [|i|].First();
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
    public void Sub(bool flag)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (flag)
        {
            [|i|].First();
            [|array[0]|].First();
            [|j|].First();
        }
        else
        {
            [|i|].First();
            [|array[0]|].First();
            [|j|].First();
        }

        [|i|].First();
        [|array[0]|].First();
        [|j|].First();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }


        [Fact]
        public async Task Test()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(bool flag, IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|].Select(z => z))
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestInvocationWithForEachLoop()
        {
            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class Bar
{
    public void Sub(bool flag, IEnumerable<int>[] array, IEnumerable<int> j)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|])
        {
        }

        [|i|].First();

        foreach (var c1 in [|array[0]|])
        {
        }

        [|array[0]|].First();

        foreach (var c2 in j)
        {
            [|j|].First();
        }

        [|j|].First();
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
using System.Collections.Generic;

public class Bar
{
    public void Sub(bool flag, IEnumerable<int>[] array, IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var j = i;
        var k = j;

        var a = array[0];
        var b = a;

        var n = h;
        var m = n;
        
        if (flag)
        {
            foreach (var x in [|k|])
            {
            }

            foreach (var x in [|b|])
            {
            }

            foreach (var x in [|m|])
            {
            }
        }
        else
        {
            var d = [|i|].First();
            var d1 = [|array[0]|].First();
            var d2 = [|h|].First();
        }
        
        foreach (var z in [|j|])
        {
        }

        foreach (var z in [|b|])
        {
        }

        foreach (var z in [|m|])
        {
        }
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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod(i);
        i.First();

        TestMethod(array[0]);
        array[0].First();

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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod(i);
        i.First();

        TestMethod(array[0]);
        array[0].First();

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
    public void Sub(IEnumerable<int>[] array, IEnumerable<int> h)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        TestMethod([|i|]);
        [|i|].First();

        TestMethod([|array[0]|]);
        [|array[0]|].First();

        TestMethod([|h|]);
        [|h|].First();
    }

    public void TestMethod<T>(T o) : where T : IEnumerable<int>
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
