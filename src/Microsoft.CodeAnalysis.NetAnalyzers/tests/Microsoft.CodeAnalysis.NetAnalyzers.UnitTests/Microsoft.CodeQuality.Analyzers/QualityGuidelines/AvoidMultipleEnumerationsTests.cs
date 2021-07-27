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
        public Task TestFirst()
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
        var c = {|#1:i|}.First();
        var d = {|#2:i|}.First();
    }
}";
            return VerifyCS.VerifyAnalyzerAsync(
                code,
                VerifyCS.Diagnostic(AvoidMultipleEnumerations.MultipleEnumerableDescriptor).WithLocation(1),
                VerifyCS.Diagnostic(AvoidMultipleEnumerations.MultipleEnumerableDescriptor).WithLocation(2));
        }

        [Fact]
        public Task TestFirstInForLoop()
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
            {|#1:i|}.First();
        }
    }
}";
            return VerifyCS.VerifyAnalyzerAsync(
                code,
                VerifyCS.Diagnostic(AvoidMultipleEnumerations.MultipleEnumerableDescriptor).WithLocation(1));
        }

        [Fact]
        public Task TestFirstInForEachLoop()
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
            {|#1:i|}.First();
        }
    }
}";
            return VerifyCS.VerifyAnalyzerAsync(
                code,
                VerifyCS.Diagnostic(AvoidMultipleEnumerations.MultipleEnumerableDescriptor).WithLocation(1));
        }

        [Fact]
        public Task TestFirstInWhileLoop()
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
            {|#1:i|}.First();
        }
    }
}";
            return VerifyCS.VerifyAnalyzerAsync(
                code,
                VerifyCS.Diagnostic(AvoidMultipleEnumerations.MultipleEnumerableDescriptor).WithLocation(1));
        }

        [Fact]
        public Task TestFirstAfterUnreachableCode()
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
            return VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}
