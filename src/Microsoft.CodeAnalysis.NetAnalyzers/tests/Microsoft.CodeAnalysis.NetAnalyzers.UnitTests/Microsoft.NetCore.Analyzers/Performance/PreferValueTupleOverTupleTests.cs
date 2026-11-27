// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferValueTupleOverTupleAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferValueTupleOverTupleFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferValueTupleOverTupleAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public class PreferValueTupleOverTupleTests
    {
        [TestMethod]
        public async Task TupleCreate_VarLocal_ItemReads_Fixed()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    void M()
                    {
                        var t = [|Tuple.Create(1, "a")|];
                        Console.WriteLine(t.Item1 + t.Item2);
                    }
                }
                """, """
                using System;

                class C
                {
                    void M()
                    {
                        var t = (1, "a");
                        Console.WriteLine(t.Item1 + t.Item2);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TupleCreate_DeconstructedOrItemRead_Fixed()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    int M()
                    {
                        var (a, b) = [|Tuple.Create(1, 2)|];
                        _ = [|Tuple.Create(3, 4)|];
                        return a + b + [|Tuple.Create(5, 6)|].Item2;
                    }
                }
                """, """
                using System;

                class C
                {
                    int M()
                    {
                        var (a, b) = (1, 2);
                        _ = (3, 4);
                        return a + b + (5, 6).Item2;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TupleCreate_EightItems_RestItemRead_Fixed()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    int M()
                    {
                        var t = [|Tuple.Create(1, 2, 3, 4, 5, 6, 7, 8)|];
                        return t.Item7 + t.Rest.Item1;
                    }
                }
                """, """
                using System;

                class C
                {
                    int M()
                    {
                        var t = (1, 2, 3, 4, 5, 6, 7, 8);
                        return t.Item7 + t.Rest.Item1;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TupleCreate_Trivia_Kept()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    int M()
                    {
                        // before
                        var t = /* a */ [|Tuple.Create(1, /* b */ 2)|] /* c */; // after

                        return t.Item1;
                    }
                }
                """, """
                using System;

                class C
                {
                    int M()
                    {
                        // before
                        var t = /* a */ (1, /* b */ 2) /* c */; // after

                        return t.Item1;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TupleCreate_Several_FixAll()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    int M()
                    {
                        var t1 = [|Tuple.Create(1, 2)|];
                        var t2 = [|Tuple.Create("a", 'b', 3.0)|];
                        var t3 = [|Tuple.Create(t1.Item1, t2.Item1)|];
                        return t1.Item2 + t2.Item2 + t3.Item2.Length;
                    }
                }
                """, """
                using System;

                class C
                {
                    int M()
                    {
                        var t1 = (1, 2);
                        var t2 = ("a", 'b', 3.0);
                        var t3 = (t1.Item1, t2.Item1);
                        return t1.Item2 + t2.Item2 + t3.Item2.Length;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task TupleCreate_NotFixable_ReportedWithoutFix()
        {
            // explicit local type, converted argument, untyped argument, one item, named arguments,
            // a discarded value, an expression tree, and a local that is reassigned
            await VerifyCSAsync("""
                using System;
                using System.Linq.Expressions;

                class C
                {
                    void M()
                    {
                        Tuple<int, string> t1 = [|Tuple.Create(1, "a")|];
                        var t2 = [|Tuple.Create<long, string>(42, "hi")|];
                        var t3 = [|Tuple.Create<int, int>(default, 1)|];
                        var t4 = [|Tuple.Create(1)|];
                        var t5 = [|Tuple.Create(item1: 1, item2: 2)|];
                        [|Tuple.Create(1, 2)|];
                        Expression<Func<int>> e = () => [|Tuple.Create(1, 2)|].Item1;
                        var t6 = [|Tuple.Create(1, 2)|];
                        t6 = [|Tuple.Create(3, 4)|];
                        Console.WriteLine(t1.Item1 + t2.Item1 + t3.Item1 + t4.Item1 + t5.Item1 + t6.Item1);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task NewTuple_Reported_NoFix()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    long M()
                    {
                        var t = [|new Tuple<long, string>(42, "hi")|];
                        return t.Item1;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task PrivateMembers_Reported_NoFix()
        {
            await VerifyCSAsync("""
                using System;
                using System.Linq;

                class C
                {
                    private readonly Tuple<int, string> [|_pair|];
                    private Tuple<int, int> [|Point|] { get; set; } = [|new Tuple<int, int>(0, 0)|];

                    C()
                    {
                        _pair = [|Tuple.Create(1, "a")|];
                    }

                    private static Tuple<int, int> [|MinMax|](int[] values) => [|Tuple.Create(values.Min(), values.Max())|];

                    private static int Sum(Tuple<int, int> [|pair|]) => pair.Item1 + pair.Item2;

                    int M(int[] values)
                    {
                        var range = MinMax(values);
                        Point = [|Tuple.Create(range.Item1, range.Item2)|];
                        return Sum([|Tuple.Create(1, 2)|]) + _pair.Item1 + Point.Item2;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task PrivateField_UsedFromNestedType_Reported()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    private static Tuple<int, int> [|s_pair|] = [|Tuple.Create(1, 2)|];

                    class Nested
                    {
                        int M() => s_pair.Item1;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task ExposedSignatures_NotReportedByDefault()
        {
            await VerifyCSAsync("""
                using System;

                public class C
                {
                    public Tuple<int, int> Field = Tuple.Create(1, 2);
                    protected Tuple<int, int> Property { get; } = Tuple.Create(1, 2);
                    internal Tuple<int, int> InternalField = Tuple.Create(1, 2);

                    public Tuple<int, int> Get() => Tuple.Create(1, 2);

                    protected int Sum(Tuple<int, int> pair) => pair.Item1 + pair.Item2;

                    internal int M() => Sum(Tuple.Create(1, 2));
                }
                """);
        }

        [TestMethod]
        public async Task ExposedSignatures_ApiSurfaceAll_Reported()
        {
            await VerifyCSAsync("""
                using System;

                public class C
                {
                    public Tuple<int, int> [|Get|]() => [|Tuple.Create(1, 2)|];

                    protected int Sum(Tuple<int, int> [|pair|]) => pair.Item1 + pair.Item2;

                    public int M() => Sum([|Tuple.Create(1, 2)|]);
                }
                """, editorConfig: "dotnet_code_quality.CA1881.api_surface = all");
        }

        [TestMethod]
        public async Task InternalMember_ApiSurfaceInternal_UsesInOtherTypesChecked()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    internal static Tuple<int, int> [|Get|]() => [|Tuple.Create(1, 2)|];
                    internal static Tuple<int, int> Checked() => Tuple.Create(1, 2);
                }

                class D
                {
                    int M() => C.Get().Item1 + (C.Checked() == null ? 0 : 1);
                }
                """, editorConfig: "dotnet_code_quality.CA1881.api_surface = private, internal");
        }

        [TestMethod]
        public async Task NullChecks_NotReported()
        {
            await VerifyCSAsync("""
                using System;

                class C
                {
                    private Tuple<int, int> _pair = Tuple.Create(1, 2);
                    private Tuple<int, int> _other;

                    int M()
                    {
                        var t1 = Tuple.Create(1, 2);
                        var t2 = Tuple.Create(1, 2);
                        var t3 = Tuple.Create(1, 2);
                        var t4 = Tuple.Create(1, 2);
                        _other ??= Tuple.Create(1, 2);
                        if (t1 == null || t2 is null || _pair != null)
                        {
                            return 0;
                        }

                        return (t3?.Item1 ?? 0) + (t4 ?? t1).Item1;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task ReferenceEqualityAndConversions_NotReported()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections;

                class C
                {
                    private readonly Tuple<int, int> _pair = Tuple.Create(1, 2);

                    void M()
                    {
                        var t1 = Tuple.Create(1, 2);
                        var t2 = Tuple.Create(1, 2);
                        var t3 = Tuple.Create(1, 2);
                        var t4 = Tuple.Create(1, 2);
                        Console.WriteLine(ReferenceEquals(t1, _pair));
                        Console.WriteLine(t2);
                        IStructuralEquatable equatable = t3;
                        lock (t4)
                        {
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task FlowsToOrFromApisOutsideTheGroup_NotReported()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;

                class C
                {
                    private Tuple<int, int> _fromList;
                    private Tuple<int, int> _passedOn = Tuple.Create(1, 2);

                    public Tuple<int, int> Exposed { get; set; }

                    Task<Tuple<int, int>> M(List<Tuple<int, int>> list, Dictionary<string, Tuple<int, int>> map)
                    {
                        list.Add(Tuple.Create(1, 2));
                        _fromList = list[0];
                        map.TryGetValue("a", out var found);
                        Tuple<int, int> local = found;
                        Exposed = Tuple.Create(1, 2);
                        Use(_passedOn);
                        Func<Tuple<int, int>> lambda = () => Tuple.Create(1, 2);
                        Func<Tuple<int, int>> methodGroup = Get;
                        return Task.FromResult(Tuple.Create(_fromList.Item1, local.Item1));
                    }

                    private static Tuple<int, int> Get() => Tuple.Create(1, 2);

                    private static void Use(object value) => Console.WriteLine(value);
                }
                """);
        }

        [TestMethod]
        public async Task MembersThatCannotChangeType_NotReported()
        {
            await VerifyCSAsync("""
                using System;

                interface I
                {
                    Tuple<int, int> Get();
                }

                abstract class B
                {
                    private protected abstract Tuple<int, int> Abstract();
                }

                class C : B, I
                {
                    private volatile Tuple<int, int> _volatile = Tuple.Create(1, 2);

                    Tuple<int, int> I.Get() => Tuple.Create(1, 2);

                    private protected override Tuple<int, int> Abstract() => Tuple.Create(1, 2);

                    private static int Optional(Tuple<int, int> pair = null) => pair.Item1;

                    int M() => Optional(Tuple.Create(1, 2)) + _volatile.Item1;
                }
                """);
        }

        [TestMethod]
        public async Task OtherLocals_NotReported()
        {
            await VerifyCSAsync("""
                using System;
                using System.Collections.Generic;

                class C
                {
                    int M(object o, List<Tuple<int, int>> list)
                    {
                        var sum = 0;
                        if (o is Tuple<int, int> matched)
                        {
                            sum += matched.Item1;
                        }

                        foreach (var item in list)
                        {
                            sum += item.Item1;
                        }

                        var copy = list[0];
                        return sum + copy.Item1;
                    }
                }
                """);
        }

        [TestMethod]
        public async Task VisualBasic_Reported()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System

                Class C
                    Private ReadOnly [|_pair|] As Tuple(Of Integer, String) = [|Tuple.Create(1, "a")|]

                    Function M() As Integer
                        Dim t = [|Tuple.Create(1, 2)|]
                        Return t.Item1 + _pair.Item1
                    End Function
                End Class
                """);
        }

        [TestMethod]
        public async Task VisualBasic_NotReported()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System

                Public Class C
                    Public Function Get1() As Tuple(Of Integer, Integer)
                        Return Tuple.Create(1, 2)
                    End Function

                    Function M() As Integer
                        Dim t = Tuple.Create(1, 2)
                        If t Is Nothing Then
                            Return 0
                        End If

                        Return t.Item1
                    End Function
                End Class
                """);
        }

        private static async Task VerifyCSAsync([StringSyntax("C#-test")] string source, [StringSyntax("C#-test")] string fixedSource = null, string editorConfig = null)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource ?? source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = LanguageVersion.Latest,
            };

            if (editorConfig is not null)
            {
                // Groups with members visible outside their type are reported at compilation end, which makes
                // those diagnostics non-local. They are never offered a fix.
                test.CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck;
                test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"""
                    root = true

                    [*]
                    {editorConfig}
                    """));
            }

            await test.RunAsync();
        }
    }
}
