// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotCreateTasksWithoutPassingATaskSchedulerAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Tasks.CSharpDoNotCreateTasksWithoutPassingATaskSchedulerFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotCreateTasksWithoutPassingATaskSchedulerAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Tasks.BasicDoNotCreateTasksWithoutPassingATaskSchedulerFixer>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class DoNotCreateTasksWithoutPassingATaskSchedulerTests
    {
        [Fact]
        public async Task NoDiagnosticCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

class C
{
    public void M(Task task, TaskFactory factory, TaskScheduler scheduler, TaskContinuationOptions continuationOptions, TaskCreationOptions creationOptions, CancellationToken ct)
    {
        task.ContinueWith(M2, scheduler);
        task.ContinueWith(M3, null, scheduler);
        task.ContinueWith(M2, ct, continuationOptions, scheduler);
        task.ContinueWith(M3, null, ct, continuationOptions, scheduler);

        factory.StartNew(M5, ct, creationOptions, scheduler);
        factory.StartNew(M6, null, ct, creationOptions, scheduler);
    }

    public void M2(Task task)
    {
    }

    public void M3(Task task, object obj)
    {
    }

    public void M4<TResult>(Task task, TResult obj)
    {
    }

    public void M5()
    {
    }

    public void M6(object obj)
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Class C
	Public Sub M(task As Task, factory As TaskFactory, scheduler As TaskScheduler, continuationOptions As TaskContinuationOptions, creationOptions As TaskCreationOptions, ct As CancellationToken)
		task.ContinueWith(AddressOf M2, scheduler)
		task.ContinueWith(AddressOf M3, Nothing, scheduler)
		task.ContinueWith(AddressOf M2, ct, continuationOptions, scheduler)
		task.ContinueWith(AddressOf M3, Nothing, ct, continuationOptions, scheduler)

		factory.StartNew(AddressOf M5, ct, creationOptions, scheduler)
		factory.StartNew(AddressOf M6, Nothing, ct, creationOptions, scheduler)
	End Sub

	Public Sub M2(task As Task)
	End Sub

	Public Sub M3(task As Task, obj As Object)
	End Sub

	Public Sub M4(Of TResult)(task As Task, obj As TResult)
	End Sub

	Public Sub M5()
	End Sub

	Public Sub M6(obj As Object)
	End Sub
End Class
");
        }

        [Fact]
        public async Task DiagnosticCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

class C
{
    public void M(Task task, TaskFactory factory, TaskScheduler scheduler, TaskContinuationOptions continuationOptions, TaskCreationOptions creationOptions, CancellationToken ct)
    {
        task.ContinueWith(M2);
        task.ContinueWith(M2, ct);
        task.ContinueWith(M2, continuationOptions);
        task.ContinueWith(M3, null);
        task.ContinueWith(M3, null, ct);
        task.ContinueWith(M3, null, continuationOptions);

        factory.StartNew(M5);
        factory.StartNew(M5, ct);
        factory.StartNew(M5, creationOptions);
        factory.StartNew(M6, null);
        factory.StartNew(M6, null, ct);
        factory.StartNew(M6, null, creationOptions);
    }

    public void M2(Task task)
    {
    }

    public void M3(Task task, object obj)
    {
    }

    public void M4<TResult>(Task task, TResult obj)
    {
    }

    public void M5()
    {
    }

    public void M6(object obj)
    {
    }
}
",
    // Test0.cs(10,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(10, 9),
    // Test0.cs(11,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(11, 9),
    // Test0.cs(12,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(12, 9),
    // Test0.cs(13,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(13, 9),
    // Test0.cs(14,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(14, 9),
    // Test0.cs(15,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(15, 9),
    // Test0.cs(17,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(17, 9),
    // Test0.cs(18,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(18, 9),
    // Test0.cs(19,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(19, 9),
    // Test0.cs(20,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(20, 9),
    // Test0.cs(21,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(21, 9),
    // Test0.cs(22,9): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetCSharpResultAt(22, 9));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Class C
	Public Sub M(task As Task, factory As TaskFactory, scheduler As TaskScheduler, continuationOptions As TaskContinuationOptions, creationOptions As TaskCreationOptions, ct As CancellationToken)
		task.ContinueWith(AddressOf M2)
		task.ContinueWith(AddressOf M2, ct)
		task.ContinueWith(AddressOf M2, continuationOptions)
		task.ContinueWith(AddressOf M3, Nothing)
		task.ContinueWith(AddressOf M3, Nothing, ct)
		task.ContinueWith(AddressOf M3, Nothing, continuationOptions)

		factory.StartNew(AddressOf M5)
		factory.StartNew(AddressOf M5, ct)
		factory.StartNew(AddressOf M5, creationOptions)
		factory.StartNew(AddressOf M6, Nothing)
		factory.StartNew(AddressOf M6, Nothing, ct)
		factory.StartNew(AddressOf M6, Nothing, creationOptions)
	End Sub

	Public Sub M2(task As Task)
	End Sub

	Public Sub M3(task As Task, obj As Object)
	End Sub

	Public Sub M4(Of TResult)(task As Task, obj As TResult)
	End Sub

	Public Sub M5()
	End Sub

	Public Sub M6(obj As Object)
	End Sub
End Class
",
    // Test0.vb(8,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(8, 3),
    // Test0.vb(9,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(9, 3),
    // Test0.vb(10,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(10, 3),
    // Test0.vb(11,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(11, 3),
    // Test0.vb(12,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(12, 3),
    // Test0.vb(13,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(13, 3),
    // Test0.vb(15,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(15, 3),
    // Test0.vb(16,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(16, 3),
    // Test0.vb(17,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(17, 3),
    // Test0.vb(18,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(18, 3),
    // Test0.vb(19,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(19, 3),
    // Test0.vb(20,3): warning RS0018: Do not create tasks without passing a TaskScheduler
    GetBasicResultAt(20, 3));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotCreateTasksWithoutPassingATaskSchedulerAnalyzer.Rule)
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotCreateTasksWithoutPassingATaskSchedulerAnalyzer.Rule)
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}