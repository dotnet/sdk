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
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer,
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
        [DataRow("short", "1, -2")]
        [DataRow("ushort", "1, 2")]
        [DataRow("char", "'a', 'b'")]
        [DataRow("int", "1, -2")]
        [DataRow("uint", "1u, 2u")]
        [DataRow("float", "1.0f, -2.0f")]
        [DataRow("long", "1L, -2L")]
        [DataRow("ulong", "1UL, 2UL")]
        [DataRow("double", "1.0, -2.0")]
        public Task MultiBytePrimitiveArrayFields_DiagnosticWhenCreateSpanIsAvailable_CS(
            string arrayType,
            string arrayInitializer)
        {
            string testDeclaration =
                $"private static readonly {arrayType}[] {{|#0:_array|}} = new {arrayType}[] {{ {arrayInitializer} }};";
            string fixedDeclaration =
                $"private static ReadOnlySpan<{arrayType}> _array => new {arrayType}[] {{ {arrayInitializer} }};";
            const string format = """
                using System;
                public class C
                {{
                    {0}
                }}
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, testDeclaration) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments(arrayType) },
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, fixedDeclaration) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                },
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task MultiBytePrimitiveArrayField_NoDiagnosticWithoutCreateSpan_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    public class C
                    {
                        private static readonly int[] _array = new int[] { 1, 2 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task BeforeCSharp7_2_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    public class C
                    {
                        private static readonly byte[] a = new byte[] { 1, 2, 3 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp7_1,
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task CSharp7_2_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp7_2,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") },
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ForEachCollection_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };

                        public static int Sum()
                        {
                            int sum = 0;
                            foreach (byte value in a)
                            {
                                sum += value;
                            }

                            return sum;
                        }
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };

                        public static int Sum()
                        {
                            int sum = 0;
                            foreach (byte value in a)
                            {
                                sum += value;
                            }

                            return sum;
                        }
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") },
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ForEachBodyWritesElement_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    public class C
                    {
                        private static readonly byte[] a = new byte[] { 1, 2, 3 };

                        public static void M()
                        {
                            foreach (byte value in a)
                            {
                                a[0] = value;
                            }
                        }
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("""
            public static async Task M()
            {
                foreach (byte value in a)
                {
                    await Task.Yield();
                }
            }
            """)]
        [DataRow("""
            public static IEnumerable<byte> M()
            {
                foreach (byte value in a)
                {
                    yield return value;
                }
            }
            """)]
        [DataRow("""
            public static Action M() => () =>
            {
                foreach (byte value in a)
                {
                }
            };
            """)]
        [DataRow("""
            public static void M()
            {
                Local();

                void Local()
                {
                    foreach (byte value in a)
                    {
                    }
                }
            }
            """)]
        [DataRow("""
            public static void M()
            {
                foreach (byte value in (IEnumerable<byte>)a)
                {
                }
            }
            """)]
        [DataRow("""
            public static void M()
            {
                foreach (byte value in a as byte[])
                {
                }
            }
            """)]
        public Task UnsupportedForEachUsage_NoDiagnostic_CS(string usage)
        {
            var test = new VerifyCS.Test
            {
                TestCode = $$"""
                    using System;
                    using System.Collections.Generic;
                    using System.Threading.Tasks;
                    public class C
                    {
                        private static readonly byte[] a = new byte[] { 1, 2, 3 };

                    {{usage}}
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task NullableArrayDeclaration_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    #nullable enable
                    using System;
                    public class C
                    {
                        private static readonly byte[]? {|#0:a|} = { 1, 2, 3 };
                    }
                    """,
                FixedCode = """
                    #nullable enable
                    using System;
                    public class C
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp10,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") },
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task AliasedArrayDeclaration_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using MyArray = byte[];
                    public class C
                    {
                        private static readonly MyArray {|#0:a|} = { 1, 2, 3 };
                    }
                    """,
                FixedCode = """
                    using System;
                    using MyArray = byte[];
                    public class C
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp12,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") },
            };

            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task NoSystemUsing_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    public class C
                    {
                        private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                    }
                    """,
                FixedCode = """
                    public class C
                    {
                        private static System.ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ImplicitArrayInitializer_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static readonly byte[] {|#0:a|} = { 1, 2, 3 };
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(@"
    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 }, {|#1:b|} = new byte[] { 5, 7 };",
            @"
    private static ReadOnlySpan<byte> b => new byte[] { 5, 7 };
    private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };", 2)]
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
        [DataRow(@"
    [Obsolete]
    private static readonly byte[] {|#0:a|} = new byte[] { 1 }, b = new byte[] { field };",
            @"
    [Obsolete]
    private static readonly byte[] b = new byte[] { field };
    [Obsolete]
    private static ReadOnlySpan<byte> a => new byte[] { 1 };")]
        [DataRow(@"
    [Obsolete]
    private static readonly byte[] {|#0:a|} = new byte[] { 1 }, {|#1:b|} = new byte[] { 2 };",
            @"
    [Obsolete]
    private static ReadOnlySpan<byte> b => new byte[] { 2 };
    [Obsolete]
    private static ReadOnlySpan<byte> a => new byte[] { 1 };", 2)]
        public Task MultipleFieldsDeclaredSingleLine_FixedCorrectly_CS(string declaration, string fixedDeclaration, int expectedDiagnostics = 1)
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
                }
            };
            var diagnostics = Enumerable.Range(0, expectedDiagnostics).Select(x => VerifyCS.Diagnostic(Rule).WithLocation(x).WithArguments("byte"));
            test.TestState.ExpectedDiagnostics.AddRange(diagnostics);
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ManyDiagnostics_FixAllCompletesInOnePass_CS()
        {
            const int DiagnosticCount = 2;
            string sourceFields = string.Join(
                Environment.NewLine,
                Enumerable.Range(0, DiagnosticCount).Select(i => $"    private static readonly byte[] {{|#{i}:a{i}|}} = new byte[] {{ {i} }};"));
            string fixedFields = string.Join(
                Environment.NewLine,
                Enumerable.Range(0, DiagnosticCount).Select(i => $"    private static ReadOnlySpan<byte> a{i} => new byte[] {{ {i} }};"));
            string format = """
                using System;
                public class C
                {{
                {0}
                }}
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, sourceFields) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, fixedFields) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                NumberOfFixAllIterations = 1
            };
            test.TestState.ExpectedDiagnostics.AddRange(
                Enumerable.Range(0, DiagnosticCount).Select(i => VerifyCS.Diagnostic(Rule).WithLocation(i).WithArguments("byte")));
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task FieldWithTrivia_TriviaPreserved_CS()
        {
            // lang=C#-test
            string source = """
                using System;
                public class C
                {
                    // Leading comment.

                    /// <summary>The data.</summary>
                    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 }; // Trailing comment.

                    private static byte field;
                }
                """;
            // lang=C#-test
            string fixedSource = """
                using System;
                public class C
                {
                    // Leading comment.

                    /// <summary>The data.</summary>
                    private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 }; // Trailing comment.

                    private static byte field;
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { fixedSource },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task FieldWithAttribute_AttributePreserved_CS()
        {
            // lang=C#-test
            string source = """
                using System;
                public class C
                {
                    [Obsolete]
                    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                }
                """;
            // lang=C#-test
            string fixedSource = """
                using System;
                public class C
                {
                    [Obsolete]
                    private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { fixedSource },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task FieldWithAttributeNotValidOnProperty_NoDiagnostic_CS()
        {
            //  'ThreadStaticAttribute' is 'AttributeTargets.Field', so it cannot move onto the
            //  generated property and the field has no valid rewrite.
            // lang=C#-test
            string source = """
                using System;
                public class C
                {
                    [ThreadStatic]
                    private static readonly byte[] a = new byte[] { 1, 2, 3 };

                    public byte M() => a[0];
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task FieldWithExplicitAttributeTarget_DiagnosticWithoutFix_CS()
        {
            // lang=C#-test
            string source = """
                using System;

                [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
                internal sealed class AAttribute : Attribute
                {
                }

                public class C
                {
                    [field: A]
                    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                }
                """;
            // lang=C#-test
            string fixedSource = """
                using System;

                [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
                internal sealed class AAttribute : Attribute
                {
                }

                public class C
                {
                    [field: A]
                    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { fixedSource },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task FieldWithDirectives_DiagnosticWithoutFix_CS()
        {
            const string Source = """
                using System;
                public class C
                {
                    private static readonly byte[]
                #if DEBUG
                        a
                #else
                        {|#0:b|}
                #endif
                        = new byte[] { 1, 2, 3 };
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { Source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { Source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte") }
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("int n = a[2];")]
        [DataRow("int n = a[1] + a[2];")]
        [DataRow("(byte, byte) t = (a[1], a[2]);")]
        [DataRow("byte b, c; (b, c) = (a[1], a[2]);")]
        [DataRow("int n = a.Length;")]
        [DataRow("a[0].ToString();")]
        [DataRow("string name = nameof(a);")]
        [DataRow("int n = (a).Length;")]
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
        [DataRow("{|#1:a|}.AsSpan()", "a")]
        [DataRow("MemoryExtensions.AsSpan({|#1:a|})", "a")]
        [DataRow("MemoryExtensions.AsSpan(array: {|#1:a|})", "a")]
        [DataRow("{|#1:a|}.AsSpan(3)", "a.Slice(3)")]
        [DataRow("MemoryExtensions.AsSpan({|#1:a|}, 3)", "a.Slice(3)")]
        [DataRow("MemoryExtensions.AsSpan(array: {|#1:a|}, start: 3)", "a.Slice(start: 3)")]
        [DataRow("MemoryExtensions.AsSpan(start: 3, array: {|#1:a|})", "a.Slice(start: 3)")]
        [DataRow("{|#1:a|}.AsSpan(1, 3)", "a.Slice(1, 3)")]
        [DataRow("MemoryExtensions.AsSpan({|#1:a|}, 1, 3)", "a.Slice(1, 3)")]
        [DataRow("MemoryExtensions.AsSpan(array: {|#1:a|}, start: 1, length: 3)", "a.Slice(start: 1, length: 3)")]
        [DataRow("MemoryExtensions.AsSpan(length: 3, start: 1, array: {|#1:a|})", "a.Slice(length: 3, start: 1)")]
        [DataRow("{|#1:a|}.AsSpan(^2)", "a[(^2)..]")]
        [DataRow("MemoryExtensions.AsSpan(array: {|#1:a|}, startIndex: ^2)", "a[(^2)..]")]
        [DataRow("{|#1:a|}.AsSpan(1..3)", "a[1..3]")]
        [DataRow("MemoryExtensions.AsSpan(range: 1..3, array: {|#1:a|})", "a[1..3]")]
        [DataRow("({|#1:a|}).AsSpan()", "a")]
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
    }}
}}";
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "readonly byte[] {|#0:a|} =", code) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { string.Format(CultureInfo.InvariantCulture, format, "ReadOnlySpan<byte> a =>", fixedCode) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                LanguageVersion = LanguageVersion.CSharp10
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("new Index(1)")]
        [DataRow("new Range(new Index(1), new Index(2))")]
        public Task IndexOrRangeAsSpanCallBeforeCSharp8_DiagnosticWithoutFix_CS(string argument)
        {
            string source = $$"""
                using System;
                public class C
                {
                    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                    public static ReadOnlySpan<byte> M() => MemoryExtensions.AsSpan({|#1:a|}, {{argument}});
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                },
                LanguageVersion = LanguageVersion.CSharp7_3
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task AsSpanCallWithDirectives_DiagnosticWithoutFix_CS()
        {
            const string Source = """
                using System;
                public class C
                {
                    private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                    public static ReadOnlySpan<byte> M() => MemoryExtensions.AsSpan(
                #if DEBUG
                        a,
                #else
                        {|#1:a|},
                #endif
                        1);
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { Source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { Source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task MultipleAsSpanCalls_AllFixed_CS()
        {
            // lang=C#-test
            string source = """
                using System;
                public class C
                {
                    private static readonly byte[] {|#0:a|} = new byte[] { 2, 4, 8, 16 };

                    private static void Consume(ReadOnlySpan<byte> value)
                    {
                    }

                    public static void M()
                    {
                        Consume({|#1:a|}.AsSpan());
                        Consume(MemoryExtensions.AsSpan(length: 2, array: {|#2:a|}, start: 1));
                        Consume({|#3:a|}.AsSpan(^2));
                    }
                }
                """;
            // lang=C#-test
            string fixedSource = """
                using System;
                public class C
                {
                    private static ReadOnlySpan<byte> a => new byte[] { 2, 4, 8, 16 };

                    private static void Consume(ReadOnlySpan<byte> value)
                    {
                    }

                    public static void M()
                    {
                        Consume(a);
                        Consume(a.Slice(length: 2, start: 1));
                        Consume(a[(^2)..]);
                    }
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithLocation(2).WithLocation(3).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { fixedSource },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                },
                LanguageVersion = LanguageVersion.CSharp10
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task AsSpanCallInAnotherPartialDeclaration_DiagnosticWithoutFix_CS()
        {
            // lang=C#-test
            string fieldSource = """
                using System;

                public partial class C
                {
                    private static readonly byte[] {|#0:a|} = new byte[] { 2, 4, 8, 16 };
                }
                """;
            // lang=C#-test
            string useSource = """
                using System;

                public partial class C
                {
                    private static void Consume(ReadOnlySpan<byte> value)
                    {
                    }

                    public static void M() => Consume(MemoryExtensions.AsSpan({|#1:a|}));
                }
                """;
            // lang=C#-test
            string fixedFieldSource = """
                using System;

                public partial class C
                {
                    private static readonly byte[] {|#0:a|} = new byte[] { 2, 4, 8, 16 };
                }
                """;
            // lang=C#-test
            string fixedUseSource = """
                using System;

                public partial class C
                {
                    private static void Consume(ReadOnlySpan<byte> value)
                    {
                    }

                    public static void M() => Consume(MemoryExtensions.AsSpan({|#1:a|}));
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { ("Field.cs", fieldSource), ("Use.cs", useSource) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { ("Field.cs", fixedFieldSource), ("Use.cs", fixedUseSource) },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task AsSpanCallInNestedType_Diagnostic_CS()
        {
            // lang=C#-test
            string source = """
                using System;
                public class C
                {
                    private static readonly byte[] {|#0:a|} = new byte[] { 2, 4, 8, 16 };

                    private class Inner
                    {
                        private static void Consume(ReadOnlySpan<byte> value)
                        {
                        }

                        public static void M() => Consume({|#1:a|}.AsSpan());
                    }
                }
                """;
            // lang=C#-test
            string fixedSource = """
                using System;
                public class C
                {
                    private static ReadOnlySpan<byte> a => new byte[] { 2, 4, 8, 16 };

                    private class Inner
                    {
                        private static void Consume(ReadOnlySpan<byte> value)
                        {
                        }

                        public static void M() => Consume(a);
                    }
                }
                """;
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                    ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte") }
                },
                FixedState =
                {
                    Sources = { fixedSource },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net50
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ArrayUseInNestedType_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    public class C
                    {
                        private static readonly byte[] a = new byte[] { 2, 4, 8, 16 };

                        private class Inner
                        {
                            public static byte[] M() => a;
                        }
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ArrayUseInGeneratedPartial_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("Field.cs", """
                            public partial class C
                            {
                                private static readonly byte[] a = new byte[] { 2, 4, 8, 16 };
                            }
                            """),
                        ("Use.g.cs", """
                            public partial class C
                            {
                                public static byte[] M() => a;
                            }
                            """)
                    },
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
        [DataRow("if (a == null) { }")]
        [DataRow("if (a is null) { }")]
        [DataRow("lock (a) { }")]
        [DataRow("byte[] local = a[0..];")]
        [DataRow("ConsumeArray(a[1..3]);")]
        [DataRow("byte c; ((a[0], b), c) = (((byte)1, (byte)2), (byte)3);")]
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
        public Task FieldReferencedInExpressionTree_NoDiagnostic_CS()
        {
            //  A ReadOnlySpan<T> property cannot be referenced inside an expression tree (CS8640),
            //  so a field used there must not be converted.
            var test = new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Linq.Expressions;
public class C
{
    private static readonly byte[] a = new byte[] { 1, 2, 3 };
    public static Expression<Func<int>> Length() => () => a.Length;
    public static Expression<Func<byte>> Element() => () => a[0];
}",
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp10
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("ConsumeSpan(a.AsSpan());")]
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
        [DataRow("private static readonly decimal[] a = new decimal[] { 1 };")]
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

        [TestMethod]
        public Task ConstructedGenericAsSpan_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C<T>
                    {
                        private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3 };
                        public static ReadOnlySpan<byte> M() => {|#1:C<int>.a|}.AsSpan();
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C<T>
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3 };
                        public static ReadOnlySpan<byte> M() => C<int>.a;
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp10,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task ConstructedGenericMutation_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    public class C<T>
                    {
                        private static readonly byte[] a = new byte[] { 1, 2, 3 };

                        static C()
                        {
                            C<int>.a[0] = 0;
                        }
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task NestedLambdaInExpressionTree_NoDiagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Linq.Expressions;
                    public class C
                    {
                        private static readonly byte[] a = new byte[] { 1, 2, 3 };
                        public static Expression<Func<Func<int>>> M() => () => () => a.Length;
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task RangeArrayAsSpan_Diagnostic_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static readonly byte[] {|#0:a|} = new byte[] { 1, 2, 3, 4 };
                        private static void Consume(ReadOnlySpan<byte> value) { }
                        public static void M()
                        {
                            Consume({|#1:a|}[1..3].AsSpan());
                            Consume(MemoryExtensions.AsSpan({|#2:a|}[0..2]));
                        }
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2, 3, 4 };
                        private static void Consume(ReadOnlySpan<byte> value) { }
                        public static void M()
                        {
                            Consume(a[1..3]);
                            Consume(a[0..2]);
                        }
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                LanguageVersion = LanguageVersion.CSharp10,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithLocation(2).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task AsSpanInSurvivingDeclaratorInitializer_Fixed_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static byte[] Copy(ReadOnlySpan<byte> value) => value.ToArray();
                        private static readonly byte[] {|#0:a|} = new byte[] { 1, 2 }, b = Copy({|#1:a|}.AsSpan());
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static byte[] Copy(ReadOnlySpan<byte> value) => value.ToArray();
                        private static readonly byte[] b = Copy(a);
                        private static ReadOnlySpan<byte> a => new byte[] { 1, 2 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithLocation(1).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task MultiDeclaratorComments_Preserved_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static readonly byte[] {|#0:a|} = new byte[] { 1 }, // b documentation
                            {|#1:b|} = new byte[] { 2 };
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        // b documentation
                        private static ReadOnlySpan<byte> b => new byte[] { 2 };
                        private static ReadOnlySpan<byte> a => new byte[] { 1 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte"),
                    VerifyCS.Diagnostic(Rule).WithLocation(1).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task CommentOnSurvivingDeclarator_NotDuplicated_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static byte value;
                        private static readonly byte[] {|#0:a|} = new byte[] { 1 }, // b documentation
                            b = new byte[] { value };
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static byte value;
                        // b documentation
                        private static readonly byte[] b = new byte[] { value };
                        private static ReadOnlySpan<byte> a => new byte[] { 1 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task MultilineSurvivingDeclarator_DoesNotAddBlankLine_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static byte value;
                        private static readonly byte[] {|#0:a|} = new byte[] { 1 },
                            b = new byte[] { value };
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static byte value;
                        private static readonly byte[] b = new byte[] { value };
                        private static ReadOnlySpan<byte> a => new byte[] { 1 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public Task CommentOnConvertedMiddleDeclarator_NotDuplicated_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    public class C
                    {
                        private static byte value;
                        private static readonly byte[] a = new byte[] { value }, /* b documentation */ {|#0:b|} = new byte[] { 1 }, c = new byte[] { value };
                    }
                    """,
                FixedCode = """
                    using System;
                    public class C
                    {
                        private static byte value;
                        private static readonly byte[] a = new byte[] { value }, c = new byte[] { value };
                        /* b documentation */
                        private static ReadOnlySpan<byte> b => new byte[] { 1 };
                    }
                    """,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithLocation(0).WithArguments("byte")
                }
            };
            return test.RunAsync(CancellationToken.None);
        }

        private static DiagnosticDescriptor Rule => PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer.Rule;
    }
}
