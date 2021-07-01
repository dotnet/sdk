// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotCreateTaskCompletionSourceWithWrongArguments,
    Microsoft.NetCore.Analyzers.Tasks.DoNotCreateTaskCompletionSourceWithWrongArgumentsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotCreateTaskCompletionSourceWithWrongArguments,
    Microsoft.NetCore.Analyzers.Tasks.DoNotCreateTaskCompletionSourceWithWrongArgumentsFixer>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    public class DoNotCreateTaskCompletionSourceWithWrongArgumentsTests
    {
        [Fact]
        public async Task NoDiagnostics_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;

class C
{
    void M()
    {
        // Use TCS correctly without options
        new TaskCompletionSource<int>(null);
        new TaskCompletionSource<int>(""hello"");
        new TaskCompletionSource<int>(new object());
        new TaskCompletionSource<int>(42);

        // Uses TaskCreationOptions correctly
        var validEnum = TaskCreationOptions.RunContinuationsAsynchronously;
        new TaskCompletionSource<int>(validEnum);
        new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        new TaskCompletionSource<int>(this.MyProperty);
        new TaskCompletionSource<int>(new object(), validEnum);
        new TaskCompletionSource<int>(new object(), TaskCreationOptions.RunContinuationsAsynchronously);
        new TaskCompletionSource<int>(new object(), this.MyProperty);
        new TaskCompletionSource<int>(null, validEnum);
        new TaskCompletionSource<int>(null, TaskCreationOptions.RunContinuationsAsynchronously);
        new TaskCompletionSource<int>(null, this.MyProperty);

        // We only pay attention to things of type TaskContinuationOptions
        new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously.ToString());
        new TaskCompletionSource<int>(TaskContinuationOptions.RunContinuationsAsynchronously.ToString());
        new TaskCompletionSource<int>((int)TaskCreationOptions.RunContinuationsAsynchronously);
        new TaskCompletionSource<int>((int)TaskContinuationOptions.RunContinuationsAsynchronously);

        // Explicit choice to store into an object; ignored
        object validObject = TaskCreationOptions.RunContinuationsAsynchronously;
        new TaskCompletionSource<int>(validObject);
        object invalidObject = TaskContinuationOptions.RunContinuationsAsynchronously;
        new TaskCompletionSource<int>(invalidObject);
    }
    TaskCreationOptions MyProperty { get; set; }
}

class Derived : TaskCompletionSource<int>
{
    public Derived() : base(TaskCreationOptions.RunContinuationsAsynchronously) { }
}
");
        }

        [Fact]
        public async Task NoDiagnostics_Basic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks

Class T
    Sub M()
        Dim tcs as TaskCompletionSource(Of Integer)

        ' Uses TaskCreationOptions correctly
        tcs = New TaskCompletionSource(Of Integer)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim validEnum As TaskCreationOptions = TaskCreationOptions.RunContinuationsAsynchronously
        tcs = New TaskCompletionSource(Of Integer)(validEnum)
        tcs = New TaskCompletionSource(Of Integer)(MyProperty)

        ' We only pay attention to things of type TaskContinuationOptions
        tcs = New TaskCompletionSource(Of Integer)(TaskCreationOptions.RunContinuationsAsynchronously.ToString())
        tcs = New TaskCompletionSource(Of Integer)(TaskContinuationOptions.RunContinuationsAsynchronously.ToString())
        tcs = New TaskCompletionSource(Of Integer)(CInt(TaskCreationOptions.RunContinuationsAsynchronously))
        tcs = New TaskCompletionSource(Of Integer)(CInt(TaskContinuationOptions.RunContinuationsAsynchronously))

        ' Explicit choice to store into an object; ignored
        Dim validObject As Object = TaskCreationOptions.RunContinuationsAsynchronously
        tcs = New TaskCompletionSource(Of Integer)(validObject)
        validObject = TaskContinuationOptions.RunContinuationsAsynchronously
        tcs = New TaskCompletionSource(Of Integer)(validObject)
    End Sub

    Private Property MyProperty As TaskCreationOptions
End Class

Class Derived
    Inherits TaskCompletionSource(Of Integer)

    Public Sub New()
        MyBase.New(TaskCreationOptions.RunContinuationsAsynchronously)
    End Sub
End Class
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"
using System.Threading.Tasks;

class C
{
    void M()
    {
        new TaskCompletionSource<int>([|TaskContinuationOptions.None|]);
        new TaskCompletionSource<int>([|TaskContinuationOptions.RunContinuationsAsynchronously|]);
        var tcs = new TaskCompletionSource<int>([|TaskContinuationOptions.AttachedToParent|]);
    }
    TaskContinuationOptions MyProperty { get; set; }
}

