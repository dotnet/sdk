// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseAsSpanInsteadOfRangeIndexerAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseAsSpanInsteadOfRangeIndexerFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public static partial class UseAsSpanInsteadOfRangeIndexerTests
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

        [Fact]
        public static async Task StringToStringLocalAsync()
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

        [Fact]
        public static async Task StringToStringReturnAsync()
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

        [Fact]
        public static async Task StringToStringParameterAsync()
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

        [Fact]
        public static async Task StringToSpanLocalAsync()
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

        [Fact]
        public static async Task StringToSpanReturnAsync()
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

        [Fact]
        public static async Task StringToSpanParameterAsync()
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

        [Fact]
        public static async Task StringToSpanParameterMultipleTimesAsync()
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

        [Fact]
        public static async Task StringToSpanCastLocalAsync()
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

        [Fact]
        public static async Task StringToSpanCastReturnAsync()
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

        [Fact]
        public static async Task StringToSpanCastParameterAsync()
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToArrayLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToArrayReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToArrayParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlySpanLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlySpanReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlySpanParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlySpanCastLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlySpanCastReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlySpanCastParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToSpanLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToSpanReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToSpanParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToSpanCastLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToSpanCastReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToSpanCastParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlyMemoryLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlyMemoryReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlyMemoryParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlyMemoryCastLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlyMemoryCastReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToReadOnlyMemoryCastParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToMemoryLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToMemoryReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToMemoryParameterAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToMemoryCastLocalAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToMemoryCastReturnAsync(string typeName)
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

        [Theory]
        [MemberData(nameof(ArrayElementTypes))]
        public static async Task ArrayToMemoryCastParameterAsync(string typeName)
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
            return test.RunAsync();
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
            return test.RunAsync();
        }
    }
}
