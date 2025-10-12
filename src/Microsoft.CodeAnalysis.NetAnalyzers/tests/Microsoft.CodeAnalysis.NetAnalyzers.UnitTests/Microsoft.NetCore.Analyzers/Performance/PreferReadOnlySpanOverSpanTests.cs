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
            string source = @"
using System;

class C
{
    private void M(Span<byte> [|data|])
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
        var length = data.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task MemoryParameter_NotWritten_ProducesDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Memory<int> [|data|])
    {
        var span = data.Span;
        var length = span.Length;
    }
}
";
            string expected = @"
using System;

class C
{
    private void M(ReadOnlyMemory<int> data)
    {
        var span = data.Span;
        var length = span.Length;
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [Fact]
        public async Task SpanParameter_Written_NoDiagnostic()
        {
            string source = @"
using System;

class C
{
    private void M(Span<byte> data)
    {
        data[0] = 1;
    }
}
";
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
    }
}
