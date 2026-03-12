// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseNonCancelableTaskDelayWithWhenAny,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseNonCancelableTaskDelayWithWhenAny,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class DoNotUseNonCancelableTaskDelayWithWhenAnyTests
    {
        [Fact]
        public async Task NoDiagnostic_TaskDelayWithCancellationToken_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    async Task M(CancellationToken ct)
                    {
                        var task = CreateTask();

                        // Should not trigger - Task.Delay has CancellationToken
                        await Task.WhenAny(task, Task.Delay(1000, ct));
                        await Task.WhenAny(Task.Delay(1000, ct), task);
                        await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(1), ct), task);
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task NoDiagnostic_TaskDelayWithCancellationToken_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System
                Imports System.Threading
                Imports System.Threading.Tasks

                Class C
                    Async Function M(ct As CancellationToken) As Task
                        Dim task = CreateTask()

                        ' Should not trigger - Task.Delay has CancellationToken
                        Await Task.WhenAny(task, Task.Delay(1000, ct))
                        Await Task.WhenAny(Task.Delay(1000, ct), task)
                        Await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(1), ct), task)
                    End Function

                    Function CreateTask() As Task
                        Return Task.CompletedTask
                    End Function
                End Class
                """);
        }

        [Fact]
        public async Task NoDiagnostic_WhenAnyWithoutTaskDelay_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        var task1 = CreateTask();
                        var task2 = CreateTask();

                        // Should not trigger - no Task.Delay
                        await Task.WhenAny(task1, task2);
                        await Task.WhenAny(CreateTask(), CreateTask());
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_TaskDelayWithoutCancellationToken_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        var task = CreateTask();

                        // Should trigger - Task.Delay without CancellationToken
                        await Task.WhenAny(task, {|CA2027:Task.Delay(1000)|});
                        await Task.WhenAny({|CA2027:Task.Delay(1000)|}, task);
                        await Task.WhenAny({|CA2027:Task.Delay(TimeSpan.FromSeconds(1))|}, task);
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_TaskDelayWithoutCancellationToken_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System
                Imports System.Threading.Tasks

                Class C
                    Async Function M() As Task
                        Dim task = CreateTask()

                        ' Should trigger - Task.Delay without CancellationToken
                        Await Task.WhenAny(task, {|CA2027:Task.Delay(1000)|})
                        Await Task.WhenAny({|CA2027:Task.Delay(1000)|}, task)
                        Await Task.WhenAny({|CA2027:Task.Delay(TimeSpan.FromSeconds(1))|}, task)
                    End Function

                    Function CreateTask() As Task
                        Return Task.CompletedTask
                    End Function
                End Class
                """);
        }

        [Fact]
        public async Task Diagnostic_MultipleTaskDelays_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        var task = CreateTask();

                        // Should trigger on both Task.Delay calls
                        await Task.WhenAny(
                            {|CA2027:Task.Delay(1000)|}, 
                            {|CA2027:Task.Delay(2000)|}, 
                            task);
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_MixedTaskDelays_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    async Task M(CancellationToken ct)
                    {
                        var task = CreateTask();

                        // Should trigger only on Task.Delay without CancellationToken
                        await Task.WhenAny(
                            {|CA2027:Task.Delay(1000)|}, 
                            Task.Delay(2000, ct), 
                            task);
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_NestedInvocation_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        var task = CreateTask();

                        // Should trigger
                        var result = await Task.WhenAny(task, {|CA2027:Task.Delay(1000)|});
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task NoDiagnostic_NotSystemTask_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                namespace CustomTasks
                {
                    public static class Task
                    {
                        public static System.Threading.Tasks.Task Delay(int milliseconds) => 
                            System.Threading.Tasks.Task.CompletedTask;

                        public static System.Threading.Tasks.Task WhenAny(params System.Threading.Tasks.Task[] tasks) => 
                            System.Threading.Tasks.Task.CompletedTask;
                    }
                }

                class C
                {
                    async System.Threading.Tasks.Task M()
                    {
                        var task = CreateTask();

                        // Should not trigger - not System.Threading.Tasks.Task.WhenAny
                        await CustomTasks.Task.WhenAny(task, CustomTasks.Task.Delay(1000));
                    }

                    System.Threading.Tasks.Task CreateTask() => System.Threading.Tasks.Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_GenericTask_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        var task = CreateTask();

                        // Should trigger - works with Task<T> too
                        await Task.WhenAny(task, {|CA2027:Task.Delay(1000)|});
                    }

                    Task<int> CreateTask() => Task.FromResult(42);
                }
                """);
        }

        [Fact]
        public async Task NoDiagnostic_SingleTaskDelay_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        // Should not trigger - single task may be used to avoid exception
                        await Task.WhenAny(Task.Delay(1000));
                    }
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_ExplicitArray_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        var task = CreateTask();

                        // Should trigger - explicit array creation
                        await Task.WhenAny(new[] { task, {|CA2027:Task.Delay(1000)|} });
                    }

                    Task CreateTask() => Task.CompletedTask;
                }
                """);
        }

        [Fact]
        public async Task Diagnostic_CollectionExpression_CSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp12,
                TestCode = """
                    using System.Threading.Tasks;

                    class C
                    {
                        async Task M()
                        {
                            var task = CreateTask();

                            // Should trigger - collection expression
                            await Task.WhenAny([task, {|CA2027:Task.Delay(1000)|}]);
                        }

                        Task CreateTask() => Task.CompletedTask;
                    }
                    """,
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostic_EmptyCollectionExpression_CSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp12,
                TestCode = """
                    using System.Threading.Tasks;

                    class C
                    {
                        async Task M()
                        {
                            await Task.WhenAny();
                        }
                    }
                    """,
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostic_CollectionExpression_SingleTask_CSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp12,
                TestCode = """
                    using System.Threading.Tasks;

                    class C
                    {
                        async Task M()
                        {
                            await Task.WhenAny([Task.Delay(1000)]);
                        }
                    }
                    """,
            }.RunAsync();
        }
    }
}
