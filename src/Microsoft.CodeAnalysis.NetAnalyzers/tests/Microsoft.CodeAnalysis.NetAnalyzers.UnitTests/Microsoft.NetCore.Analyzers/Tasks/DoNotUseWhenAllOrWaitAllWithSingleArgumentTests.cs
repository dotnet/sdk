// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Tasks.CSharpDoNotUseWhenAllOrWaitAllWithSingleArgument,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class DoNotUseWhenAllOrWaitAllWithSingleArgumentTests
    {
        [Fact]
        public async Task NoDiagnostic_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    using System.Threading.Tasks;

    class C
    {
        async Task M()
        {
            var t1 = CreateTask();
            var t2 = CreateTask();
            var objectTask1 = CreateObjectTask();
            var objectTask2 = CreateObjectTask();

            // Use WhenAll correctly with multiple tasks
            await Task.WhenAll(t1, t2);
            await Task.WhenAll(new[] { t1, t2 });
            await Task.WhenAll(CreateTask(), CreateTask());
            await Task.WhenAll(objectTask1, objectTask2);
            await Task.WhenAll(new[] { objectTask1, objectTask2 });
            await Task.WhenAll(CreateObjectTask(), CreateObjectTask());

            // This does not use the params array and should not trigger diagnostic
            await Task.WhenAll(new[] { t1 });        

            // Use WaitAll correctly with multiple tasks
            Task.WaitAll(t1, t2);
            Task.WaitAll(new[] { t1, t2 });
            Task.WaitAll(CreateTask(), CreateTask());
            Task.WaitAll(objectTask1, objectTask2);
            Task.WaitAll(new[] { objectTask1, objectTask2 });
            Task.WaitAll(CreateObjectTask(), CreateObjectTask());

            // Make sure a random Task.WaitAll that isn't System.Tasks.Task doens't trigger
            Test.Task.WaitAll(t1);
            await Test.Task.WhenAll(t1);
        }

        async Task WhenAllCall(params Task[] tasks)
        {
            // Should not trigger when taking an array from args
            await Task.WhenAll(tasks);
        }

        void WaitAllCall(params Task[] tasks)
        {
            // Should not trigger when taking an array from args
            Task.WaitAll(tasks);
        }

        Task CreateTask() => Task.CompletedTask;
        Task<object> CreateObjectTask() => Task.FromResult(new object());
    }
}

namespace Test
{
    public static class Task
    {
        public static async System.Threading.Tasks.Task WhenAll(params System.Threading.Tasks.Task[] tasks)
        {
            // Should not trigger when taking an array from args
            await System.Threading.Tasks.Task.WhenAll(tasks);
        }

        public static void WaitAll(params System.Threading.Tasks.Task[] tasks)
        {
            // Should not trigger when taking an array from args
            System.Threading.Tasks.Task.WaitAll(tasks);
        }
    }
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_WhenAll_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;

class C
{
    async Task M()
    {
        var t1 = CreateTask();
        var objectTask1 = CreateObjectTask();

        await {|CA2250:Task.WhenAll(t1)|};
        await {|CA2250:Task.WhenAll(CreateTask())|};
        Task whenResult = {|CA2250:Task.WhenAll(t1)|};
        DoSomethingWithTask(whenResult);

        await {|CA2250:Task.WhenAll(objectTask1)|};
        await {|CA2250:Task.WhenAll(CreateObjectTask())|};
        Task whenResult2 = {|CA2250:Task.WhenAll(objectTask1)|};
        DoSomethingWithTask(whenResult2);
    }

    void DoSomethingWithTask(Task task) 
    {
    }

    Task CreateTask() => Task.CompletedTask;
    Task<object> CreateObjectTask() => Task.FromResult(new object());
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_WaitAll_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;

class C
{
    async Task M()
    {
        var t1 = CreateTask();
        var objectTask1 = CreateObjectTask();

        {|CA2251:Task.WaitAll(t1)|};
        {|CA2251:Task.WaitAll(CreateTask())|};
        {|CA2251:Task.WaitAll(objectTask1)|};
        {|CA2251:Task.WaitAll(CreateObjectTask())|};
    }

    Task CreateTask() => Task.CompletedTask;
    Task<object> CreateObjectTask() => Task.FromResult(new object());
}
");
        }
    }
}
