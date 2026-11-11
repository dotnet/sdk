// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidZeroLengthArrayAllocationsAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidZeroLengthArrayAllocationsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidZeroLengthArrayAllocationsAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidZeroLengthArrayAllocationsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class AvoidZeroLengthArrayAllocationsAnalyzerTests
    {
        /// <summary>
        /// This type isn't defined in all locations where this test runs.  Need to alter the
        /// test code slightly to account for this.
        /// </summary>
        private static bool IsArrayEmptyDefined()
        {
            Assembly assembly = typeof(object).Assembly;
            Type type = assembly.GetType("System.Array");
            return type.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static) != null;
        }

        private static string GetArrayEmptySourceBasic()
        {
            const string arrayEmptySourceRaw = """

                Namespace System
                    Public Class Array
                       Public Shared Function Empty(Of T)() As T()
                           Return Nothing
                       End Function
                    End Class
                End Namespace

                """;
            return IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;
        }

        private static string GetArrayEmptySourceCSharp()
        {
            const string arrayEmptySourceRaw = """

                namespace System
                {
                    public class Array
                    {
                        public static T[] Empty<T>()
                        {
                            return null;
                        }
                    }
                }

                """;
            return IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;
        }

        [TestMethod]
        public async Task EmptyArrayCSharpAsync()
        {
            const string badSource = """

                using System.Collections.Generic;

                class C
                {
                    unsafe void M1()
                    {
                        int[] arr1 = {|#0:new int[0]|};                       // yes
                        byte[] arr2 = {|#1:{ }|};                             // yes
                        C[] arr3 = {|#2:new C[] { }|};                        // yes
                        string[] arr4 = new string[] { null };         // no
                        double[] arr5 = new double[1];                 // no
                        int[] arr6 = new[] { 1 };                      // no
                        int[][] arr7 = {|#3:new int[0][]|};                   // yes
                        int[][][][] arr8 = {|#4:new int[0][][][]|};           // yes
                        int[,] arr9 = new int[0,0];                    // no
                        int[][,] arr10 = {|#5:new int[0][,]|};                // yes
                        int[][,] arr11 = new int[1][,];                // no
                        int[,][] arr12 = new int[0,0][];               // no
                        int*[] arr13 = new int*[0];                    // no
                        List<int> list1 = new List<int>() { };         // no
                    }
                }
                """;

            const string fixedSource = """

                using System.Collections.Generic;

                class C
                {
                    unsafe void M1()
                    {
                        int[] arr1 = System.Array.Empty<int>();                       // yes
                        byte[] arr2 = System.Array.Empty<byte>();                             // yes
                        C[] arr3 = System.Array.Empty<C>();                        // yes
                        string[] arr4 = new string[] { null };         // no
                        double[] arr5 = new double[1];                 // no
                        int[] arr6 = new[] { 1 };                      // no
                        int[][] arr7 = System.Array.Empty<int[]>();                   // yes
                        int[][][][] arr8 = System.Array.Empty<int[][][]>();           // yes
                        int[,] arr9 = new int[0,0];                    // no
                        int[][,] arr10 = System.Array.Empty<int[,]>();                // yes
                        int[][,] arr11 = new int[1][,];                // no
                        int[,][] arr12 = new int[0,0][];               // no
                        int*[] arr13 = new int*[0];                    // no
                        List<int> list1 = new List<int>() { };         // no
                    }
                }
                """;
            string arrayEmptySource = GetArrayEmptySourceCSharp();

            var diagnostics = new[]
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<byte>()"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("Array.Empty<C>()"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("Array.Empty<int[]>()"),
                VerifyCS.Diagnostic().WithLocation(4).WithArguments("Array.Empty<int[][][]>()"),
                VerifyCS.Diagnostic().WithLocation(5).WithArguments("Array.Empty<int[,]>()"),
            };

            await VerifyCS.VerifyCodeFixAsync(badSource + arrayEmptySource, diagnostics, fixedSource + arrayEmptySource);

            await VerifyCS.VerifyCodeFixAsync(
                "using System;\r\n" + badSource + arrayEmptySource,
                diagnostics,
                "using System;\r\n" + fixedSource.Replace("System.Array.Empty", "Array.Empty", StringComparison.Ordinal) + arrayEmptySource);
        }

        [TestMethod]
        public async Task EmptyArrayCSharpErrorAsync()
        {
            const string badSource = """
                // This is a compile error but we want to ensure analyzer doesn't complain for it.
                [System.Runtime.CompilerServices.Dynamic(new bool[0]){|CS0116:]|}
                """;

            await VerifyCS.VerifyAnalyzerAsync(badSource);
        }

        [TestMethod]
        public async Task EmptyArrayVisualBasicAsync()
        {
            const string badSource = """

                Imports System.Collections.Generic

                <System.Runtime.CompilerServices.Dynamic(new Boolean(-1) {})> _
                Class C
                    Sub M1()
                        Dim arr1 As Integer() = {|#0:New Integer(-1) { }|}               ' yes
                        Dim arr2 As Byte() = {|#1:{ }|}                                  ' yes
                        Dim arr3 As C() = {|#2:New C(-1) { }|}                           ' yes
                        Dim arr4 As String() = New String() { Nothing }           ' no
                        Dim arr5 As Double() = New Double(1) { }                  ' no
                        Dim arr6 As Integer() = { -1 }                            ' no
                        Dim arr7 as Integer()() = {|#3:New Integer(-1)() { }|}           ' yes
                        Dim arr8 as Integer()()()() = {|#4:New Integer(  -1)()()() { }|} ' yes
                        Dim arr9 as Integer(,) = New Integer(-1,-1) { }           ' no
                        Dim arr10 as Integer()(,) = {|#5:New Integer(-1)(,) { }|}        ' yes
                        Dim arr11 as Integer()(,) = New Integer(1)(,) { }         ' no
                        Dim arr12 as Integer(,)() = New Integer(-1,-1)() { }      ' no
                        Dim arr13 as Integer() = New Integer(0) { }               ' no
                        Dim list1 as List(Of Integer) = New List(Of Integer) From { }  ' no
                    End Sub
                End Class
                """;

            const string fixedSource = """

                Imports System.Collections.Generic

                <System.Runtime.CompilerServices.Dynamic(new Boolean(-1) {})> _
                Class C
                    Sub M1()
                        Dim arr1 As Integer() = System.Array.Empty(Of Integer)()               ' yes
                        Dim arr2 As Byte() = System.Array.Empty(Of Byte)()                                  ' yes
                        Dim arr3 As C() = System.Array.Empty(Of C)()                           ' yes
                        Dim arr4 As String() = New String() { Nothing }           ' no
                        Dim arr5 As Double() = New Double(1) { }                  ' no
                        Dim arr6 As Integer() = { -1 }                            ' no
                        Dim arr7 as Integer()() = System.Array.Empty(Of Integer())()           ' yes
                        Dim arr8 as Integer()()()() = System.Array.Empty(Of Integer()()())() ' yes
                        Dim arr9 as Integer(,) = New Integer(-1,-1) { }           ' no
                        Dim arr10 as Integer()(,) = System.Array.Empty(Of Integer(,))()        ' yes
                        Dim arr11 as Integer()(,) = New Integer(1)(,) { }         ' no
                        Dim arr12 as Integer(,)() = New Integer(-1,-1)() { }      ' no
                        Dim arr13 as Integer() = New Integer(0) { }               ' no
                        Dim list1 as List(Of Integer) = New List(Of Integer) From { }  ' no
                    End Sub
                End Class
                """;

            string arrayEmptySource = GetArrayEmptySourceBasic();

            var diagnostics = new[]
            {
                VerifyVB.Diagnostic().WithLocation(0).WithArguments("Array.Empty(Of Integer)()"),
                VerifyVB.Diagnostic().WithLocation(1).WithArguments("Array.Empty(Of Byte)()"),
                VerifyVB.Diagnostic().WithLocation(2).WithArguments("Array.Empty(Of C)()"),
                VerifyVB.Diagnostic().WithLocation(3).WithArguments("Array.Empty(Of Integer())()"),
                VerifyVB.Diagnostic().WithLocation(4).WithArguments("Array.Empty(Of Integer()()())()"),
                VerifyVB.Diagnostic().WithLocation(5).WithArguments("Array.Empty(Of Integer(,))()"),
            };

            await VerifyVB.VerifyCodeFixAsync(badSource + arrayEmptySource, diagnostics, fixedSource + arrayEmptySource);

            await VerifyVB.VerifyCodeFixAsync(
                "Imports System\r\n" + badSource + arrayEmptySource,
                diagnostics,
                "Imports System\r\n" + fixedSource.Replace("System.Array.Empty", "Array.Empty", StringComparison.Ordinal) + arrayEmptySource);
        }

        [TestMethod]
        public async Task EmptyArrayCSharp_DifferentTypeKindAsync()
        {
            const string badSource = """

                class C
                {
                    void M1()
                    {
                        int[] arr1 = {|#0:new int[(long)0]|};                 // yes
                        double[] arr2 = {|#1:new double[(ulong)0]|};         // yes
                        double[] arr3 = new double[(long)1];         // no
                    }
                }
                """;

            const string fixedSource = """

                class C
                {
                    void M1()
                    {
                        int[] arr1 = System.Array.Empty<int>();                 // yes
                        double[] arr2 = System.Array.Empty<double>();         // yes
                        double[] arr3 = new double[(long)1];         // no
                    }
                }
                """;

            var diagnostics = new[]
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<double>()"),
            };

            await VerifyCS.VerifyCodeFixAsync(badSource, diagnostics, fixedSource);

            await VerifyCS.VerifyCodeFixAsync(
                "using System;\r\n" + badSource,
                diagnostics,
                "using System;\r\n" + fixedSource.Replace("System.Array.Empty", "Array.Empty", StringComparison.Ordinal));
        }

        [WorkItem(10214, "https://github.com/dotnet/roslyn/issues/10214")]
        [TestMethod]
        public async Task EmptyArrayVisualBasic_CompilerGeneratedArrayCreationAsync()
        {
            const string source = """
                Class C
                    Private Sub F(ParamArray args As String())
                    End Sub

                Private Sub G()
                        F()     ' Compiler seems to generate a param array with size 0 for the invocation.
                    End Sub
                End Class
                """;

            string arrayEmptySource = GetArrayEmptySourceBasic();

            // Should we be flagging diagnostics on compiler generated code?
            // Should the analyzer even be invoked for compiler generated code?
            await VerifyVB.VerifyAnalyzerAsync(source + arrayEmptySource);
        }

        [WorkItem(1209, "https://github.com/dotnet/roslyn-analyzers/issues/1209")]
        [TestMethod]
        public async Task EmptyArrayCSharp_CompilerGeneratedArrayCreationInObjectCreationAsync()
        {
            const string source = """
                namespace N
                {
                    using Microsoft.CodeAnalysis;
                    class C
                    {
                        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                            "RuleId",
                            "Title",
                            "MessageFormat",
                            "Dummy",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true,
                            description: "Description");
                    }
                }
                """;

            string arrayEmptySource = GetArrayEmptySourceCSharp();

            // Should we be flagging diagnostics on compiler generated code?
            // Should the analyzer even be invoked for compiler generated code?
            await VerifyCS.VerifyAnalyzerAsync(source + arrayEmptySource);
        }

        [WorkItem(1209, "https://github.com/dotnet/roslyn-analyzers/issues/1209")]
        [TestMethod]
        public async Task EmptyArrayCSharp_CompilerGeneratedArrayCreationInIndexerAccessAsync()
        {
            const string source = """
                public abstract class C
                {
                    protected abstract int this[int p1, params int[] p2] {get; set;}
                    public void M()
                    {
                        var x = this[0];
                    }
                }
                """;

            string arrayEmptySource = GetArrayEmptySourceCSharp();

            // Should we be flagging diagnostics on compiler generated code?
            // Should the analyzer even be invoked for compiler generated code?
            await VerifyCS.VerifyAnalyzerAsync(source + arrayEmptySource);
        }

        [TestMethod]
        public async Task EmptyArrayCSharp_UsedInAttribute_NoDiagnosticsAsync()
        {
            const string source = """
                using System;

                [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                class CustomAttribute : Attribute
                {
                    public CustomAttribute(object o)
                    {
                    }
                }

                [Custom(new int[0])]
                [Custom(new string[] { })]
                class C
                {
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task EmptyArrayCSharp_UsedInAttributeParams_NoDiagnosticsAsync()
        {
            const string source = """
                using System;

                [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                class CustomAttribute : Attribute
                {
                    public CustomAttribute(params int[] i)
                    {
                    }
                }

                [Custom(new int[0])]
                [Custom]
                class C
                {
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task EmptyArrayCSharp_UsedInAttributeNamedArgument_NoDiagnosticsAsync()
        {
            const string source = """
                using System;

                [AttributeUsage(AttributeTargets.All)]
                class CustomAttribute : Attribute
                {
                    public int[] Field;
                    public int[] Property { get; set; }
                }

                [Custom(Field = new int[0], Property = new int[0])]
                class C
                {
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [TestMethod]
        public async Task EmptyArrayVisualBasic_UsedInAttributeNamedArgument_NoDiagnosticsAsync()
        {
            const string source = """
                Imports System

                <AttributeUsage(AttributeTargets.All)>
                Class CustomAttribute
                    Inherits Attribute

                    Public Field As Integer()
                    Public Property [Property] As Integer()
                End Class

                <Custom(Field:=New Integer(-1) {}, [Property]:=New Integer(-1) {})>
                Class C
                End Class
                """;
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [WorkItem(1298, "https://github.com/dotnet/roslyn-analyzers/issues/1298")]
        [TestMethod]
        public async Task EmptyArrayCSharp_FieldOrPropertyInitializerAsync()
        {
            const string badSource = """

                using System;

                class C
                {
                    public int[] f1 = {|#0:new int[] { }|};
                    public int[] p1 { get; set; } = {|#1:new int[] { }|};
                }

                """;
            const string fixedSource = """

                using System;

                class C
                {
                    public int[] f1 = Array.Empty<int>();
                    public int[] p1 { get; set; } = Array.Empty<int>();
                }

                """;

            await VerifyCS.VerifyCodeFixAsync(
                badSource,
                new[]
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<int>()"),
                },
                fixedSource);
        }

        [WorkItem(1298, "https://github.com/dotnet/roslyn-analyzers/issues/1298")]
        [TestMethod]
        public async Task EmptyArrayCSharp_UsedInAssignmentAsync()
        {
            const string badSource = """

                using System;

                class C
                {
                    void M()
                    {
                        int[] l1;
                        l1 = {|#0:new int[0]|};
                        l1 = {|#1:new int[] { }|};
                    }
                }

                """;
            const string fixedSource = """

                using System;

                class C
                {
                    void M()
                    {
                        int[] l1;
                        l1 = Array.Empty<int>();
                        l1 = Array.Empty<int>();
                    }
                }

                """;
            await VerifyCS.VerifyCodeFixAsync(
                badSource,
                new[]
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<int>()"),
                },
                fixedSource);
        }

        [WorkItem(1298, "https://github.com/dotnet/roslyn-analyzers/issues/1298")]
        [TestMethod]
        public async Task EmptyArrayCSharp_DeclarationTypeDoesNotMatch_NotArrayAsync()
        {
            const string badSource = """

                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;

                class C
                {
                    public IEnumerable<int> f1 = {|#0:new int[0]|};
                    public ICollection<int> f2 = {|#1:new int[0]|};
                    public IReadOnlyCollection<int> f3 = {|#2:new int[0]|};
                    public IList<int> f4 = {|#3:new int[0]|};
                    public IReadOnlyList<int> f5 = {|#4:new int[0]|};

                    public IEnumerable f6 = {|#5:new int[0]|};
                    public ICollection f7 = {|#6:new int[0]|};
                    public IList f8 = {|#7:new int[0]|};
                }

                """;
            const string fixedSource = """

                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Collections.ObjectModel;

                class C
                {
                    public IEnumerable<int> f1 = Array.Empty<int>();
                    public ICollection<int> f2 = Array.Empty<int>();
                    public IReadOnlyCollection<int> f3 = Array.Empty<int>();
                    public IList<int> f4 = Array.Empty<int>();
                    public IReadOnlyList<int> f5 = Array.Empty<int>();

                    public IEnumerable f6 = Array.Empty<int>();
                    public ICollection f7 = Array.Empty<int>();
                    public IList f8 = Array.Empty<int>();
                }

                """;
            await VerifyCS.VerifyCodeFixAsync(
                badSource,
                new[]
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(2).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(3).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(4).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(5).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(6).WithArguments("Array.Empty<int>()"),
                    VerifyCS.Diagnostic().WithLocation(7).WithArguments("Array.Empty<int>()"),
                },
                fixedSource);
        }

        [WorkItem(1298, "https://github.com/dotnet/roslyn-analyzers/issues/1298")]
        [TestMethod]
        public async Task EmptyArrayCSharp_DeclarationTypeDoesNotMatch_DifferentElementTypeAsync()
        {
            const string badSource = """

                using System;

                class C
                {
                    public object[] f1 = {|#0:new string[0]|};
                }

                """;
            const string fixedSource = """

                using System;

                class C
                {
                    public object[] f1 = Array.Empty<string>();
                }

                """;

            await VerifyCS.VerifyCodeFixAsync(
                badSource,
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<string>()"),
                fixedSource);
        }

        [WorkItem(1298, "https://github.com/dotnet/roslyn-analyzers/issues/1298")]
        [TestMethod]
        public async Task EmptyArrayCSharp_UsedAsExpressionAsync()
        {
            const string badSource = """

                using System;

                class C
                {
                    void M1(object[] array)
                    {
                    }

                    // Tests handling of implicit conversion. Do not change to 'object[] obj'.
                    void M2(object obj)
                    {
                    }

                    void M3()
                    {
                        M1({|#0:new object[0]|});
                        M2({|#1:new object[0]|});
                    }

                    object M4() => {|#2:new object[0]|};

                    object M5()
                    {
                        return {|#3:new object[0]|};
                    }
                }

                """;
            const string fixedSource = """

                using System;

                class C
                {
                    void M1(object[] array)
                    {
                    }

                    // Tests handling of implicit conversion. Do not change to 'object[] obj'.
                    void M2(object obj)
                    {
                    }

                    void M3()
                    {
                        M1(Array.Empty<object>());
                        M2(Array.Empty<object>());
                    }

                    object M4() => Array.Empty<object>();

                    object M5()
                    {
                        return Array.Empty<object>();
                    }
                }

                """;
            await VerifyCS.VerifyCodeFixAsync(
                badSource,
                new[]
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<object>()"),
                    VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<object>()"),
                    VerifyCS.Diagnostic().WithLocation(2).WithArguments("Array.Empty<object>()"),
                    VerifyCS.Diagnostic().WithLocation(3).WithArguments("Array.Empty<object>()"),
                },
                fixedSource);
        }

        [TestMethod]
        public async Task EmptyArrayCSharp_SystemNotImportedAsync()
        {
            const string badSource = """

                class C
                {
                    public object[] f1 = {|#0:new object[0]|};
                }

                """;
            const string fixedSource = """

                class C
                {
                    public object[] f1 = System.Array.Empty<object>();
                }

                """;
            await VerifyCS.VerifyCodeFixAsync(
                badSource,
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<object>()"),
                fixedSource);
        }

        [TestMethod]
        public async Task ExplicitZeroLengthArrayArgumentToParams_CSharpAsync()
        {
            const string source = """
                class C
                {
                    public C(params int[] values)
                    {
                    }

                    void Direct(params int[] values)
                    {
                    }

                    void Object(params object[] values)
                    {
                    }

                    void Jagged(params int[][] values)
                    {
                    }

                    int this[params int[] values] => 0;

                    void M()
                    {
                        Direct({|#0:new int[0]|});
                        Object({|#1:new int[0]|});
                        Jagged({|#2:new int[0]|});
                        _ = new C({|#3:new int[0]|});
                        _ = this[{|#4:new int[0]|}];
                        int[] values = {|#5:{}|};
                    }
                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(
                source,
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(3).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(4).WithArguments("Array.Empty<int>()"),
                VerifyCS.Diagnostic().WithLocation(5).WithArguments("Array.Empty<int>()"));
        }

        [TestMethod]
        [WorkItem(4665, "https://github.com/dotnet/roslyn-analyzers/issues/4665")]
        public async Task NoDiagnosticInExpressionTree_CSharpAsync()
        {
            const string source = """
                using System;
                using System.Linq.Expressions;

                class C
                {
                    Expression<Func<int[]>> f = () => new int[0];
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [WorkItem(4665, "https://github.com/dotnet/roslyn-analyzers/issues/4665")]
        public async Task NoDiagnosticInExpressionTree_VisualBasicAsync()
        {
            const string source = """
                Imports System
                Imports System.Linq.Expressions

                Class C
                    Private f1 As Expression(Of Func(Of Integer())) = Function() New Integer(-1) {}
                    Private f2 As Expression(Of Func(Of Integer())) = Function() New Integer() {}
                End Class
                """;
            await VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [TestMethod]
        [WorkItem(82484, "https://github.com/dotnet/roslyn/issues/82484")]
        public async Task NoDiagnosticForCollectionExpression_NonArrayTargetType_CSharpAsync()
        {
            const string source = """
                using System.Collections.Generic;

                class C
                {
                    List<string> l1 = ["a", "b", "c"];
                    List<int> l2 = [];
                    IEnumerable<int> l3 = [1, 2, 3];
                }
                """;
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp12,
                TestCode = source,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("[1, 2]")]
        [DataRow("[with(), 1, 2]")]
        [WorkItem(82484, "https://github.com/dotnet/roslyn/issues/82484")]
        public async Task NoDiagnosticForCollectionExpression_ParamsArrayConstructor_CSharpAsync(string collectionExpression)
        {
            string source = $$"""
                using System.Collections;
                using System.Collections.Generic;

                class Collection : IEnumerable<int>
                {
                    public Collection(params int[] values)
                    {
                    }

                    public void Add(int value)
                    {
                    }

                    public IEnumerator<int> GetEnumerator()
                    {
                        yield break;
                    }

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }

                class C
                {
                    Collection collection = {{collectionExpression}};
                }
                """;
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                TestCode = source,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(82484, "https://github.com/dotnet/roslyn/issues/82484")]
        public async Task NoDiagnosticForCollectionExpression_EmptyArrayTargetType_CSharpAsync()
        {

            const string source = """
                class C
                {
                    int[] arr = [];
                }
                """;
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp12,
                TestCode = source,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(82484, "https://github.com/dotnet/roslyn/issues/82484")]
        public async Task DiagnosticForZeroLengthArrayInsideCollectionExpression_CSharpAsync()
        {
            const string badSource = """

                using System;
                using System.Collections.Generic;

                class C
                {
                    List<int[]> l1 = [{|#0:new int[0]|}];
                }

                """;
            const string fixedSource = """

                using System;
                using System.Collections.Generic;

                class C
                {
                    List<int[]> l1 = [Array.Empty<int>()];
                }

                """;
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp12,
                TestCode = badSource,
                FixedCode = fixedSource,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic().WithLocation(0).WithArguments("Array.Empty<int>()"),
                },
            }.RunAsync(CancellationToken.None);
        }
    }
}
