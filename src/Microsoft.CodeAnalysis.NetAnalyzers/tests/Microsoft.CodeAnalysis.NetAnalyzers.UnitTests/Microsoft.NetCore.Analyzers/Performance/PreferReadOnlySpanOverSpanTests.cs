// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
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
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        var length = data.Length;
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        var length = data.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ReassignedToOwnSliceLoop_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        while (!data.IsEmpty)
                        {
                            int idx = data.IndexOf((byte)0);
                            if (idx < 0)
                                break;
                            data = data.Slice(idx + 1);
                        }
                        _ = data.Length;
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        while (!data.IsEmpty)
                        {
                            int idx = data.IndexOf((byte)0);
                            if (idx < 0)
                                break;
                            data = data.Slice(idx + 1);
                        }
                        _ = data.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ReassignedToOwnSliceLoopThenConsumedByWritableApi_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private static void Consume(Span<byte> span) { }

                    private void M(Span<byte> data)
                    {
                        while (!data.IsEmpty)
                        {
                            int idx = data.IndexOf((byte)1);
                            if (idx < 0)
                                break;
                            data = data.Slice(idx + 1);
                        }
                        // Passed to writable Span API makes parameter unsafe for conversion
                        Consume(data);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ReassignedToOwnSliceLoopThenWritten_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        while (!data.IsEmpty)
                        {
                            int idx = data.IndexOf((byte)2);
                            if (idx < 0)
                                break;
                            data = data.Slice(idx + 1);
                        }
                        // Write to the parameter after slicing keeps it writable-only
                        if (!data.IsEmpty)
                        {
                            data[0] = 42;
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedToMethodReadOnly_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
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
                """, """
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
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInForEachLoop_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
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
                """, """
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
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInForLoop_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            byte b = data[i];
                            Console.WriteLine(b);
                        }
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            byte b = data[i];
                            Console.WriteLine(b);
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInLinqQuery_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;
                using System.Linq;

                class C
                {
                    private void M(Span<int> [|data|])
                    {
                        var sum = data.ToArray().Sum();
                    }
                }
                """, """
                using System;
                using System.Linq;

                class C
                {
                    private void M(ReadOnlySpan<int> data)
                    {
                        var sum = data.ToArray().Sum();
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ReadThroughIndexer_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        var first = data[0];
                        var last = data[data.Length - 1];
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        var first = data[0];
                        var last = data[data.Length - 1];
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_InTernaryExpression_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|], bool condition)
                    {
                        var length = condition ? data.Length : 0;
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data, bool condition)
                    {
                        var length = condition ? data.Length : 0;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedToGenericMethod_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
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
                """, """
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
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInReturnStatement_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private ReadOnlySpan<byte> M(Span<byte> [|data|])
                    {
                        return data;
                    }
                }
                """, """
                using System;

                class C
                {
                    private ReadOnlySpan<byte> M(ReadOnlySpan<byte> data)
                    {
                        return data;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ConditionalAccess_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        var result = data.IsEmpty ? 0 : data[0];
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        var result = data.IsEmpty ? 0 : data[0];
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_MultipleReferences_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        Console.WriteLine(data.Length);
                        Console.WriteLine(data[0]);
                        Console.WriteLine(data[1]);
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        Console.WriteLine(data.Length);
                        Console.WriteLine(data[0]);
                        Console.WriteLine(data[1]);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_CopyTo_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        Span<byte> destination = stackalloc byte[10];
                        data.CopyTo(destination);
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        Span<byte> destination = stackalloc byte[10];
                        data.CopyTo(destination);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_TryCopyTo_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        Span<byte> destination = stackalloc byte[10];
                        data.TryCopyTo(destination);
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        Span<byte> destination = stackalloc byte[10];
                        data.TryCopyTo(destination);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ReturnedAsReadOnlySpan_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private ReadOnlySpan<byte> M(Span<byte> [|data|])
                    {
                        return data; // Returning as readonly
                    }
                }
                """, """
                using System;

                class C
                {
                    private ReadOnlySpan<byte> M(ReadOnlySpan<byte> data)
                    {
                        return data; // Returning as readonly
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_StoredInReadOnlyMemoryField_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private ReadOnlyMemory<byte> _field;

                    private void M(Memory<byte> [|data|])
                    {
                        _field = data;
                    }
                }
                """, """
                using System;

                class C
                {
                    private ReadOnlyMemory<byte> _field;

                    private void M(ReadOnlyMemory<byte> data)
                    {
                        _field = data;
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_StoredInReadOnlyMemoryArray_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Memory<int> [|data|])
                    {
                        var array = new ReadOnlyMemory<int>[] { data };
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlyMemory<int> data)
                    {
                        var array = new ReadOnlyMemory<int>[] { data };
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_StoredInReadOnlySpanProperty_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
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
                """, """
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
                """);
        }

        [Fact]
        public async Task MemoryParameter_PassedToMethodExpectingReadOnly_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
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
                """, """
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
                """);
        }

        [Fact]
        public async Task MemoryParameter_NotWritten_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Memory<int> data)
                    {
                        var span = data.Span; // .Span stores result in local - can't verify safe usage
                        var length = span.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_Written_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        data[0] = 1;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_WrittenViaIndexer_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedAsRefParameter_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task PublicMethod_DefaultConfig_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                public class C
                {
                    public void M(Span<byte> data)
                    {
                        var length = data.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task OverrideMethod_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task InterfaceImplementation_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task ReadOnlySpanParameter_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        var length = data.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_SlicedButNotWritten_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        var slice = data.Slice(0, 10);
                        var length = slice.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task MultipleParameters_MixedUsage()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|readOnlyData|], Span<byte> writableData)
                    {
                        var length = readOnlyData.Length;
                        writableData[0] = 1;
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> readOnlyData, Span<byte> writableData)
                    {
                        var length = readOnlyData.Length;
                        writableData[0] = 1;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedToWritableSpanMethod_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_CopiedToLocal_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        var copy = data;
                        Console.WriteLine(copy.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedAsOutArgument_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task MemoryParameter_AccessSpanProperty_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Memory<int> data)
                    {
                        var s = data.Span; // .Span stores result in local - can't verify safe usage
                        Console.WriteLine(s[0]);
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_SliceAndRead_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Memory<int> data)
                    {
                        var slice = data.Slice(1, 5); // Slice stored in local - can't verify safe usage
                        Console.WriteLine(slice.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_RangeOperator_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        var slice = data[1..5];
                        Console.WriteLine(slice.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_RangeFromEndOperator_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        var slice = data[^3..^1];
                        Console.WriteLine(slice.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ReturnedFromMethod_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private Span<byte> M(Span<byte> data)
                    {
                        return data;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ExpressionReturnedFromMethod_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private Span<byte> M(Span<byte> data) => data;
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_StoredInRefParameter_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                
                using System;

                class C
                {
                    private void M(Span<byte> data, ref Span<byte> output)
                    {
                        output = data;
                    }
                }
                
                """);
        }

        [Fact]
        public async Task SpanParameter_StoredInOutParameter_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data, out Span<byte> output)
                    {
                        output = data;
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_StoredInField_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private Memory<byte> _field;

                    private void M(Memory<byte> data)
                    {
                        _field = data;
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_StoredInArray_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Memory<int> data)
                    {
                        var array = new Memory<int>[] { data };
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_StoredInProperty_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_MultipleReferencesOneWrite_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedAsRefArgument_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_RefVariableDeclaration_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_RefReturn_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class Test
                {
                    // Method returns ref, so parameter must be writable
                    private ref int GetFirst(Span<int> data)
                    {
                        return ref data[0];
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_PassedToMethodViaSlice_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                        if (buffer.Length > 0)
                            buffer.Span[0] = 'x';
                        return ValueTask.FromResult(buffer.Length);
                    }
                }
                """);
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

            await new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms =
                {
                    (solution, projectId) => solution.WithProjectCompilationOptions(projectId,
                        ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true))
                }
            }.RunAsync();
        }

        [Fact]
        public async Task SpanParameter_IndexerWithDecrementOperator_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class Test
                {
                    private void DecrementLast(Span<int> buffer)
                    {
                        int length = buffer.Length;
                        buffer[length - 1]--;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_SliceAssignedToLocalAndWritten_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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
                """);
        }

        [Fact]
        public async Task SpanParameter_ChainedSliceWithIncrementOperator_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
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

        [Fact]
        public async Task SpanParameter_UsedInSwitchExpression_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private int M(Span<byte> [|data|], int selector)
                    {
                        return selector switch
                        {
                            0 => data.Length,
                            1 => data[0],
                            _ => data.IsEmpty ? 0 : data[data.Length - 1]
                        };
                    }
                }
                """, """
                using System;

                class C
                {
                    private int M(ReadOnlySpan<byte> data, int selector)
                    {
                        return selector switch
                        {
                            0 => data.Length,
                            1 => data[0],
                            _ => data.IsEmpty ? 0 : data[data.Length - 1]
                        };
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInPatternMatching_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private bool M(Span<byte> [|data|])
                    {
                        return data switch
                        {
                            { Length: > 0 } => data[0] == 42,
                            _ => false
                        };
                    }
                }
                """, """
                using System;

                class C
                {
                    private bool M(ReadOnlySpan<byte> data)
                    {
                        return data switch
                        {
                            { Length: > 0 } => data[0] == 42,
                            _ => false
                        };
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInNullCoalescingOperator_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private int M(Span<byte> data, bool condition)
                    {
                        Span<byte> local = condition ? data : default;
                        return local.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_MultipleParametersSameType_OnlyReadOnlyOnesMarked()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|source|], Span<byte> destination)
                    {
                        source.CopyTo(destination);
                        destination[0] = 42;
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> source, Span<byte> destination)
                    {
                        source.CopyTo(destination);
                        destination[0] = 42;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInConditionalExpression_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private ReadOnlySpan<byte> M(Span<byte> data1, Span<byte> data2, bool condition)
                    {
                        return condition ? data1 : data2;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInInterpolatedString_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private string M(Span<byte> [|data|])
                    {
                        return $"Length: {data.Length}, First: {(data.IsEmpty ? 0 : data[0])}";
                    }
                }
                """, """
                using System;

                class C
                {
                    private string M(ReadOnlySpan<byte> data)
                    {
                        return $"Length: {data.Length}, First: {(data.IsEmpty ? 0 : data[0])}";
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_OnlyAccessedViaLength_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private int M(Memory<byte> [|data|])
                    {
                        return data.Length + data.IsEmpty.GetHashCode();
                    }
                }
                """, """
                using System;

                class C
                {
                    private int M(ReadOnlyMemory<byte> data)
                    {
                        return data.Length + data.IsEmpty.GetHashCode();
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ComparedWithOtherSpan_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private bool M(Span<byte> [|data1|], Span<byte> [|data2|])
                    {
                        return data1.SequenceEqual(data2);
                    }
                }
                """, """
                using System;

                class C
                {
                    private bool M(ReadOnlySpan<byte> data1, ReadOnlySpan<byte> data2)
                    {
                        return data1.SequenceEqual(data2);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInLocalFunction_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        LocalFunc(data);

                        static void LocalFunc(Span<byte> d)
                        {
                            Console.WriteLine(d.Length);
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedToStaticMethodWithReadOnlyOverload_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        Helper(data);
                    }

                    private static void Helper(ReadOnlySpan<byte> data) => Console.WriteLine(data.Length);
                    private static void Helper(Span<byte> data) => throw new NotImplementedException();
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_WithDefaultParameter_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data, int offset = 0)
                    {
                        var slice = data.Slice(offset);
                        var length = slice.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_NullableReferenceTypeContext_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                #nullable enable
                using System;

                class C
                {
                    private void M(Span<string?> [|data|])
                    {
                        foreach (var s in data)
                        {
                            Console.WriteLine(s?.Length ?? 0);
                        }
                    }
                }
                """, """
                #nullable enable
                using System;

                class C
                {
                    private void M(ReadOnlySpan<string?> data)
                    {
                        foreach (var s in data)
                        {
                            Console.WriteLine(s?.Length ?? 0);
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_NestedGenericType_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    private void M(Span<List<int>> [|data|])
                    {
                        var count = data.Length;
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    private void M(ReadOnlySpan<List<int>> data)
                    {
                        var count = data.Length;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInThrowExpression_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        _ = data.Length > 0 ? data[0] : throw new InvalidOperationException();
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        _ = data.Length > 0 ? data[0] : throw new InvalidOperationException();
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInRecursiveMethod_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private int Sum(Span<int> [|data|])
                    {
                        if (data.IsEmpty) return 0;
                        return data[0] + Sum(data.Slice(1));
                    }
                }
                """, """
                using System;

                class C
                {
                    private int Sum(ReadOnlySpan<int> data)
                    {
                        if (data.IsEmpty) return 0;
                        return data[0] + Sum(data.Slice(1));
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedWithMemoryExtensionsIndexOf_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private int M(Span<byte> [|data|])
                    {
                        return data.IndexOf((byte)42);
                    }
                }
                """, """
                using System;

                class C
                {
                    private int M(ReadOnlySpan<byte> data)
                    {
                        return data.IndexOf((byte)42);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedWithMemoryExtensionsContains_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private bool M(Span<char> [|data|])
                    {
                        return data.Contains('x');
                    }
                }
                """, """
                using System;

                class C
                {
                    private bool M(ReadOnlySpan<char> data)
                    {
                        return data.Contains('x');
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedWithStartsWith_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private bool M(Span<byte> [|data|], ReadOnlySpan<byte> prefix)
                    {
                        return data.StartsWith(prefix);
                    }
                }
                """, """
                using System;

                class C
                {
                    private bool M(ReadOnlySpan<byte> data, ReadOnlySpan<byte> prefix)
                    {
                        return data.StartsWith(prefix);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_AccessedViaExplicitInterfaceCast_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        IEnumerable<byte> enumerable = data.ToArray();
                        foreach (var b in enumerable) { }
                    }
                }
                """, """
                using System;
                using System.Collections.Generic;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        IEnumerable<byte> enumerable = data.ToArray();
                        foreach (var b in enumerable) { }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_PassedToGenericMethodConstrainedToSpan_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        Helper(data);
                    }

                    private void Helper<T>(Span<T> data)
                    {
                        data[0] = default;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInUsingStatement_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        var copy = data; // Local copy prevents analysis
                        Console.WriteLine(copy.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_WrittenViaCompoundAssignment_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<int> data)
                    {
                        if (data.Length > 0)
                        {
                            data[0] += 5;
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_WrittenViaPrefixIncrement_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<int> data)
                    {
                        if (data.Length > 0)
                        {
                            ++data[0];
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_WrittenViaPostfixIncrement_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<int> data)
                    {
                        if (data.Length > 0)
                        {
                            data[0]++;
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_Clear_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        data.Clear();
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_Fill_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> data)
                    {
                        data.Fill(0);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_Reverse_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<int> data)
                    {
                        data.Reverse();
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_Sort_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Span<int> data)
                    {
                        data.Sort();
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_AccessSpanAndWrite_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private void M(Memory<byte> data)
                    {
                        var span = data.Span;
                        span[0] = 42;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_ImplicitOperatorToReadOnlySpan_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        ReadOnlySpan<byte> ros = data;
                        Console.WriteLine(ros.Length);
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        ReadOnlySpan<byte> ros = data;
                        Console.WriteLine(ros.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_UsedInConstructor_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                ref struct SpanWrapper
                {
                    public ReadOnlySpan<byte> Data;
                    
                    public SpanWrapper(ReadOnlySpan<byte> data)
                    {
                        Data = data;
                    }
                }

                class C
                {
                    private SpanWrapper M(Span<byte> [|data|])
                    {
                        return new SpanWrapper(data);
                    }
                }
                """, """
                using System;

                ref struct SpanWrapper
                {
                    public ReadOnlySpan<byte> Data;
                    
                    public SpanWrapper(ReadOnlySpan<byte> data)
                    {
                        Data = data;
                    }
                }

                class C
                {
                    private SpanWrapper M(ReadOnlySpan<byte> data)
                    {
                        return new SpanWrapper(data);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_TryCopyToWithoutWrite_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private bool M(Span<byte> [|data|])
                    {
                        Span<byte> destination = stackalloc byte[10];
                        return data.TryCopyTo(destination);
                    }
                }
                """, """
                using System;

                class C
                {
                    private bool M(ReadOnlySpan<byte> data)
                    {
                        Span<byte> destination = stackalloc byte[10];
                        return data.TryCopyTo(destination);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_GetPinnableReference_NoDiagnostic()
        {
            await VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    private unsafe void M(Span<byte> data)
                    {
                        ref byte r = ref data.GetPinnableReference();
                        byte b = r;
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_AssignedBackToItself_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private void M(Span<byte> [|data|])
                    {
                        data = data.Slice(1);
                        Console.WriteLine(data.Length);
                    }
                }
                """, """
                using System;

                class C
                {
                    private void M(ReadOnlySpan<byte> data)
                    {
                        data = data.Slice(1);
                        Console.WriteLine(data.Length);
                    }
                }
                """);
        }

        [Fact]
        public async Task SpanParameter_OverloadedOperatorEquals_ProducesDiagnostic()
        {
            await VerifyFixerAsync("""
                using System;

                class C
                {
                    private bool M(Span<byte> [|data1|], Span<byte> [|data2|])
                    {
                        return data1 == data2; // Uses overloaded == operator
                    }
                }
                """, """
                using System;

                class C
                {
                    private bool M(ReadOnlySpan<byte> data1, ReadOnlySpan<byte> data2)
                    {
                        return data1 == data2; // Uses overloaded == operator
                    }
                }
                """);
        }

        [Fact]
        public async Task MemoryParameter_PinMethod_ProducesDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Buffers;

                    class C
                    {
                        private unsafe void M(Memory<byte> [|data|])
                        {
                            using (MemoryHandle handle = data.Pin())
                            {
                                byte* ptr = (byte*)handle.Pointer;
                                if (ptr != null)
                                {
                                    *ptr = 42;
                                }
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System;
                    using System.Buffers;

                    class C
                    {
                        private unsafe void M(ReadOnlyMemory<byte> data)
                        {
                            using (MemoryHandle handle = data.Pin())
                            {
                                byte* ptr = (byte*)handle.Pointer;
                                if (ptr != null)
                                {
                                    *ptr = 42;
                                }
                            }
                        }
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms =
                {
                    (solution, projectId) => solution.WithProjectCompilationOptions(projectId,
                        ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true))
                }
            }.RunAsync();
        }

        private static async Task VerifyAnalyzerAsync(
            [StringSyntax("C#-test")] string source,
            LanguageVersion languageVersion = LanguageVersion.Default)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = languageVersion,
            }.RunAsync();
        }

        private static async Task VerifyFixerAsync(
            [StringSyntax("C#-test")] string source,
            [StringSyntax("C#-test")] string fixedSource,
            LanguageVersion languageVersion = LanguageVersion.Default)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = languageVersion,
            }.RunAsync();
        }
    }
}
