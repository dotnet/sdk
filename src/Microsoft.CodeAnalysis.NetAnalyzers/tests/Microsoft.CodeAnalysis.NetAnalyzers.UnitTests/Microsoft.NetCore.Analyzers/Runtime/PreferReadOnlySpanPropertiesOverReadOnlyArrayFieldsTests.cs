// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferReadOnlySpanPropertiesOverReadOnlyArrayFields,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsTests
    {
        [TestMethod]
        [DataRow("bool", "true, true, false, false, true")]
        [DataRow("bool", "false")]
        [DataRow("bool", "ConstBool, true, false")]
        [DataRow("bool", "true, ConstBool, true")]
        [DataRow("bool", "")]
        [DataRow("byte", "7, 14, 128, 255")]
        [DataRow("byte", "8, 16, ConstByte")]
        [DataRow("byte", "")]
        [DataRow("sbyte", "-41, 11, 0")]
        [DataRow("sbyte", "ConstSByte, ConstSByte")]
        [DataRow("sbyte", "")]
        public Task ConstReadOnlyArrayFields_Diagnostic_CS(string arrayType, string arrayInitializer)
        {
            string testDeclaration = $"private static readonly {arrayType}[] {{|#0:_array|}} = new {arrayType}[] {{ {arrayInitializer} }};";
            string fixedDeclaration = $"private static ReadOnlySpan<{arrayType}> _array => new {arrayType}[] {{ {arrayInitializer} }};";
            string format = @"
using System;
public class C
{{
    {0}
    private const byte ConstByte = 7;
    private const sbyte ConstSByte = -7;
    private const bool ConstBool = true;
}}";
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, testDeclaration) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments(arrayType) }
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, fixedDeclaration) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(@"
    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 }, {|#1:b|} = new byte[] { 5, 7 };",
            @"
    private static ReadOnlySpan<byte> b => new byte[] { 5, 7 };
    private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };", 2, 2)]
        [DataRow(@"
    private static readonly byte[] {|#0:a|} = new byte[] { 1 }, b = new byte[] { field };",
            @"
    private static readonly byte[] b = new byte[] { field };
    private static ReadOnlySpan<byte> a => new byte[] { 1 };")]
        [DataRow(@"
    private static readonly byte[] a = new byte[] { field }, {|#0:b|} = new byte[] { 1 };",
            @"
    private static readonly byte[] a = new byte[] { field };
    private static ReadOnlySpan<byte> b => new byte[] { 1 };")]
        [DataRow(@"
    private static readonly byte[] a = new byte[] { 1, 2, field }, {|#0:b|} = new byte[] { 4, 5, 6 }, c = new byte[] { field, field };",
            @"
    private static readonly byte[] a = new byte[] { 1, 2, field }, c = new byte[] { field, field };
    private static ReadOnlySpan<byte> b => new byte[] { 4, 5, 6 };")]
        [DataRow(@"
    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2 }, b = new byte[] { field, 4 }, {|#1:c|} = new byte[] { 5, 6, 7 };",
            @"
    private static readonly byte[] b = new byte[] { field, 4 };
    private static ReadOnlySpan<byte> c => new byte[] { 5, 6, 7 };
    private static ReadOnlySpan<byte> a => new byte[] { 1, 2 };", 2)]
        public Task MultipleFieldsDeclaredSingleLine_FixedCorrectly_CS(string declaration, string fixedDeclaration, int expectedDiagnostics = 1, int fixAllIterations = 1)
        {
            string format = @"
using System;
public class C
{{
    private static byte field;
    private static byte Method() => 6;
{0}
}}";
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, declaration) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, fixedDeclaration) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                NumberOfFixAllIterations = fixAllIterations
            };
            var diagnostics = Enumerable.Range(0, expectedDiagnostics).Select(x => VerifyCS.Diagnostic(Rule).WithLocation(x).WithArguments("byte"));
            test.TestState.ExpectedDiagnostics.AddRange(diagnostics);
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("int n = a[2];")]
        [DataRow("int n = a[1] + a[2];")]
        [DataRow("(byte, byte) t = (a[1], a[2]);")]
        [DataRow("byte b, c; (b, c) = (a[1], a[2]);")]
        [DataRow("int n = a.Length;")]
        [DataRow("a[0].ToString();")]
        public Task LegalUsage_Diagnostic_CS(string code)
        {
            string format = @"
using System;
public class C
{{
    private static {0} new byte[] {{ 2, 4, 8, 16 }};
    public void M()
    {{
        {1}
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "readonly byte[] {|#0:a|} =", code) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "ReadOnlySpan<byte> a =>", code) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("a.AsSpan()", "a")]
        [DataRow("MemoryExtensions.AsSpan(a)", "a")]
        [DataRow("a.AsSpan(3)", "a.Slice(3)")]
        [DataRow("MemoryExtensions.AsSpan(a, 3)", "a.Slice(3)")]
        [DataRow("a.AsSpan(1, 3)", "a.Slice(1, 3)")]
        [DataRow("MemoryExtensions.AsSpan(a, 1, 3)", "a.Slice(1, 3)")]
        public Task AsSpanCallToRosArgument_Diagnostic_CS(string code, string fixedCode)
        {
            string format = @"
using System;
public class C
{{
    private static {0} new byte[] {{ 2, 4, 8, 16 }};
    public void ConsumeRos(ReadOnlySpan<byte> ros) {{ }}
    public void M()
    {{
        ConsumeRos({1});
        ReadOnlySpan<byte> ros = {1};
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "readonly byte[] {|#0:a|} =", code) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "ReadOnlySpan<byte> a =>", fixedCode) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("a[0..]")]
        [DataRow("a[^2..]")]
        [DataRow("a[1..^1]")]
        [DataRow("a[0..^0]")]
        [DataRow("a[1..3]")]
        [DataRow("a[..^0]")]
        public Task ArraySliceIndexer_Diagnostic_CS(string code)
        {
            string format = @"
using System;
public class C
{{
    private static {0} new byte[] {{ 2, 4, 8, 16 }};
    public void ConsumeRos(ReadOnlySpan<byte> ros) {{ }}
    public void M()
    {{
        ConsumeRos({1});
        ReadOnlySpan<byte> ros = {1};
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "readonly byte[] {|#0:a|} =", code) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "ReadOnlySpan<byte> a =>", code) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                LanguageVersion = LanguageVersion.CSharp10
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("new byte[] { GetByte(), 7, 14 }")]
        [DataRow("GetBytes()")]
        [DataRow("new byte[] { 6, 19, GetByte() }")]
        [DataRow("new byte[] { 5, readOnlyByte, 5 }")]
        [DataRow("new byte[] { 4, mutableByte, 4 }")]
        public Task NonConstInitializer_NoDiagnostic_CS(string initializer)
        {
            var test = new VerifyCS.Test
            {
                TestCode = $@"
using System;
public class C
{{
    private static byte[] GetBytes() => null;
    private static byte GetByte() => 4;
    private static readonly byte readOnlyByte = 7;
    private static byte mutableByte = 6;

    private static readonly byte[] a = {initializer};
}}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("ConsumeArray(a);")]
        [DataRow("ConsumeSpan(a);")]
        [DataRow("ConsumeEnumerable(a);")]
        [DataRow("int n = a.Rank;")]
        [DataRow("a.CopyTo(new byte[5], 0);")]
        [DataRow("a[0] = 12;")]
        [DataRow("(a[0], a[1]) = t;")]
        [DataRow("(a[1], b) = t;")]
        [DataRow("(b, a[1]) = t;")]
        [DataRow("byte[] c; (b, c) = (1, a);")]
        [DataRow("a[1] += 12;")]
        [DataRow("a[2] -= 12;")]
        [DataRow("a[3] *= 12;")]
        [DataRow("a[4] /= 12;")]
        [DataRow("a[1]++;")]
        [DataRow("++a[1];")]
        [DataRow("a[1]--;")]
        [DataRow("--a[1];")]
        [DataRow("ref byte r = ref a[1];")]
        [DataRow("byte[] local = a;")]
        [DataRow("byte[] local; local = a;")]
        [DataRow("byte[] local = null; local ??= a;")]
        [DataRow("ConsumeByteRef(ref a[3]);")]
        [DataRow("ConsumeByteOut(out a[3]);")]
        [DataRow("ConsumeImplicit(a);")]
        [DataRow("ConsumeExplicit((Explicit)a);")]
        [DataRow("new C(a);")]
        public Task IllegalFieldUsage_NoDiagnostic_CS(string code)
        {
            var test = new VerifyCS.Test
            {
                TestCode = $@"
using System;
using System.Collections.Generic;
public class C
{{
    private static readonly byte[] a = new byte[] {{ 1, 2, 3 }};
    private static void ConsumeArray(byte[] bytes) {{ }}
    private static void ConsumeEnumerable(IEnumerable<byte> bytes) {{ }}
    private static void ConsumeSpan(Span<byte> bytes) {{ }}
    private static void ConsumeByteRef(ref byte b) {{ }}
    private static void ConsumeByteOut(out byte b) => b = default;
    private static void ConsumeImplicit(Implicit i) {{ }}
    private static void ConsumeExplicit(Explicit e) {{ }}
    public C(byte[] bytes) {{ }}

    public void M(byte b, (byte x, byte y) t)
    {{
        {code}
    }}
}}

public class Implicit
{{
    public static implicit operator Implicit(byte[] operand) => new Implicit();
}}

public class Explicit
{{
    public static explicit operator Explicit(byte[] operand) => new Explicit();
}}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp10
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("ConsumeSpan(a.AsSpan());")]
        [DataRow("ConsumeSpan(a.AsSpan(1));")]
        [DataRow("ConsumeSpan(a.AsSpan(1, 3));")]
        [DataRow("var span = a.AsSpan();")]
        [DataRow("var span = a.AsSpan(1);")]
        [DataRow("var span = a.AsSpan(1, 3);")]
        public Task IllegalAsSpanResultUsage_NoDiagnostic_CS(string code)
        {
            string format = @"
using System;
public class C
{{
    private static readonly byte[] a = new byte[] {{ 2, 4, 6, 8 }};
    private void ConsumeSpan(Span<byte> span) {{ }}
    public void M()
    {{
        {0}
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestCode = string.Format(CultureInfo.InvariantCulture, format, code),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ElementReturnByRef_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
public class C
{
    private static readonly byte[] a = new byte[] { 1, 2, 3 };

    public ref byte M()
    {
        return ref a[1];
    }
}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("private readonly byte[] a = new byte[] { 1 };")]
        [DataRow("private static byte[] a = new byte[] { 1 };")]
        [DataRow("private static readonly byte[,] a = new byte[,] { { 1, 2 }, { 3, 4 } };")]
        [DataRow("private static readonly byte[][] a = new byte[][] { new byte[] { 1, 2 }, new byte[] { 3, 4, 5 } };")]
        [DataRow("private static byte[] A { get; } = new byte[] { 1 };")]
        [DataRow("private static readonly short[] a = new short[] { 1 };")]
        [DataRow("private static readonly int[] a = new int[] { 1 };")]
        [DataRow("private static readonly string[] a = new string[] { nameof(a) };")]
        [DataRow("private static readonly byte[] a;")]
        [DataRow("private static readonly byte[] a = new byte[123];")]
        [DataRow("internal static readonly byte[] a = new byte[] { 1 };")]
        [DataRow("protected static readonly byte[] a = new byte[] { 1 };")]
        [DataRow("public static readonly byte[] a = new byte[] { 1 };")]
        [DataRow("protected internal static readonly byte[] a = new byte[] { 1 };")]
        [DataRow("protected private static readonly byte[] a = new byte[] { 1 };")]
        public Task IllegalDeclarations_NoDiagnostic_CS(string declaration)
        {
            var test = new VerifyCS.Test
            {
                TestCode = $@"
public class C
{{
    {declaration}
}}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("a = new byte[1];")]
        [DataRow("(a, b) = t;")]
        [DataRow("a = GetBytes();")]
        [DataRow("a = new byte[] { 1, 2, 3 };")]
        [DataRow("ConsumeBytesRef(ref a);")]
        [DataRow("ref byte[] r = ref a;")]
        public Task MutationInStaticCtor_NoDiagnostic_CS(string code)
        {
            var test = new VerifyCS.Test
            {
                TestCode = $@"
using System;
public class C
{{
    private static readonly byte[] a = new byte[] {{ 1 }};
    private static byte[] GetBytes() => new byte[1];
    private static void ConsumeBytesRef(ref byte[] bytes) {{ }}
    private static byte b;
    private static (byte[], byte) t;

    static C()
    {{
        {code}
    }}
}}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("byte[]")]
        [DataRow("Span<byte>")]
        public Task ReturnArrayOrSpan_NoDiagnostic_CS(string returnType)
        {
            string source = $@"
using System;
public class C
{{
    private static readonly byte[] a = new byte[] {{ 1, 2, 3 }};
    private {returnType} M()
    {{
        return a;
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task UsedInArrayInitializer_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
public class C
{
    private static readonly byte[] a = new byte[] { 1, 2, 3 };
    private static readonly byte[][] b = new byte[][]
    {
        a,
        new byte[] { 4, 5, 6, 7 },
        new byte[] { 8, 9 }
    };
}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task UsedInObjectInitializer_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
public class O
{
    public byte[] A { get; set; }
    public int I { get; set; }
}

public class C
{
    private static readonly byte[] a = new byte[] { 1, 2, 3 };
    public void M()
    {
        var o = new O { A = a, I = 12 };
    }
}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        private static DiagnosticDescriptor Rule => PreferReadOnlySpanPropertiesOverReadOnlyArrayFields.Rule;
    }
}
