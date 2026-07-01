// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseAsSpanInsteadOfRangeIndexerAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseAsSpanInsteadOfRangeIndexerFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public partial class UseAsSpanInsteadOfRangeIndexerTests
    {
#pragma warning disable CA1819
        public static object[][] ArrayElementTypes { get; } =
            new[]
            {
                new object[] { "int" },
                new object[] { "byte" },
                new object[] { "object" },
                new object[] { "string" },
            };
#pragma warning restore CA1819

        [TestMethod]
        public async Task StringToStringLocalAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public string TestMethod(string input)
    {
        string tmp = input[3..5];
        return tmp;
    }
}");
        }

        [TestMethod]
        public async Task StringToStringReturnAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public string TestMethod(string input)
    {
        return input[3..5];
    }
}");
        }

        [TestMethod]
        public async Task StringToStringParameterAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(string input) => input.Length;

    public int TestMethod(string input)
    {
        return TestMethod2(input[3..5]);
    }
}");
        }

        [TestMethod]
        public async Task StringToSpanLocalAsync()
        {
            // This test is responsible for verifying the placeholders for string to ReadOnlySpan<char>.
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(string input)
    {
        ReadOnlySpan<char> tmp = input[3..5];
        return tmp.Length;
    }
}",
                @"
using System;

public class TestClass
{
    public int TestMethod(string input)
    {
        ReadOnlySpan<char> tmp = input.AsSpan()[3..5];
        return tmp.Length;
    }
}",
                VerifyCS.Diagnostic(UseAsSpanInsteadOfRangeIndexerAnalyzer.StringRule).
                    WithSpan(8, 34, 8, 34 + 11).
                    WithArguments("AsSpan", "System.Range", "string"));
        }

        [TestMethod]
        public async Task StringToSpanReturnAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public ReadOnlySpan<char> TestMethod(string input)
    {
        return {|CA1831:input[3..5]|};
    }
}",
                @"
using System;

