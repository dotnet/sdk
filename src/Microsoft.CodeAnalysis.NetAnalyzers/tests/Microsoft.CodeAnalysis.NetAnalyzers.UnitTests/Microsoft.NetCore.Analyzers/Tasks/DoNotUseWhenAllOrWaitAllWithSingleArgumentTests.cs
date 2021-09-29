// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseWhenAllOrWaitAllWithSingleArgument,
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseWhenAllOrWaitAllWithSingleArgumentFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseWhenAllOrWaitAllWithSingleArgument,
    Microsoft.NetCore.Analyzers.Tasks.DoNotUseWhenAllOrWaitAllWithSingleArgumentFixer>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class DoNotUseWhenAllOrWaitAllWithSingleArgumentTests
    {
        [Fact]
        public async Task NoDiagnostic_CSharpAsync()
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
        public async Task NoDiagnostic_VBAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks

Namespace N

    Class C
        Async Function M() As Task
            Dim t1 = CreateTask()
            Dim t2 = CreateTask()
            Dim objectTask1 = CreateObjectTask()
            Dim objectTask2 = CreateObjectTask()

            ' Use WhenAll correctly with multiple tasks
            Await Task.WhenAll(t1, t2)
            Await Task.WhenAll(New Task() {t1, t2})
            Await Task.WhenAll(CreateTask(), CreateTask())
            Await Task.WhenAll(objectTask1, objectTask2)
            Await Task.WhenAll(New Task() {objectTask1, objectTask2})
            Await Task.WhenAll(CreateObjectTask(), CreateObjectTask())

            ' This does not use the params array and should not trigger diagnostic
            Await Task.WhenAll(New Task() {t1})

            ' Use WaitAll correctly with multiple tasks
            Task.WaitAll(t1, t2)
            Task.WaitAll(New Task() {t1, t2})
            Task.WaitAll(CreateTask(), CreateTask())
            Task.WaitAll(objectTask1, objectTask2)
            Task.WaitAll(New Task() {objectTask1, objectTask2})
            Task.WaitAll(CreateObjectTask(), CreateObjectTask())

            ' Make sure a random Task.WaitAll that isn't System.Tasks.Task doens't trigger
            Test.Task.WaitAll(t1)
            Await Test.Task.WhenAll(t1)
        End Function

        Async Function WhenAllCall(ParamArray tasks As Task()) As Task
            ' Should not trigger when taking an array from args
            Await Task.WhenAll(tasks)
        End Function

        Sub WaitAllCall(ParamArray tasks As Task())
            ' Should not trigger when taking an array from args
            Task.WaitAll(tasks)
        End Sub

        Function CreateTask() As Task
            Return Task.CompletedTask
        End Function

        Function CreateObjectTask() As Task(Of Object)
            Return Task.FromResult(New Object())
        End Function
    End Class
End Namespace

Namespace Test
    Public Module Task
        Public Async Function WhenAll(ParamArray tasks As System.Threading.Tasks.Task()) As System.Threading.Tasks.Task
            ' Should not trigger when taking an array from args
            Await System.Threading.Tasks.Task.WhenAll(tasks)
        End Function

        Public Sub WaitAll(ParamArray tasks As System.Threading.Tasks.Task())
            ' Should not trigger when taking an array from args
            System.Threading.Tasks.Task.WaitAll(tasks)
        End Sub
    End Module
End Namespace
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_WhenAll_CSharpAsync()
        {
            var source = @"
using System.Threading.Tasks;

class C
{
    async Task M()
    {
        var t1 = CreateTask();
        var objectTask1 = CreateObjectTask();

        await {|CA1842:Task.WhenAll(t1)|};
        await {|CA1842:Task.WhenAll(CreateTask())|};

        // Test initializer
        var t1WhenAll = {|CA1842:Task.WhenAll(t1)|};
        DoSomethingWithTask(t1WhenAll);

        // Test assignment
        t1WhenAll = {|CA1842:Task.WhenAll(t1)|};
        DoSomethingWithTask(t1WhenAll);

        await {|CA1842:Task.WhenAll(objectTask1)|};
        await {|CA1842:Task.WhenAll(CreateObjectTask())|};
        var ot1WhenAll = {|CA1842:Task.WhenAll(objectTask1)|};
        DoSomethingWithTask(ot1WhenAll);
    }

    void DoSomethingWithTask(Task task) 
    {
    }

    Task CreateTask() => Task.CompletedTask;
    Task<object> CreateObjectTask() => Task.FromResult(new object());
}
";

            var fixedSource = @"
using System.Threading.Tasks;

class C
{
    async Task M()
    {
        var t1 = CreateTask();
        var objectTask1 = CreateObjectTask();

        await t1;
        await CreateTask();

        // Test initializer
        var t1WhenAll = {|CA1842:Task.WhenAll(t1)|};
        DoSomethingWithTask(t1WhenAll);

        // Test assignment
        t1WhenAll = {|CA1842:Task.WhenAll(t1)|};
        DoSomethingWithTask(t1WhenAll);

        await objectTask1;
        await CreateObjectTask();
        var ot1WhenAll = {|CA1842:Task.WhenAll(objectTask1)|};
        DoSomethingWithTask(ot1WhenAll);
    }

    void DoSomethingWithTask(Task task) 
    {
    }

    Task CreateTask() => Task.CompletedTask;
    Task<object> CreateObjectTask() => Task.FromResult(new object());
}
";
            var test = new VerifyCS.Test()
            {
                TestCode = source,
                FixedState =
                {
                    MarkupHandling = MarkupMode.Allow,
                    Sources = { fixedSource }
                }
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task Diagnostics_FixApplies_WhenAll_VBAsync()
        {
            var code = @"
Imports System.Threading.Tasks

Class C
    Async Function M() As Task
        Dim t1 = CreateTask()
        Dim objectTask1 = CreateObjectTask()

        Await {|CA1842:Task.WhenAll(t1)|}
        Await {|CA1842:Task.WhenAll(CreateTask())|}
        Dim t1WhenAll = {|CA1842:Task.WhenAll(t1)|}
        DoSomethingWithTask(t1WhenAll)

        Await {|CA1842:Task.WhenAll(objectTask1)|}
        Await {|CA1842:Task.WhenAll(CreateObjectTask())|}
        Dim o1WhenAll = {|CA1842:Task.WhenAll(objectTask1)|}
        DoSomethingWithTask(o1WhenAll)
    End Function

    Async Function DoSomethingWithTask(task As Task) As Task
        Await task
    End Function

    Function CreateTask() As Task
        Return Task.CompletedTask
    End Function

    Function CreateObjectTask() As Task(Of Object)
        Return Task.FromResult(New Object())
    End Function
End Class";

            var fixedCode = @"
Imports System.Threading.Tasks

Class C
    Async Function M() As Task
        Dim t1 = CreateTask()
        Dim objectTask1 = CreateObjectTask()

        Await t1
        Await CreateTask()
        Dim t1WhenAll = {|CA1842:Task.WhenAll(t1)|}
        DoSomethingWithTask(t1WhenAll)

        Await objectTask1
        Await CreateObjectTask()
        Dim o1WhenAll = {|CA1842:Task.WhenAll(objectTask1)|}
        DoSomethingWithTask(o1WhenAll)
    End Function

    Async Function DoSomethingWithTask(task As Task) As Task
        Await task
    End Function

    Function CreateTask() As Task
        Return Task.CompletedTask
    End Function

    Function CreateObjectTask() As Task(Of Object)
        Return Task.FromResult(New Object())
    End Function
End Class";

            var test = new VerifyVB.Test()
            {
                TestCode = code,
                FixedState =
                {
                    MarkupHandling = MarkupMode.Allow,
                    Sources = { fixedCode }
                }
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task Diagnostics_FixApplies_WaitAll_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Threading.Tasks;

class C
{
    async Task M()
    {
        var t1 = CreateTask();
        var objectTask1 = CreateObjectTask();

        {|CA1843:Task.WaitAll(t1)|};
        {|CA1843:Task.WaitAll(CreateTask())|};
        {|CA1843:Task.WaitAll(objectTask1)|};
        {|CA1843:Task.WaitAll(CreateObjectTask())|};
    }

    Task CreateTask() => Task.CompletedTask;
    Task<object> CreateObjectTask() => Task.FromResult(new object());
}
",
@"
using System.Threading.Tasks;

class C
{
    async Task M()
    {
        var t1 = CreateTask();
        var objectTask1 = CreateObjectTask();

        t1.Wait();
        CreateTask().Wait();
        objectTask1.Wait();
        CreateObjectTask().Wait();
    }

    Task CreateTask() => Task.CompletedTask;
    Task<object> CreateObjectTask() => Task.FromResult(new object());
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_WaitAll_VBAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.Threading.Tasks

Class C
    Sub M()
        Dim t1 = CreateTask()
        Dim objectTask1 = CreateObjectTask()

        {|CA1843:Task.WaitAll(t1)|}
        {|CA1843:Task.WaitAll(CreateTask())|}
        {|CA1843:Task.WaitAll(objectTask1)|}
        {|CA1843:Task.WaitAll(CreateObjectTask())|}
    End Sub

    Function CreateTask() As Task
        Return Task.CompletedTask
    End Function

    Function CreateObjectTask() As Task(Of Object)
        Return Task.FromResult(New Object())
    End Function
End Class",
@"
Imports System.Threading.Tasks

Class C
    Sub M()
        Dim t1 = CreateTask()
        Dim objectTask1 = CreateObjectTask()

        t1.Wait()
        CreateTask().Wait()
        objectTask1.Wait()
        CreateObjectTask().Wait()
    End Sub

    Function CreateTask() As Task
        Return Task.CompletedTask
    End Function

    Function CreateObjectTask() As Task(Of Object)
        Return Task.FromResult(New Object())
    End Function
End Class");
        }
    }
}