class Derived : TaskCompletionSource<int>
{
    public Derived() : base([|TaskContinuationOptions.RunContinuationsAsynchronously|]) { }
}
",
@"
using System.Threading.Tasks;

class C
{
    void M()
    {
        new TaskCompletionSource<int>(TaskCreationOptions.None);
        new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.AttachedToParent);
    }
    TaskContinuationOptions MyProperty { get; set; }
}

class Derived : TaskCompletionSource<int>
{
    public Derived() : base(TaskCreationOptions.RunContinuationsAsynchronously) { }
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_CSharp_NonGeneric()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"
using System.Threading.Tasks;

namespace System.Threading.Tasks
{
    public class TaskCompletionSource // added in .NET 5
    {
        public TaskCompletionSource(TaskCreationOptions options) { }
        public TaskCompletionSource(object state) { }
    }
}

class C
{
    void M()
    {
        new TaskCompletionSource([|TaskContinuationOptions.None|]);
        new TaskCompletionSource([|TaskContinuationOptions.RunContinuationsAsynchronously|]);
        var tcs = new TaskCompletionSource([|TaskContinuationOptions.AttachedToParent|]);
    }
    TaskContinuationOptions MyProperty { get; set; }
}

class Derived : TaskCompletionSource
{
    public Derived() : base([|TaskContinuationOptions.RunContinuationsAsynchronously|]) { }
}
",
@"
using System.Threading.Tasks;

namespace System.Threading.Tasks
{
    public class TaskCompletionSource // added in .NET 5
    {
        public TaskCompletionSource(TaskCreationOptions options) { }
        public TaskCompletionSource(object state) { }
    }
}

class C
{
    void M()
    {
        new TaskCompletionSource(TaskCreationOptions.None);
        new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs = new TaskCompletionSource(TaskCreationOptions.AttachedToParent);
    }
    TaskContinuationOptions MyProperty { get; set; }
}

class Derived : TaskCompletionSource
{
    public Derived() : base(TaskCreationOptions.RunContinuationsAsynchronously) { }
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_Basic()
        {
            await VerifyVB.VerifyCodeFixAsync(
@"
Imports System.Threading.Tasks

Class C
    Private Sub M()
        Dim tcs = New TaskCompletionSource(Of Integer)([|TaskContinuationOptions.None|])
        tcs = New TaskCompletionSource(Of Integer)([|TaskContinuationOptions.RunContinuationsAsynchronously|])
        tcs = New TaskCompletionSource(Of Integer)([|TaskContinuationOptions.AttachedToParent|])
    End Sub

    Private Property MyProperty As TaskContinuationOptions
End Class

Class Derived
    Inherits TaskCompletionSource(Of Integer)

    Public Sub New()
        MyBase.New([|TaskContinuationOptions.RunContinuationsAsynchronously|])
    End Sub
End Class
",
@"
Imports System.Threading.Tasks

Class C
    Private Sub M()
        Dim tcs = New TaskCompletionSource(Of Integer)(TaskCreationOptions.None)
        tcs = New TaskCompletionSource(Of Integer)(TaskCreationOptions.RunContinuationsAsynchronously)
        tcs = New TaskCompletionSource(Of Integer)(TaskCreationOptions.AttachedToParent)
    End Sub

    Private Property MyProperty As TaskContinuationOptions
End Class

Class Derived
    Inherits TaskCompletionSource(Of Integer)

    Public Sub New()
        MyBase.New(TaskCreationOptions.RunContinuationsAsynchronously)
    End Sub
End Class
");
        }

        [Fact]
        public async Task Diagnostics_FixDoesntApply_CSharp()
        {
            const string Input = @"
using System.Threading.Tasks;

class C
{
    void M()
    {
        // Option not available on TaskCreationOptions
        new TaskCompletionSource<int>([|TaskContinuationOptions.OnlyOnFaulted|]);

        // Invoked with a local
        var invalidEnum1 = TaskContinuationOptions.RunContinuationsAsynchronously;
        new TaskCompletionSource<int>([|invalidEnum1|]);
        TaskContinuationOptions invalidEnum2 = TaskContinuationOptions.RunContinuationsAsynchronously;
        new TaskCompletionSource<int>([|invalidEnum2|]);

        // Invoked with a property
        new TaskCompletionSource<int>([|MyProperty|]);
    }
    TaskContinuationOptions MyProperty { get; set; }
}
";
            await VerifyCS.VerifyCodeFixAsync(Input, Input);
        }
    }
}