public class TestClass
{
    public ReadOnlySpan<char> TestMethod(string input)
    {
        return input.AsSpan()[3..5];
    }
}");
        }

        [TestMethod]
        public async Task StringToSpanParameterAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<char> input) => input.Length;

    public int TestMethod(string input)
    {
        return TestMethod2({|CA1831:input[3..5]|});
    }
}",
                @"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<char> input) => input.Length;

    public int TestMethod(string input)
    {
        return TestMethod2(input.AsSpan()[3..5]);
    }
}");
        }

        [TestMethod]
        public async Task StringToSpanParameterMultipleTimesAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<char> input) => input.Length;

    public int TestMethod(string input)
    {
        return TestMethod2({|CA1831:input[3..5]|}) + TestMethod2({|CA1831:input[1..^2]|});
    }
}",
                @"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<char> input) => input.Length;

    public int TestMethod(string input)
    {
        return TestMethod2(input.AsSpan()[3..5]) + TestMethod2(input.AsSpan()[1..^2]);
    }
}");
        }

        [TestMethod]
        public async Task StringToSpanCastLocalAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(string input)
    {
        ReadOnlySpan<char> tmp = (ReadOnlySpan<char>)input[3..5];
        return tmp.Length;
    }
}");
        }

        [TestMethod]
        public async Task StringToSpanCastReturnAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public ReadOnlySpan<char> TestMethod(string input)
    {
        return (ReadOnlySpan<char>)input[3..5];
    }
}");
        }

        [TestMethod]
        public async Task StringToSpanCastParameterAsync()
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<char> input) => input.Length;

    public int TestMethod(string input)
    {
        return TestMethod2((ReadOnlySpan<char>)input[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToArrayLocalAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public " + typeName + @"[] TestMethod(" + typeName + @"[] input)
    {
        " + typeName + @"[] tmp = input[3..5];
        return tmp;
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToArrayReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public " + typeName + @"[] TestMethod(" + typeName + @"[] input)
    {
        return input[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToArrayParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(" + typeName + @"[] input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2(input[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlySpanLocalAsync(string typeName)
        {
            // This test is responsible for verifying the placeholders for T[] to ReadOnlySpan<T>.
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        ReadOnlySpan<" + typeName + @"> tmp = input[3..5];
        return tmp.Length;
    }
}",
                @"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        ReadOnlySpan<" + typeName + @"> tmp = input.AsSpan()[3..5];
        return tmp.Length;
    }
}",
                VerifyCS.Diagnostic(UseAsSpanInsteadOfRangeIndexerAnalyzer.ArrayReadOnlyRule).
                    WithSpan(8, 30 + typeName.Length, 8, 30 + 11 + typeName.Length).
                    WithArguments("AsSpan", "System.Range", typeName + "[]"));
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlySpanReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public ReadOnlySpan<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return {|CA1832:input[3..5]|};
    }
}",
                @"
using System;

public class TestClass
{
    public ReadOnlySpan<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return input.AsSpan()[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlySpanParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2({|CA1832:input[3..5]|});
    }
}",
                @"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlySpan<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2(input.AsSpan()[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlySpanCastLocalAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        ReadOnlySpan<" + typeName + @"> tmp = (ReadOnlySpan<" + typeName + @">)input[3..5];
        return tmp.Length;
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlySpanCastReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public ReadOnlySpan<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return (ReadOnlySpan<" + typeName + @">)input[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlySpanCastParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2<T>(ReadOnlySpan<T> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2((ReadOnlySpan<" + typeName + @">)input[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToSpanLocalAsync(string typeName)
        {
            // This test is responsible for verifying the placeholders for T[] to Span<T>.
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        Span<" + typeName + @"> tmp = input[3..5];
        return tmp.Length;
    }
}",
                @"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        Span<" + typeName + @"> tmp = input.AsSpan()[3..5];
        return tmp.Length;
    }
}",
                VerifyCS.Diagnostic(UseAsSpanInsteadOfRangeIndexerAnalyzer.ArrayReadWriteRule).
                    WithSpan(8, 22 + typeName.Length, 8, 22 + 11 + typeName.Length).
                    WithArguments("AsSpan", "System.Range", typeName + "[]"));
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToSpanReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public Span<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return {|CA1833:input[3..5]|};
    }
}",
                @"
using System;

public class TestClass
{
    public Span<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return input.AsSpan()[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToSpanParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(Span<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2({|CA1833:input[3..5]|});
    }
}",
                @"
using System;

public class TestClass
{
    private static int TestMethod2(Span<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2(input.AsSpan()[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToSpanCastLocalAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        Span<" + typeName + @"> tmp = (Span<" + typeName + @">)input[3..5];
        return tmp.Length;
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToSpanCastReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public Span<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return (Span<" + typeName + @">)input[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToSpanCastParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2<T>(Span<T> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2((Span<" + typeName + @">)input[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlyMemoryLocalAsync(string typeName)
        {
            // This test is responsible for verifying the placeholders for T[] to ReadOnlyMemory<T>.
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        ReadOnlyMemory<" + typeName + @"> tmp = input[3..5];
        return tmp.Length;
    }
}",
                @"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        ReadOnlyMemory<" + typeName + @"> tmp = input.AsMemory()[3..5];
        return tmp.Length;
    }
}",
                VerifyCS.Diagnostic(UseAsSpanInsteadOfRangeIndexerAnalyzer.ArrayReadOnlyRule).
                    WithSpan(8, 32 + typeName.Length, 8, 32 + 11 + typeName.Length).
                    WithArguments("AsMemory", "System.Range", typeName + "[]"));
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlyMemoryReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public ReadOnlyMemory<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return {|CA1832:input[3..5]|};
    }
}",
                @"
using System;

public class TestClass
{
    public ReadOnlyMemory<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return input.AsMemory()[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlyMemoryParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlyMemory<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2({|CA1832:input[3..5]|});
    }
}",
                @"
using System;

public class TestClass
{
    private static int TestMethod2(ReadOnlyMemory<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2(input.AsMemory()[3..5]);
    }
}"
                );
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlyMemoryCastLocalAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        ReadOnlyMemory<" + typeName + @"> tmp = (ReadOnlyMemory<" + typeName + @">)input[3..5];
        return tmp.Length;
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlyMemoryCastReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public ReadOnlyMemory<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return (ReadOnlyMemory<" + typeName + @">)input[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToReadOnlyMemoryCastParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2<T>(ReadOnlyMemory<T> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2((ReadOnlyMemory<" + typeName + @">)input[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToMemoryLocalAsync(string typeName)
        {
            // This test is responsible for verifying the placeholders for T[] to Memory<T>.
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        Memory<" + typeName + @"> tmp = input[3..5];
        return tmp.Length;
    }
}",
                @"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        Memory<" + typeName + @"> tmp = input.AsMemory()[3..5];
        return tmp.Length;
    }
}",
                VerifyCS.Diagnostic(UseAsSpanInsteadOfRangeIndexerAnalyzer.ArrayReadWriteRule).
                    WithSpan(8, 24 + typeName.Length, 8, 24 + 11 + typeName.Length).
                    WithArguments("AsMemory", "System.Range", typeName + "[]"));
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToMemoryReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public Memory<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return {|CA1833:input[3..5]|};
    }
}",
                @"
using System;

public class TestClass
{
    public Memory<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return input.AsMemory()[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToMemoryParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2(Memory<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2({|CA1833:input[3..5]|});
    }
}",
                @"
using System;

public class TestClass
{
    private static int TestMethod2(Memory<" + typeName + @"> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2(input.AsMemory()[3..5]);
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToMemoryCastLocalAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public int TestMethod(" + typeName + @"[] input)
    {
        Memory<" + typeName + @"> tmp = (Memory<" + typeName + @">)input[3..5];
        return tmp.Length;
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToMemoryCastReturnAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    public Memory<" + typeName + @"> TestMethod(" + typeName + @"[] input)
    {
        return (Memory<" + typeName + @">)input[3..5];
    }
}");
        }

        [TestMethod]
        [DynamicData(nameof(ArrayElementTypes))]
        public async Task ArrayToMemoryCastParameterAsync(string typeName)
        {
            await TestCSAsync(@"
using System;

public class TestClass
{
    private static int TestMethod2<T>(Memory<T> input) => input.Length;

    public int TestMethod(" + typeName + @"[] input)
    {
        return TestMethod2((Memory<" + typeName + @">)input[3..5]);
    }
}");
        }

        private static Task TestCSAsync(string source, string corrected, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp9,
                FixedCode = corrected,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync(CancellationToken.None);
        }

        private static Task TestCSAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp9,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync(CancellationToken.None);
        }
    }
}
