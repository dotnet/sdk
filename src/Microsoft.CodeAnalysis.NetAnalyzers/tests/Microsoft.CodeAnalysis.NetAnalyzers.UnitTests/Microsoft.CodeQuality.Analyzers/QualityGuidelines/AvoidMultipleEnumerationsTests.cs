// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations,
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
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var c = [|i|].First();
        var d = [|i|].First();
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
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        for (int j = 0; j < 100; j++)
        {
            [|i|].First();
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
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in Enumerable.Range(1, 10))
        {
            [|i|].First();
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
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        while (true)
        {
            [|i|].First();
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
    public void Sub()
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (false)
        {
            i.First();
        }

        i.First();
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
    public void Sub(int b)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            [|i|].First();
        }
        else if (b == 3)
        {
            [|i|].First();
        }
        else if (b == 5)
        {
            [|i|].First();
        }

        [|i|].First();
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
    public void Sub(int b)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        if (b == 1)
        {
            i.First();
        }
        else if (b == 3)
        {
            i.First();
        }
        else if (b == 5)
        {
        }

        i.First();
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
    public void Sub(bool flag)
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
        }
        else
        {
            [|i|].First();
        }

        [|i|].First();
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
    public void Sub(bool flag)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        foreach (var c in [|i|])
        {
        }

        [|i|].First();
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
    public void Sub(bool flag)
    {
        IEnumerable<int> i = Enumerable.Range(1, 10);
        var j = i;
        var k = j;
        
        if (flag)
        {
            foreach (var x in [|k|])
            {
            }
        }
        else
        {
            var d = [|i|].First();
        }
        
        foreach (var z in [|j|])
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
