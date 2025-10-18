// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferReadOnlySpanOverSpanAnalyzer,
    Microsoft.NetCore.Analyzers.Performance.PreferReadOnlySpanOverSpanFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class PreferReadOnlySpanOverSpanTests
    {
        [Fact]
        public async Task SpanParameter_NotWritten_ProducesDiagnostic()
        {
            string source = """
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        var length = data.Length;
                    }
                }
                """;
            string expected = """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        var length = data.Length;
                    }
                }
                """;
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task MemoryParameter_NotWritten_ProducesDiagnostic()
        {
            string source = """
                using System;

                class C
                {
                    private void M(Memory<int> [|data|])
                    {
                        var span = data.Span;
                        var length = span.Length;
                    }
                }
                """;
            string expected = """
                using System;

                class C
                {
                    private void M(ReadOnlyMemory<int> data)
                    {
                        var span = data.Span;
                        var length = span.Length;
                    }
                }
                """;
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_Written_NoDiagnostic()
        {
            string source = """
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        data[0] = 1;
                    }
                }
                """;
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_WrittenViaIndexer_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<int> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = i;
        }
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_PassedAsRefParameter_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data)
    {
        Helper(ref data);
    }

    private void Helper(ref Span<byte> data)
    {
        data[0] = 1;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task PublicMethod_DefaultConfig_NoDiagnostic()
        {
            string source = @"
using System;

public class C
{
    public void M(Span<byte> data)
    {
        var length = data.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task OverrideMethod_NoDiagnostic()
        {
            string source = @"
using System;

public class Base
{
    public virtual void M(Span<byte> data) { }
}

public class Derived : Base
{
    public override void M(Span<byte> data)
    {
        var length = data.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task InterfaceImplementation_NoDiagnostic()
        {
            string source = @"
using System;

public interface I
{
    void M(Span<byte> data);
}

public class C : I
{
    public void M(Span<byte> data)
    {
        var length = data.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task ReadOnlySpanParameter_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_PassedToMethodReadOnly_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        Helper(data);
    }

    private void Helper(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        Helper(data);
    }

    private void Helper(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_SlicedButNotWritten_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var slice = data.Slice(0, 10);
        var length = slice.Length;
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var slice = data.Slice(0, 10);
        var length = slice.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task MultipleParameters_MixedUsage()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|readOnlyData|], Span<byte> writableData)
    {
        var length = readOnlyData.Length;
        writableData[0] = 1;
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> readOnlyData, Span<byte> writableData)
    {
        var length = readOnlyData.Length;
        writableData[0] = 1;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_PassedToWritableSpanMethod_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data)
    {
        Helper(data);
    }

    private void Helper(Span<byte> data)
    {
        data[0] = 1;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_CopiedToLocal_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var copy = data;
        Console.WriteLine(copy.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var copy = data;
        Console.WriteLine(copy.Length);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_UsedInForEachLoop_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        foreach (var b in data)
        {
            Console.WriteLine(b);
        }
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            Console.WriteLine(b);
        }
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_PassedAsOutArgument_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data)
    {
        Helper(out data);
    }

    private void Helper(out Span<byte> data)
    {
        data = default;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MemoryParameter_AccessSpanProperty_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Memory<int> [|data|])
    {
        var s = data.Span;
        Console.WriteLine(s[0]);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlyMemory<int> data)
    {
        var s = data.Span;
        Console.WriteLine(s[0]);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_UsedInLinqQuery_ProducesDiagnostic()
        {
            string source = @"
using System;
using System.Linq;

class C
{
    private void M(Span<int> [|data|])
    {
        var sum = data.ToArray().Sum();
    }
}
";
            string expected = @"
using System;
using System.Linq;

class C
{
    private void M(ReadOnlySpan<int> data)
    {
        var sum = data.ToArray().Sum();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_ReadThroughIndexer_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var first = data[0];
        var last = data[data.Length - 1];
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var first = data[0];
        var last = data[data.Length - 1];
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_InTernaryExpression_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|], bool condition)
    {
        var length = condition ? data.Length : 0;
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data, bool condition)
    {
        var length = condition ? data.Length : 0;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_PassedToGenericMethod_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        Helper<byte>(data);
    }

    private void Helper<T>(ReadOnlySpan<T> data)
    {
        Console.WriteLine(data.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        Helper<byte>(data);
    }

    private void Helper<T>(ReadOnlySpan<T> data)
    {
        Console.WriteLine(data.Length);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_UsedInReturnStatement_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private ReadOnlySpan<byte> M(Span<byte> [|data|])
    {
        return data;
    }
}
";
            string expected = @"
using System;

class C
{
    private ReadOnlySpan<byte> M(ReadOnlySpan<byte> data)
    {
        return data;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task MemoryParameter_SliceAndRead_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Memory<int> [|data|])
    {
        var slice = data.Slice(1, 5);
        Console.WriteLine(slice.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlyMemory<int> data)
    {
        var slice = data.Slice(1, 5);
        Console.WriteLine(slice.Length);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_ConditionalAccess_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var result = data.IsEmpty ? 0 : data[0];
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var result = data.IsEmpty ? 0 : data[0];
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_MultipleReferences_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        Console.WriteLine(data.Length);
        Console.WriteLine(data[0]);
        var slice = data.Slice(1);
        Console.WriteLine(slice.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        Console.WriteLine(data.Length);
        Console.WriteLine(data[0]);
        var slice = data.Slice(1);
        Console.WriteLine(slice.Length);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_CopyTo_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        Span<byte> destination = stackalloc byte[10];
        data.CopyTo(destination);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        Span<byte> destination = stackalloc byte[10];
        data.CopyTo(destination);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_TryCopyTo_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        Span<byte> destination = stackalloc byte[10];
        data.TryCopyTo(destination);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        Span<byte> destination = stackalloc byte[10];
        data.TryCopyTo(destination);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_IndexOperator_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data)
    {
        data[^1] = 5; // Write using Index operator
    }
}
";
            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_IndexOperatorRead_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var last = data[^1]; // Read using Index operator
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var last = data[^1]; // Read using Index operator
    }
}
";
            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = expected,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_RangeOperator_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var slice = data[1..5]; // Read using Range operator
        Console.WriteLine(slice.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var slice = data[1..5]; // Read using Range operator
        Console.WriteLine(slice.Length);
    }
}
";
            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = expected,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_RangeFromEndOperator_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
    {
        var slice = data[^3..^1]; // Read using Range with Index
        Console.WriteLine(slice.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var slice = data[^3..^1]; // Read using Range with Index
        Console.WriteLine(slice.Length);
    }
}
";
            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = expected,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp9
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_ReturnedFromMethod_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private Span<byte> M(Span<byte> data)
    {
        return data; // Returning non-readonly type
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_ReturnedAsReadOnlySpan_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private ReadOnlySpan<byte> M(Span<byte> [|data|])
    {
        return data; // Returning as readonly
    }
}
";
            string expected = @"
using System;

class C
{
    private ReadOnlySpan<byte> M(ReadOnlySpan<byte> data)
    {
        return data; // Returning as readonly
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_StoredInRefParameter_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data, ref Span<byte> output)
    {
        output = data;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_StoredInOutParameter_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data, out Span<byte> output)
    {
        output = data;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MemoryParameter_StoredInField_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private Memory<byte> _field;

    private void M(Memory<byte> data)
    {
        _field = data;
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MemoryParameter_StoredInReadOnlyMemoryField_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private ReadOnlyMemory<byte> _field;

    private void M(Memory<byte> [|data|])
    {
        _field = data;
    }
}
";
            string expected = @"
using System;

class C
{
    private ReadOnlyMemory<byte> _field;

    private void M(ReadOnlyMemory<byte> data)
    {
        _field = data;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task MemoryParameter_StoredInArray_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Memory<int> data)
    {
        var array = new Memory<int>[] { data };
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MemoryParameter_StoredInReadOnlyMemoryArray_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Memory<int> [|data|])
    {
        var array = new ReadOnlyMemory<int>[] { data };
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlyMemory<int> data)
    {
        var array = new ReadOnlyMemory<int>[] { data };
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_StoredInProperty_NoDiagnostic()
        {
            string source = @"
using System;

ref struct Container
{
    public Span<byte> Data { get; set; }
}

class C
{
    private void M(Span<byte> data)
    {
        var container = new Container { Data = data };
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task SpanParameter_StoredInReadOnlySpanProperty_ProducesDiagnostic()
        {
            string source = @"
using System;

ref struct Container
{
    public ReadOnlySpan<byte> Data { get; set; }
}

class C
{
    private void M(Span<byte> [|data|])
    {
        var container = new Container { Data = data };
    }
}
";
            string expected = @"
using System;

ref struct Container
{
    public ReadOnlySpan<byte> Data { get; set; }
}

class C
{
    private void M(ReadOnlySpan<byte> data)
    {
        var container = new Container { Data = data };
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_MultipleReferencesOneWrite_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data)
    {
        Console.WriteLine(data.Length);
        Console.WriteLine(data[0]);
        data[1] = 5; // Write
        Console.WriteLine(data[2]);
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [Fact]
        public async Task MemoryParameter_PassedToMethodExpectingReadOnly_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Memory<int> [|data|])
    {
        Helper(data);
    }

    private void Helper(ReadOnlyMemory<int> data)
    {
        Console.WriteLine(data.Length);
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlyMemory<int> data)
    {
        Helper(data);
    }

    private void Helper(ReadOnlyMemory<int> data)
    {
        Console.WriteLine(data.Length);
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        private static async Task VerifyCSCodeFixAsync(string source, string fixedSource)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_PassedAsRefArgument_NoDiagnostic()
        {
            var source = """
                using System;

                class Test
                {
                    private static void IntroSort(Span<int> keys, int depthLimit)
                    {
                        if (keys.Length == 2)
                        {
                            SwapIfGreater(ref keys[0], ref keys[1]);
                            return;
                        }
                    }

                    private static void SwapIfGreater(ref int a, ref int b)
                    {
                        if (a > b)
                        {
                            int temp = a;
                            a = b;
                            b = temp;
                        }
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_RefVariableDeclaration_NoDiagnostic()
        {
            var source = """
                using System;

                class Test
                {
                    private void Method(Span<int> data)
                    {
                        // Taking a ref to an indexed element requires writability
                        ref int firstElement = ref data[0];
                        firstElement = 42;
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_RefReturn_NoDiagnostic()
        {
            var source = """
                using System;

                class Test
                {
                    // Method returns ref, so parameter must be writable
                    private ref int GetFirst(Span<int> data)
                    {
                        return ref data[0];
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task MemoryParameter_PassedToMethodViaSlice_NoDiagnostic()
        {
            var source = """
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class Test
                {
                    // buffer is passed via Slice to a method that expects writable Memory<char>
                    internal async ValueTask<int> ReadBlockAsyncInternal(Memory<char> buffer, CancellationToken cancellationToken)
                    {
                        int n = 0, i;
                        do
                        {
                            i = await ReadAsyncInternal(buffer.Slice(n), cancellationToken).ConfigureAwait(false);
                            n += i;
                        } while (i > 0 && n < buffer.Length);

                        return n;
                    }

                    private ValueTask<int> ReadAsyncInternal(Memory<char> buffer, CancellationToken cancellationToken)
                    {
                        // Implementation that writes to buffer
                        return ValueTask.FromResult(buffer.Length);
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_UsedInFixed_NoDiagnostic()
        {
            var source = """
                using System;

                class Test
                {
                    private unsafe void ProcessData(Span<byte> data)
                    {
                        fixed (byte* ptr = data)
                        {
                            // Use pointer
                            *ptr = 42;
                        }
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestState = { AllowUnsafeBlocks = true }
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_IndexerWithDecrementOperator_NoDiagnostic()
        {
            var source = """
                using System;

                class Test
                {
                    private void DecrementLast(Span<int> buffer)
                    {
                        int length = buffer.Length;
                        buffer[length - 1]--;
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_SliceAssignedToLocalAndWritten_NoDiagnostic()
        {
            var source = """
                using System;

                class Test
                {
                    internal static ReadOnlySpan<IntPtr> CopyRuntimeTypeHandles(int[] inHandles, Span<IntPtr> stackScratch)
                    {
                        if (inHandles == null || inHandles.Length == 0)
                        {
                            return default;
                        }

                        Span<IntPtr> outHandles = inHandles.Length <= stackScratch.Length ?
                            stackScratch.Slice(0, inHandles.Length) :
                            new IntPtr[inHandles.Length];
                        for (int i = 0; i < inHandles.Length; i++)
                        {
                            outHandles[i] = (IntPtr)inHandles[i];
                        }
                        return outHandles;
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_ChainedSliceWithIncrementOperator_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class Test
                {
                    private void Method(Span<int> span)
                    {
                        span.Slice(1, 4).Slice(1, 2)[0]++;
                    }
                }
                """);
        }
    }
}
