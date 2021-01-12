// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.UseValueTasksCorrectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.UseValueTasksCorrectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class UseValueTasksCorrectlyTests
    {
        private static string CSBoilerplate(string s) => s +
            @"
            internal static class Helpers
            {
                public static System.Threading.Tasks.ValueTask ReturnsValueTask() => throw null;
                public static System.Threading.Tasks.ValueTask<T> ReturnsValueTaskOfT<T>() => throw null;
                public static System.Threading.Tasks.ValueTask<T> ReturnsValueTaskOfT<T>(T value) => throw null;
                public static System.Threading.Tasks.ValueTask<int> ReturnsValueTaskOfInt() => throw null;

                public static void AcceptsValueTask(System.Threading.Tasks.ValueTask vt) => throw null;
                public static void AcceptsValueTaskOfT<T>(System.Threading.Tasks.ValueTask<T> vt) => throw null;
                public static void AcceptsValueTaskOfInt(System.Threading.Tasks.ValueTask<int> vt) => throw null;

                public static void AcceptsTValue<TValue>(TValue t) => throw null;
            }
            ";

        private static string VBBoilerplate(string s) => s +
            @"
            Friend Module Helpers
                Function ReturnsValueTask() As System.Threading.Tasks.ValueTask
                    Throw New Exception()
                End Function

                Function ReturnsValueTaskOfT(Of T)() As System.Threading.Tasks.ValueTask(Of T)
                    Throw New Exception()
                End Function

                Function ReturnsValueTaskOfInt() As System.Threading.Tasks.ValueTask(Of Integer)
                    Throw New Exception()
                End Function

                Sub AcceptsValueTask(ByVal vt As System.Threading.Tasks.ValueTask)
                    Throw New Exception()
                End Sub

                Sub AcceptsValueTaskOfT(Of T)(ByVal vt As System.Threading.Tasks.ValueTask(Of T))
                    Throw New Exception()
                End Sub

                Sub AcceptsValueTaskOfInt(ByVal vt As System.Threading.Tasks.ValueTask(Of Integer))
                    Throw New Exception()
                End Sub

                Sub AcceptsTValue(Of TValue)(ByVal t As TValue)
                    Throw New Exception()
                End Sub
            End Module
            ";

        public static readonly IEnumerable<object[]> IsCompleteProperties =
            new object[][]
            {
                new object[] { "IsCompleted" },
                new object[] { "IsCompletedSuccessfully" },
                new object[] { "IsFaulted" },
                new object[] { "IsCanceled" }
            };

        public static IEnumerable<object[]> IsCompletedPropertiesAndResultAccess()
        {
            foreach (object[] o in IsCompleteProperties)
            {
                yield return new object[] { o[0], "Result" };
                yield return new object[] { o[0], "GetAwaiter().GetResult()" };
            }
        }

        #region No Diagnostic Tests

        [Fact]
        public async Task NoDiagnostics_AwaitMethodCallResults()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public async Task AwaitMethodCallResults()
                    {
                        await new ValueTask();
                        await default(ValueTask);
                        await Helpers.ReturnsValueTask();

                        await new ValueTask<string>(""hello"");
                        await new ValueTask<string>(Task.FromResult(""hello""));
                        string s = await Helpers.ReturnsValueTaskOfT<string>();

                        int i = await Helpers.ReturnsValueTaskOfInt();
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_AwaitMethodCallResults_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(VBBoilerplate(@"
                Imports System
                Imports System.Threading.Tasks

                Class C
                    Public Async Function AwaitMethodCallResults() As Task
                        Await New ValueTask()
                        Await Helpers.ReturnsValueTask()
                        Await New ValueTask(Of String)("", hello, "")
                        Await New ValueTask(Of String)(Task.FromResult("", hello, ""))
                        Dim s As String = Await Helpers.ReturnsValueTaskOfT(Of String)()
                        Dim i As Integer = Await Helpers.ReturnsValueTaskOfInt()
                    End Function
                End Class"));
        }

        [Fact]
        public async Task NoDiagnostics_Ternary()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public async Task Ternary()
                    {
                        await
                            (DateTime.Now > DateTime.Now ? Helpers.ReturnsValueTask() :
                             DateTime.Now > DateTime.Now ? new ValueTask() :
                             Helpers.ReturnsValueTask());

                        int i = await (DateTime.Now > DateTime.Now ?
                            Helpers.ReturnsValueTaskOfT<int>() :
                            Helpers.ReturnsValueTaskOfInt());
    
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_NullConditional()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public ValueTask NullConditional(C c) => c?.ReturnsValueTask() ?? default;
                    public async Task NullConditionalWithAwait(C c) => await (c?.ReturnsValueTask() ?? default);

                    public ValueTask<T> NullConditionalOfT<T>(C c) => c?.ReturnsValueTaskOfT<T>() ?? default;
                    public async Task<T> NullConditionalWithAwaitOfT<T>(C c) => await (c?.ReturnsValueTaskOfT<T>() ?? default);

                    public ValueTask NullConditionalWithSecondaryCall(C c) => c?.ReturnsValueTask() ?? Helpers.ReturnsValueTask();
                    public ValueTask<T> NullConditionalWithSecondaryCallOfT<T>(C c) => c?.ReturnsValueTaskOfT<T>() ?? Helpers.ReturnsValueTaskOfT<T>();

                    private ValueTask ReturnsValueTask() => default;
                    private ValueTask<T> ReturnsValueTaskOfT<T>() => default;
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_Switch()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        CSBoilerplate(@"
                            using System;
                            using System.Threading.Tasks;

                            class C
                            {
                                public async Task Switch()
                                {
                                    int i = 42;

                                    await (i switch
                                    {
                                        1 => Helpers.ReturnsValueTask(),
                                        2 => new ValueTask(),
                                        3 => default(ValueTask),
                                        4 => new ValueTask(Task.CompletedTask),
                                        _ => Helpers.ReturnsValueTask()
                                    });

                                    string s = await (i switch
                                    {
                                        1 => Helpers.ReturnsValueTaskOfT<string>(),
                                        2 => new ValueTask<string>(""hello""),
                                        3 => default(ValueTask<string>),
                                        4 => new ValueTask<string>(Task.FromResult(""hello"")),
                                        _ => Helpers.ReturnsValueTaskOfT<string>()
                                    });

                                    _ = await (i switch
                                    {
                                        1 => Helpers.ReturnsValueTaskOfInt(),
                                        2 => new ValueTask<int>(42),
                                        3 => default(ValueTask<int>),
                                        4 => new ValueTask<int>(Task.FromResult(42)),
                                        _ => Helpers.ReturnsValueTaskOfT<int>()
                                    });
                                }
                            }")
                    }
                },
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostics_AwaitConfiguredMethodCallResults()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public async Task AwaitConfiguredMethodCallResults()
                    {
                        await new ValueTask().ConfigureAwait(false);
                        await default(ValueTask).ConfigureAwait(true);
                        await Helpers.ReturnsValueTask().ConfigureAwait(false);

                        await new ValueTask<string>(""hello"").ConfigureAwait(true);
                        await new ValueTask<string>(Task.FromResult(""hello"")).ConfigureAwait(false);
                        string s = await Helpers.ReturnsValueTaskOfT<string>().ConfigureAwait(true);

                        int i = await Helpers.ReturnsValueTaskOfInt().ConfigureAwait(false);
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_PassAsArguments()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void PassAsArguments()
                    {
                        Helpers.AcceptsValueTask(new ValueTask());
                        Helpers.AcceptsValueTask(Helpers.ReturnsValueTask());

                        Helpers.AcceptsValueTaskOfT(new ValueTask<string>(""hello""));
                        Helpers.AcceptsValueTaskOfT(Helpers.ReturnsValueTaskOfT<string>());

                        Helpers.AcceptsValueTaskOfInt(new ValueTask<int>(42));
                        Helpers.AcceptsValueTaskOfInt(Helpers.ReturnsValueTaskOfInt());
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_Preserve()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void Preserve()
                    {
                        new ValueTask().Preserve();
                        default(ValueTask).Preserve();
                        Helpers.ReturnsValueTask().Preserve();

                        new ValueTask<string>(""hello"").Preserve();
                        new ValueTask<string>(Task.FromResult(""hello"")).Preserve();
                        Helpers.ReturnsValueTaskOfT<string>().Preserve();

                        Helpers.ReturnsValueTaskOfInt().Preserve();
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_AsTask()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void AsTask()
                    {
                        new ValueTask().AsTask();
                        default(ValueTask).AsTask();
                        Helpers.ReturnsValueTask().AsTask();

                        new ValueTask<string>(""hello"").AsTask();
                        new ValueTask<string>(Task.FromResult(""hello"")).AsTask();
                        Helpers.ReturnsValueTaskOfT<string>().AsTask();

                        Helpers.ReturnsValueTaskOfInt().AsTask();
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_ReturnValueTask()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public ValueTask ReturnValueTaskExpression() => Helpers.ReturnsValueTask();

                    public ValueTask<T> ReturnValueTaskOfTExpression<T>() => Helpers.ReturnsValueTaskOfT<T>();

                    public ValueTask<int> ReturnValueTaskOfIntExpression() => Helpers.ReturnsValueTaskOfInt();

                    public ValueTask<(string, string)> ReturnTupleWithConsts() => Helpers.ReturnsValueTaskOfT<(string, string)>(("""", """"));

                    public ValueTask<(string, string)> ReturnTupleWithCalls() => Helpers.ReturnsValueTaskOfT((SomeMethod(), SomeProp));

                    private string SomeProp => """";
                    private string SomeMethod() => """";

                    public ValueTask ReturnValueTask()
                    {
                        Console.WriteLine();
                        return Helpers.ReturnsValueTask();
                    }

                    public ValueTask<T> ReturnValueTaskOfT<T>()
                    {
                        Console.WriteLine();
                        return Helpers.ReturnsValueTaskOfT<T>();
                    }

                    public ValueTask<int> ReturnValueTaskOfInt()
                    {
                        Console.WriteLine();
                        return Helpers.ReturnsValueTaskOfInt();
                    }

                }"));
        }

        [Fact]
        public async Task NoDiagnostics_AssignVariableThenReturnValueTask()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public ValueTask ReturnValueTaskExpression() => Helpers.ReturnsValueTask();

                    public ValueTask<T> ReturnValueTaskOfTExpression<T>() => Helpers.ReturnsValueTaskOfT<T>();

                    public ValueTask<int> ReturnValueTaskOfIntExpression() => Helpers.ReturnsValueTaskOfInt();

                    public ValueTask ReturnValueTask()
                    {
                        var inst = this;
                        var vt = inst.ReturnValueTaskExpression();
                        return vt;
                    }

                    public ValueTask<T> ReturnValueTaskOfT<T>()
                    {
                        var inst = this;
                        var vt = inst.ReturnValueTaskOfTExpression<T>();
                        return vt;
                    }

                    public ValueTask<int> ReturnValueTaskOfInt()
                    {
                        var inst = this;
                        var vt = inst.ReturnValueTaskOfIntExpression();
                        return vt;
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_OutValueTask()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void OutValueTask(out ValueTask vt)
                    {
                        vt = Helpers.ReturnsValueTask();
                    }

                    public void OutValueTaskOfT<T>(out ValueTask<T> vt)
                    {
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                    }

                    public void OutValueTaskOfInt(out ValueTask<int> vt)
                    {
                        vt = Helpers.ReturnsValueTaskOfInt();
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_WorkBetweenStoreAndAwait()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public async Task WorkBetweenStoreAndAwait()
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        Console.WriteLine();
                        await vt;
                    }

                    public async Task WorkBetweenStoreAndAwaitOfT<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        Console.WriteLine();
                        await vt;
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task NoDiagnostics_AssertsBeforeDirectAccess(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsBeforeDirectAccess()
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        Debug.Assert(vt." + isProp + @");
                        vt.GetAwaiter().GetResult();
                    }

                    public void AssertsBeforeDirectAccessOfT<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        Debug.Assert(vt." + isProp + @");
                        vt.GetAwaiter().GetResult();
                    }

                    public void AssertsBeforeDirectAccessOfTResult<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        Debug.Assert(vt." + isProp + @");
                        T result = vt.Result;
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task NoDiagnostics_AssertsBeforeDirectAccessToAssignedLocal(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsBeforeDirectAccessToAssignedLocal()
                    {
                        ValueTask vt = default;
                        Console.WriteLine();
                        vt = Helpers.ReturnsValueTask();
                        Debug.Assert(vt." + isProp + @");
                        vt.GetAwaiter().GetResult();
                    }

                    public void AssertsBeforeDirectAccessToAssignedLocalOfT<T>()
                    {
                        ValueTask<T> vt = default;
                        Console.WriteLine();
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                        Debug.Assert(vt." + isProp + @");
                        vt.GetAwaiter().GetResult();
                    }

                    public void AssertsBeforeDirectAccessToAssignedLocalOfTResult<T>()
                    {
                        ValueTask<T> vt = default;
                        Console.WriteLine();
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                        Debug.Assert(vt." + isProp + @");
                        T result = vt.Result;
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task NoDiagnostics_AssertsEarlierInFlowBeforeDirectAccess(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsEarlierInFlowBeforeDirectAccess()
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        Debug.Assert(vt." + isProp + @");
                        if (DateTime.Now > DateTime.Now)
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                    }

                    public void AssertsEarlierInFlowBeforeDirectAccessOfT<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        Debug.Assert(vt." + isProp + @");
                        if (DateTime.Now > DateTime.Now)
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                    }

                    public void AssertsEarlierInFlowBeforeDirectAccessOfTResult<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        Debug.Assert(vt." + isProp + @");
                        if (DateTime.Now > DateTime.Now)
                        {
                            T result = vt.Result;
                        }
                        else
                        {
                            T result = vt.Result;
                        }
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task NoDiagnostics_FastPathForSyncCompleted(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task FastPathForSyncCompleted(CancellationToken cancellationToken)
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        if (vt." + isProp + @")
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            using (cancellationToken.Register(() => { }))
                            {
                                await vt.AsTask();
                            }
                        }
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task NoDiagnostics_FastPathForSyncCompletedInverted(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task FastPathForSyncCompletedInverted(CancellationToken cancellationToken)
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        if (!vt." + isProp + @")
                        {
                            using (cancellationToken.Register(() => { }))
                            {
                                await vt.AsTask();
                            }
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompletedPropertiesAndResultAccess))]
        public async Task NoDiagnostics_FastPathForSyncCompletedOfInt(string isProp, string resultMember)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task<int> FastPathForSyncCompletedOfInt(CancellationToken cancellationToken)
                    {
                        ValueTask<int> vt = Helpers.ReturnsValueTaskOfInt();
                        if (vt." + isProp + @")
                        {
                            return vt." + resultMember + @";
                        }

                        using (cancellationToken.Register(() => { }))
                        {
                            return await vt.AsTask();
                        }
                    }
                }"));
        }

        [Theory]
        [MemberData(nameof(IsCompletedPropertiesAndResultAccess))]
        public async Task NoDiagnostics_FastPathForSyncCompletedOfTInvertedNoElse(string isProp, string resultMember)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task<T> FastPathForSyncCompletedOfTInvertedNoElse<T>(CancellationToken cancellationToken)
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        if (!vt." + isProp + @")
                        {
                            using (cancellationToken.Register(() => { }))
                            {
                                return await vt.AsTask();
                            }
                        }

                        return vt." + resultMember + @";
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_CreateInTry_AwaitInFinally()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task CreateInTry_AwaitInFinally()
                    {
                        ValueTask t = default;
                        try
                        {
                            t = Helpers.ReturnsValueTask();
                        }
                        finally
                        {
                            await t;
                        }
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_CreateInTry_AwaitInCatch()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task CreateInTry_AwaitInCatch()
                    {
                        ValueTask t = default;
                        try
                        {
                            t = Helpers.ReturnsValueTask();
                        }
                        catch
                        {
                            await t;
                        }
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_ReturnOutOfPropertyExpressionBodied()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public ValueTask Prop => Helpers.ReturnsValueTask();
                    public ValueTask<string> PropString => Helpers.ReturnsValueTaskOfT<string>();
                    public ValueTask<int> PropInt => Helpers.ReturnsValueTaskOfInt();
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_ReturnOutOfPropertyStatements()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public ValueTask Prop
                    {
                        get
                        {
                            var vt = Helpers.ReturnsValueTask();
                            return vt;
                        }
                    }

                    public ValueTask<string> PropString
                    {
                        get
                        {
                            var vt = Helpers.ReturnsValueTaskOfT<string>();
                            return vt;
                        }
                    }

                    public ValueTask<int> PropInt
                    {
                        get
                        {
                            var vt = Helpers.ReturnsValueTaskOfInt();
                            return vt;
                        }
                    }
                }"));
        }

        [Fact]
        public async Task NoDiagnostics_CreateInTry_AwaitInCatchAndFinally()
        {
            // NOTE: This is a false negative.  Ideally the analyzer would catch
            // this case, but the validation across try/catch/finally is complicated.
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task CreateInTry_AwaitInCatchAndFinally()
                    {
                        ValueTask t = default;
                        try
                        {
                            t = Helpers.ReturnsValueTask();
                        }
                        catch
                        {
                            await t;
                        }
                        finally
                        {
                            await t;
                        }
                    }
                }"));
        }

        #endregion

        #region Diagnostic Tests

        [Fact]
        public async Task Diagnostics_DontConsume()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void DontConsume()
                    {
                        Helpers.ReturnsValueTask();
                        Helpers.ReturnsValueTaskOfT<string>();
                        Helpers.ReturnsValueTaskOfInt();

                        Helpers.ReturnsValueTask().ConfigureAwait(true);
                        Helpers.ReturnsValueTaskOfT<string>().ConfigureAwait(false);
                        Helpers.ReturnsValueTaskOfInt().ConfigureAwait(true);
                    }
                }"),
                GetCSharpResultAt(9, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(10, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(11, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(13, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(14, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(15, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule)
            );
        }

        [Fact]
        public async Task Diagnostics_DontConsume_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync(VBBoilerplate(@"
                Imports System
                Imports System.Threading.Tasks

                Class C
                    Public Sub DontConsume()
                        Helpers.ReturnsValueTask()
                        Helpers.ReturnsValueTaskOfT(Of String)()
                        Helpers.ReturnsValueTaskOfInt()
                        Helpers.ReturnsValueTask().ConfigureAwait(True)
                        Helpers.ReturnsValueTaskOfT(Of String)().ConfigureAwait(False)
                        Helpers.ReturnsValueTaskOfInt().ConfigureAwait(True)
                    End Sub
                End Class
                "),
                GetBasicResultAt(7, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetBasicResultAt(8, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetBasicResultAt(9, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetBasicResultAt(10, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetBasicResultAt(11, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetBasicResultAt(12, 25, UseValueTasksCorrectlyAnalyzer.UnconsumedRule)
            );
        }

        [Fact]
        public async Task NoDiagnostics_StoreLocalUnused()
        {
            // NOTE: This is a false negative.  Ideally the analyzer would catch
            // this case, but the validation to ensure the local is properly consumed
            // is challenging and we prefer false negatives over false positives.
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void StoreLocalUnused()
                    {
                        ValueTask vt0 = Helpers.ReturnsValueTask();
                        ValueTask<string> vt1 = Helpers.ReturnsValueTaskOfT<string>();
                        ValueTask<int> vt2 = Helpers.ReturnsValueTaskOfInt();
                    }
                }")
            );
        }

        [Fact]
        public async Task NoDiagnostics_StoreLocalUnused_Guarded()
        {
            // NOTE: This is a false negative.  Ideally the analyzer would catch
            // this case, but the validation to ensure the local is properly consumed
            // is challenging and we prefer false negatives over false positives.
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void StoreLocalUnused(bool guard)
                    {
                        if (guard) throw new Exception();
                        ValueTask vt0 = Helpers.ReturnsValueTask();
                        ValueTask<string> vt1 = Helpers.ReturnsValueTaskOfT<string>();
                        ValueTask<int> vt2 = Helpers.ReturnsValueTaskOfInt();
                    }
                }")
            );
        }

        [Fact]
        public async Task Diagnostics_PassAsGeneric()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void PassAsGeneric()
                    {
                        Helpers.AcceptsTValue(Helpers.ReturnsValueTask());
                        Helpers.AcceptsTValue(Helpers.ReturnsValueTaskOfT<string>());
                        Helpers.AcceptsTValue(Helpers.ReturnsValueTaskOfInt());
                    }
                }"),
                GetCSharpResultAt(9, 47, UseValueTasksCorrectlyAnalyzer.GeneralRule),
                GetCSharpResultAt(10, 47, UseValueTasksCorrectlyAnalyzer.GeneralRule),
                GetCSharpResultAt(11, 47, UseValueTasksCorrectlyAnalyzer.GeneralRule)
            );
        }

        [Fact]
        public async Task Diagnostics_DirectResultAccess()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void DirectResultAccess()
                    {
                        Helpers.ReturnsValueTask().GetAwaiter().GetResult();
                        Helpers.ReturnsValueTaskOfT<string>().GetAwaiter().GetResult();
                        Helpers.ReturnsValueTaskOfInt().GetAwaiter().GetResult();

                        _ = Helpers.ReturnsValueTaskOfT<string>().Result;
                        _ = Helpers.ReturnsValueTaskOfInt().Result;
                    }
                }"),
                GetCSharpResultAt(9, 25, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(10, 25, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(11, 25, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(13, 29, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(14, 29, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Fact]
        public async Task Diagnostics_UnguardedLocalResultAccess()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void UnguardedResultAccess()
                    {
                        ValueTask vt0 = Helpers.ReturnsValueTask();
                        vt0.GetAwaiter().GetResult();

                        ValueTask<string> vt1 = Helpers.ReturnsValueTaskOfT<string>();
                        vt1.GetAwaiter().GetResult();

                        ValueTask<int> vt2 = Helpers.ReturnsValueTaskOfInt();
                        vt2.GetAwaiter().GetResult();

                        ValueTask<string> vt3 = Helpers.ReturnsValueTaskOfT<string>();
                        _ = vt3.Result;
                    }
                }"),
                GetCSharpResultAt(9, 41, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(12, 49, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(15, 46, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(18, 49, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Fact]
        public async Task Diagnostics_UnguardedOutResultAccess()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void UnguardedResultAccess(out ValueTask vt0, out ValueTask<string> vt1, out ValueTask<int> vt2, out ValueTask<string> vt3)
                    {
                        vt0 = Helpers.ReturnsValueTask();
                        vt0.GetAwaiter().GetResult();

                        vt1 = Helpers.ReturnsValueTaskOfT<string>();
                        vt1.GetAwaiter().GetResult();

                        vt2 = Helpers.ReturnsValueTaskOfInt();
                        vt2.GetAwaiter().GetResult();

                        vt3 = Helpers.ReturnsValueTaskOfT<string>();
                        _ = vt3.Result;
                    }
                }"),
                GetCSharpResultAt(9, 31, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(12, 31, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(15, 31, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(18, 31, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Fact]
        public async Task Diagnostics_MultipleLocalAwaits()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public async Task MultipleAwaits()
                    {
                        ValueTask vt1 = Helpers.ReturnsValueTask();
                        await vt1;

                        ValueTask<string> vt2 = Helpers.ReturnsValueTaskOfT<string>();
                        await vt2;

                        ValueTask<int> vt3 = Helpers.ReturnsValueTaskOfInt();
                        await vt3;

                        await vt1;
                        await vt2;
                        await vt3;
                    }
                }"),
                GetCSharpResultAt(9, 41, UseValueTasksCorrectlyAnalyzer.DoubleConsumptionRule),
                GetCSharpResultAt(12, 49, UseValueTasksCorrectlyAnalyzer.DoubleConsumptionRule),
                GetCSharpResultAt(15, 46, UseValueTasksCorrectlyAnalyzer.DoubleConsumptionRule)
            );
        }

        [Fact]
        public async Task Diagnostics_StoreIntoFields()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    private ValueTask _vt;
                    private ValueTask<int> _vtOfInt;
                    private ValueTask<string> _vtOfString;

                    public void StoreIntoFields()
                    {
                        _vt = Helpers.ReturnsValueTask();
                        _vtOfString = Helpers.ReturnsValueTaskOfT<string>();
                        _vtOfInt = Helpers.ReturnsValueTaskOfInt();
                    }
                }"),
                GetCSharpResultAt(13, 31, UseValueTasksCorrectlyAnalyzer.GeneralRule),
                GetCSharpResultAt(14, 39, UseValueTasksCorrectlyAnalyzer.GeneralRule),
                GetCSharpResultAt(15, 36, UseValueTasksCorrectlyAnalyzer.GeneralRule)
            );
        }

        [Fact]
        public async Task Diagnostics_Discards()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading.Tasks;

                class C
                {
                    public void Discards()
                    {
                        _ = Helpers.ReturnsValueTask();
                        _ = Helpers.ReturnsValueTaskOfT<string>();
                        _ = Helpers.ReturnsValueTaskOfInt();

                        _ = Helpers.ReturnsValueTask().Preserve();
                        _ = Helpers.ReturnsValueTaskOfT<string>().Preserve();
                        _ = Helpers.ReturnsValueTaskOfInt().Preserve();

                        _ = Helpers.ReturnsValueTask().AsTask();
                        _ = Helpers.ReturnsValueTaskOfT<string>().AsTask();
                        _ = Helpers.ReturnsValueTaskOfInt().AsTask();
                    }
                }"),
                GetCSharpResultAt(9, 29, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(10, 29, UseValueTasksCorrectlyAnalyzer.UnconsumedRule),
                GetCSharpResultAt(11, 29, UseValueTasksCorrectlyAnalyzer.UnconsumedRule)
            );
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task Diagnostics_AssertsAfterDirectLocalAccess(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsAfterDirectAccess()
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        vt.GetAwaiter().GetResult();
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsAfterDirectAccessOfT<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        vt.GetAwaiter().GetResult();
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsAfterDirectAccessOfTResult<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        T result = vt.Result;
                        Debug.Assert(vt." + isProp + @");
                    }
                }"),
                GetCSharpResultAt(10, 40, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(17, 43, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(24, 43, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task Diagnostics_AssertsAfterDirectOutAccess(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsAfterDirectAccess(out ValueTask vt)
                    {
                        vt = Helpers.ReturnsValueTask();
                        vt.GetAwaiter().GetResult();
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsAfterDirectAccessOfT<T>(out ValueTask<T> vt)
                    {
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                        vt.GetAwaiter().GetResult();
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsAfterDirectAccessOfTResult<T>(out ValueTask<T> vt)
                    {
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                        T result = vt.Result;
                        Debug.Assert(vt." + isProp + @");
                    }
                }"),
                GetCSharpResultAt(10, 30, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(17, 30, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(24, 30, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task Diagnostics_AssertsLaterInFlowAfterDirectLocalAccess(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsLaterInFlowAfterDirectAccess()
                    {
                        ValueTask vt = Helpers.ReturnsValueTask();
                        if (DateTime.Now > DateTime.Now)
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsLaterInFlowAfterDirectAccessOfT<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        if (DateTime.Now > DateTime.Now)
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsLaterInFlowAfterDirectAccessOfTResult<T>()
                    {
                        ValueTask<T> vt = Helpers.ReturnsValueTaskOfT<T>();
                        if (DateTime.Now > DateTime.Now)
                        {
                            T result = vt.Result;
                        }
                        else
                        {
                            T result = vt.Result;
                        }
                        Debug.Assert(vt." + isProp + @");
                    }
                }"),
                GetCSharpResultAt(10, 40, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(24, 43, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(38, 43, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Theory]
        [MemberData(nameof(IsCompleteProperties))]
        public async Task Diagnostics_AssertsLaterInFlowAfterDirectOutAccess(string isProp)
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Diagnostics;
                using System.Threading.Tasks;

                class C
                {
                    public void AssertsLaterInFlowAfterDirectAccess(out ValueTask vt)
                    {
                        vt = Helpers.ReturnsValueTask();
                        if (DateTime.Now > DateTime.Now)
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsLaterInFlowAfterDirectAccessOfT<T>(out ValueTask<T> vt)
                    {
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                        if (DateTime.Now > DateTime.Now)
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            vt.GetAwaiter().GetResult();
                        }
                        Debug.Assert(vt." + isProp + @");
                    }

                    public void AssertsLaterInFlowAfterDirectAccessOfTResult<T>(out ValueTask<T> vt)
                    {
                        vt = Helpers.ReturnsValueTaskOfT<T>();
                        if (DateTime.Now > DateTime.Now)
                        {
                            T result = vt.Result;
                        }
                        else
                        {
                            T result = vt.Result;
                        }
                        Debug.Assert(vt." + isProp + @");
                    }
                }"),
                GetCSharpResultAt(10, 30, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(24, 30, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule),
                GetCSharpResultAt(38, 30, UseValueTasksCorrectlyAnalyzer.AccessingIncompleteResultRule)
            );
        }

        [Fact]
        public async Task Diagnostics_MultipleAwaitsAcrossDifferingConditions()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSBoilerplate(@"
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    public async Task MultipleAwaitsAcrossDifferingConditions<T>(bool condition1, bool condition2)
                    {
                        ValueTask vt1 = Helpers.ReturnsValueTask();
                        ValueTask<int> vt2 = Helpers.ReturnsValueTaskOfInt();
                        ValueTask<T> vt3 = Helpers.ReturnsValueTaskOfT<T>();
                        
                        if (condition1)
                        {
                            await vt1;
                        }
                        else
                        {
                            await vt2;
                        }

                        await vt1;
                        await vt3;

                        if (condition2)
                        {
                            await vt2;
                            await vt3;
                        }
                    }
                }"),
                VerifyCS.Diagnostic(UseValueTasksCorrectlyAnalyzer.DoubleConsumptionRule).WithSpan(10, 41, 10, 67),
                VerifyCS.Diagnostic(UseValueTasksCorrectlyAnalyzer.DoubleConsumptionRule).WithSpan(12, 44, 12, 76)
            );
        }

        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(rule).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(rule).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
