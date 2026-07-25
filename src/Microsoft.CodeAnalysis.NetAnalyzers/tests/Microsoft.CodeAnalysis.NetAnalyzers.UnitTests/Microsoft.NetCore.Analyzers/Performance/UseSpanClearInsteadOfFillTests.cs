// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.CSharpUseSpanClearInsteadOfFillAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseSpanClearInsteadOfFillFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public class UseSpanClearInsteadOfFillTests
    {
        [TestMethod]
        public async Task TestCodeFix()
        {
            string source = @"
using System;

class C
{
    void M(Span<byte> span)
    {
        [|span.Fill(0)|];
    }
}
";
            string expected = @"
using System;

class C
{
    void M(Span<byte> span)
    {
        span.Clear();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        [DataRow("int", "0")]
        [DataRow("int", "1 - 1")]
        [DataRow("long", "0")]
        [DataRow("double", "0")]
        [DataRow("double", "0.0")]
        [DataRow("nint", "0")]
        [DataRow("int", "(int)-0.0")]
        [DataRow("object", "null")]
        [DataRow("string", "null")]
        [DataRow("int?", "null")]
        [DataRow("int", "default")]
        [DataRow("int?", "default")]
        [DataRow("DateTime", "new DateTime()")]
        [DataRow("DateTime", "new DateTime { }")]
        [DataRow("DateTime", "default")]
        [DataRow("DateTime", "default(DateTime)")]
        [DataRow("DayOfWeek", "DayOfWeek.Sunday")]
        [DataRow("DayOfWeek", "(DayOfWeek)0")]
        [DataRow("char", "'\\0'")]
        [DataRow("bool", "false")]
        public async Task TestDefaultValue(string type, string value)
        {
            string source = $@"
using System;

class C
{{
    void M(Span<{type}> span)
    {{
        [|span.Fill({value})|];
    }}
}}
";
            string expected = $@"
using System;

class C
{{
    void M(Span<{type}> span)
    {{
        span.Clear();
    }}
}}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        [DataRow("int", "1")]
        [DataRow("float", "-0.0f")]
        [DataRow("double", "-0.0")]
        [DataRow("decimal", "-0.0m")]
        [DataRow("string", "\"\"")]
        [DataRow("object", "new object()")]
        [DataRow("int?", "0")]
        [DataRow("int?", "default(int)")]
        [DataRow("DateTime?", "new DateTime()")]
        [DataRow("DateTime?", "default(DateTime)")]
        [DataRow("DateTimeOffset", "new DateTime()")]
        [DataRow("DateTimeOffset", "default(DateTime)")]
        public async Task TestNonDefaultValue(string type, string value)
        {
            string source = $@"
using System;

class C
{{
    void M(Span<{type}> span)
    {{
        span.Fill({value});
    }}
}}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task TestGeneric_Unconstrained()
        {
            string source = @"
using System;

class C
{
    void M<T>(Span<T> span)
    {
        [|span.Fill(default)|];
    }
}
";
            string expected = @"
using System;

class C
{
    void M<T>(Span<T> span)
    {
        span.Clear();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        public async Task TestGeneric_Reference_Null()
        {
            string source = @"
#nullable enable

using System;

class C
{
    void M<T>(Span<T?> span) where T : class
    {
        [|span.Fill(null)|];
    }
}
";
            string expected = @"
#nullable enable

using System;

class C
{
    void M<T>(Span<T?> span) where T : class
    {
        span.Clear();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        public async Task TestGeneric_ValueType_Null()
        {
            string source = @"
using System;

class C
{
    void M<T>(Span<T?> span) where T : struct
    {
        [|span.Fill(null)|];
    }
}
";
            string expected = @"
using System;

class C
{
    void M<T>(Span<T?> span) where T : struct
    {
        span.Clear();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        public async Task TestGeneric_Reference_DefaultT()
        {
            string source = @"
#nullable enable

using System;

class C
{
    void M<T>(Span<T?> span) where T : class
    {
        [|span.Fill(default(T))|];
    }
}
";
            string expected = @"
#nullable enable

using System;

class C
{
    void M<T>(Span<T?> span) where T : class
    {
        span.Clear();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        public async Task TestGeneric_ValueType_DefaultT()
        {
            string source = @"
using System;

class C
{
    void M<T>(Span<T?> span) where T : struct
    {
        span.Fill(default(T));
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task TestCustomConversion()
        {
            string source = @"
using System;

struct S
{
    public static explicit operator S(int value) => throw null;
    public static explicit operator int(S value) => throw null;
}

class C
{
    void M(Span<S> span)
    {
        span.Fill((S)0);
    }

    void M(Span<int> span)
    {
        span.Fill((int)(S)0);
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task TestDerived()
        {
            string source = @"
using System;

class Base { }
class Derived : Base { }

class C
{
    void M(Span<Base> span)
    {
        [|span.Fill(default(Derived))|];
    }
}
";
            string expected = @"
using System;

class Base { }
class Derived : Base { }

class C
{
    void M(Span<Base> span)
    {
        span.Clear();
    }
}
";
            await VerifyCSCodeFixAsync(source, expected);
        }

        [TestMethod]
        public async Task TestStructParameterlessConstructor()
        {
            string source = @"

using System;

struct S
{
    int x;
    public S() => x = 4;
}

class C
{
    void M(Span<S> span)
    {
        span.Fill(new S());
    }
}
";
            await VerifyCSCodeFixAsync(source, source);
        }

        [TestMethod]
        public async Task TestBadFillCallAsync()
        {
            string source = @"

using System;

class C
{
    void M(Span<int> span)
    {
        span.Fill();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerError("CS7036").WithSpan(9, 14, 9, 18));
        }

        private static Task VerifyCSCodeFixAsync(string source, string corrected)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp10,
                FixedCode = corrected,
            };

            return test.RunAsync(CancellationToken.None);
        }
    }
}
