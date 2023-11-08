// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DisposableFieldsShouldBeDisposed,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DisposableFieldsShouldBeDisposed,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.DisposeAnalysis)]
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
    public class DisposableFieldsShouldBeDisposedTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
           => VerifyCS.Diagnostic()
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
               .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);

        [Fact]
        public async Task DisposableAllocationInConstructor_AssignedDirectly_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocationInConstructor_AssignedDirectly_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,24): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetCSharpResultAt(13, 24, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,22): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetBasicResultAt(13, 22, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableAllocationInMethod_AssignedDirectly_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public void SomeMethod()
    {
        a = new A();
    }

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub SomeMethod()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocationInMethod_AssignedDirectly_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public void SomeMethod()
    {
        a = new A();
    }

    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub SomeMethod()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableAllocationInFieldInitializer_AssignedDirectly_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    private readonly A a2 = new A();
    
    public void Dispose()
    {
        a.Dispose();
        a2.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()
    Private ReadOnly a2 As New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
        a2.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocationInFieldInitializer_AssignedDirectly_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    private readonly A a2 = new A();
    
    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"),
            // Test0.cs(14,24): warning CA2213: 'B' contains field 'a2' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetCSharpResultAt(14, 24, "B", "a2", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()
    Private ReadOnly a2 As New A()

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"),
            // Test0.vb(14,22): warning CA2213: 'B' contains field 'a2' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetBasicResultAt(14, 22, "B", "a2", "A"));
        }

        [Fact]
        public async Task StaticField_NotDisposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private static A a = new A();
    private static readonly A a2 = new A();

    public void Dispose()
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private Shared a As A = New A()
    Private Shared ReadOnly a2 As New A()

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class");
        }

        [Fact, WorkItem(3042, "https://github.com/dotnet/roslyn-analyzers/issues/3042")]
        public async Task AsyncDisposableAllocationInConstructor_AssignedDirectly_Disposed_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.Threading.Tasks;

class A : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        return default(ValueTask);
    }
}

class B : IDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    public void Dispose()
    {
        a.DisposeAsync();
    }
}
"
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.Threading.Tasks

Class A
    Implements IAsyncDisposable

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Return Nothing
    End Function
End Class

Class B
    Implements IDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.DisposeAsync()
    End Sub
End Class"
            }.RunAsync();
        }

        [Fact, WorkItem(3042, "https://github.com/dotnet/roslyn-analyzers/issues/3042")]
        public async Task AsyncDisposableAllocationInConstructor_AssignedDirectly_NotDisposed_DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.Threading.Tasks;

class A : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        return default(ValueTask);
    }
}

class B : IDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    public void Dispose()
    {
    }
}
",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(15, 24, "B", "a", "A"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.Threading.Tasks

Class A
    Implements IAsyncDisposable

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Return Nothing
    End Function
End Class

Class B
    Implements IDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(16, 22, "B", "a", "A"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(3042, "https://github.com/dotnet/roslyn-analyzers/issues/3042")]
        public async Task DisposableAllocationInAsyncDisposableConstructor_AssignedDirectly_Disposed_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.Threading.Tasks;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IAsyncDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    public ValueTask DisposeAsync()
    {
        a.Dispose();
        return default(ValueTask);
    }
}
"
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.Threading.Tasks

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IAsyncDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        a.Dispose()
        Return Nothing
    End Function
End Class"
            }.RunAsync();
        }

        [Fact, WorkItem(3042, "https://github.com/dotnet/roslyn-analyzers/issues/3042")]
        public async Task DisposableAllocationInAsyncDisposableConstructor_AssignedDirectly_NotDisposed_DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.Threading.Tasks;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IAsyncDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    public ValueTask DisposeAsync()
    {
        return default(ValueTask);
    }
}
",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(14, 24, "B", "a", "A"),
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.Threading.Tasks

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IAsyncDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Return Nothing
    End Function
End Class",
                ExpectedDiagnostics =
                {
                    GetBasicResultAt(14, 22, "B", "a", "A"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(6075, "https://github.com/dotnet/roslyn-analyzers/issues/6075")]
        public async Task AsyncDisposableDisposedInExplicitAsyncDisposable_Disposed_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class FileStream2 : IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
}

public sealed class Test : IAsyncDisposable, IDisposable
{
    private readonly HttpClient client;
    private readonly FileStream2 stream;

    public Test()
    {
        client = new HttpClient();
        stream = new FileStream2();
    }

    public void Dispose()
    {
        client.Dispose();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await stream.DisposeAsync();
    }
}
"
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.IO
Imports System.Net.Http
Imports System.Threading.Tasks

class FileStream2 
	implements IAsyncDisposable
	public function DisposeAsync() as ValueTask implements IAsyncDisposable.DisposeAsync
		return nothing
	end function
end class

public class Test 
	implements IAsyncDisposable, IDisposable
	
    private readonly client as HttpClient
    private readonly stream as FileStream2

	public sub new()
        client = new HttpClient
        stream = new FileStream2
	end sub

	public sub Dispose() implements IDisposable.Dispose
        client.Dispose()
	end sub

	function DisposeAsync() as ValueTask implements IAsyncDisposable.DisposeAsync
		return stream.DisposeAsync()
	end function
end class
"
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.IO
Imports System.Net.Http
Imports System.Threading.Tasks

class FileStream2 
	implements IAsyncDisposable
	public function DisposeAsync() as ValueTask implements IAsyncDisposable.DisposeAsync
		return nothing
	end function
end class

public class Test 
	implements IAsyncDisposable, IDisposable
	
    private readonly client as HttpClient
    private readonly stream as FileStream2

	public sub new()
        client = new HttpClient
        stream = new FileStream2
	end sub

	public sub Dispose() implements IDisposable.Dispose
        client.Dispose()
	end sub

    rem arbitrary implementation name
	function DisposeOtherAsync() as ValueTask implements IAsyncDisposable.DisposeAsync
		return stream.DisposeAsync()
	end function
end class
"
            }.RunAsync();
        }

        [Fact, WorkItem(6075, "https://github.com/dotnet/roslyn-analyzers/issues/6075")]
        public async Task DisposableDisposedInExplicitDisposable_Disposed_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public sealed class Test : IDisposable
{
    private readonly HttpClient client;
    private readonly FileStream stream;

    public Test()
    {
        client = new HttpClient();
        stream = new FileStream(""C://some-path"", FileMode.CreateNew);
    }

    void IDisposable.Dispose()
    {
        client.Dispose();
        stream.Dispose();
    }
}
"
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.IO
Imports System.Net.Http
Imports System.Threading.Tasks

public class Test 
	implements IDisposable
	
    private readonly client as HttpClient
    private readonly stream as FileStream

	public sub new()
        client = new HttpClient
        stream = new FileStream(""C://some-path"", FileMode.CreateNew)
	end sub

	public sub Dispose() implements IDisposable.Dispose
        client.Dispose()
        stream.Dispose()
	end sub
end class
"
            }.RunAsync();
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughLocal_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public void SomeMethod()
    {
        var l = new A();
        a = l;
    }

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub SomeMethod()
        Dim l = New A()
        a = l
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughLocal_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public void SomeMethod()
    {
        var l = new A();
        a = l;
    }

    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub SomeMethod()
        Dim l = New A()
        a = l
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughParameter_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B(A p)
    {
        p = new A();
        a = p;
    }

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New(p As A)
        p = New A()
        a = p
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughParameter_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B(A p)
    {
        p = new A();
        a = p;
    }

    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New(p As A)
        p = New A()
        a = p
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableSymbolWithoutAllocation_AssignedThroughParameter_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B(A p)
    {
        a = p;
    }

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New(p As A)
        a = p
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableSymbolWithoutAllocation_AssignedThroughParameter_NotDisposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B(A p)
    {
        a = p;
    }

    public void Dispose()
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New(p As A)
        a = p
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(PointsToAnalysisKind.None)]
        [InlineData(PointsToAnalysisKind.PartialWithoutTrackingFieldsAndProperties)]
        [InlineData(PointsToAnalysisKind.Complete)]
        public async Task DisposableAllocation_AssignedThroughField_Disposed_NoDiagnosticAsync(PointsToAnalysisKind? pointsToAnalysisKind)
        {
            var editorConfig = pointsToAnalysisKind.HasValue ?
                $"dotnet_code_quality.CA2213.points_to_analysis_kind = {pointsToAnalysisKind}" :
                string.Empty;

            var csCode = @"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public void SomeMethod(C c)
    {
        c.Field = new A();
        a = c.Field;
    }

    public void Dispose()
    {
        a.Dispose();
    }
}

class C
{
    public A Field;
}
";
            var csTest = new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { csCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfig}") },
                }
            };

            await csTest.RunAsync();

            var vbCode = @"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub SomeMethod(c As C)
        c.Field = New A()
        a = c.Field
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class

Class C
    Public Field As A
End Class
";
            var vbTest = new VerifyVB.Test()
            {
                TestState =
                {
                    Sources = { vbCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfig}") },
                }
            };

            await vbTest.RunAsync();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(PointsToAnalysisKind.None)]
        [InlineData(PointsToAnalysisKind.PartialWithoutTrackingFieldsAndProperties)]
        [InlineData(PointsToAnalysisKind.Complete)]
        public async Task DisposableAllocation_AssignedThroughField_NotDisposed_DiagnosticAsync(PointsToAnalysisKind? pointsToAnalysisKind)
        {
            var editorConfig = pointsToAnalysisKind.HasValue ?
                $"dotnet_code_quality.CA2213.points_to_analysis_kind = {pointsToAnalysisKind}" :
                string.Empty;

            var csCode = @"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public void SomeMethod(C c)
    {
        c.Field = new A();
        a = c.Field;
    }

    public void Dispose()
    {
    }
}

class C
{
    public A Field;
}
";
            var csTest = new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { csCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfig}") },
                }
            };

            await csTest.RunAsync();

            var vbCode = @"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub SomeMethod(c As C)
        c.Field = New A()
        a = c.Field
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class C
    Public Field As A
End Class
";
            var vbTest = new VerifyVB.Test()
            {
                TestState =
                {
                    Sources = { vbCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfig}") },
                }
            };

            await vbTest.RunAsync();
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughInstanceInvocation_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B()
    {
        a = GetA();
    }

    private A GetA() => new A();

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New()
        a = GetA()
    End Sub

    Private Function GetA() As A
        Return New A()
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughInstanceInvocation_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B()
    {
        a = GetA();
    }

    private A GetA() => new A();

    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New()
        a = GetA()
    End Sub

    Private Function GetA() As A
        Return New A()
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughStaticCreateInvocation_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B()
    {
        a = Create();
    }

    private static A Create() => new A();

    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New()
        a = Create()
    End Sub

    Private Shared Function Create() As A
        Return New A()
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedThroughStaticCreateInvocation_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    public B()
    {
        a = Create();
    }

    private static A Create() => new A();

    public void Dispose()
    {
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Sub New()
        a = Create()
    End Sub

    Private Shared Function Create() As A
        Return New A()
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableAllocation_AssignedInDifferentType_DisposedInContainingType_NoDiagnosticAsync()
        {
            // We don't track disposable field assignments in different type.
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    public A a;
    public void Dispose()
    {
        a.Dispose();
    }
}

class WrapperB
{
    private B b;
    public void Create()
    {
        b.a = new A();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Public a As A

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class

Class WrapperB
    Dim b As B
    Public Sub Create()
        b.a = new A()
    End Sub
End Class
");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedInDifferentType_DisposedInDifferentNonDisposableType_NoDiagnosticAsync()
        {
            // We don't track disposable field assignments in different type.
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    public A a;
    public void Dispose()
    {
    }
}

class WrapperB
{
    private B b;

    public void Create()
    {
        b.a = new A();
    }

    public void Dispose()
    {
        b.a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Public a As A

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class WrapperB
    Dim b As B

    Public Sub Create()
        b.a = new A()
    End Sub

    Public Sub Dispose()
        b.a.Dispose()
    End Sub
End Class
");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedInDifferentType_NotDisposed_NoDiagnosticAsync()
        {
            // We don't track disposable field assignments in different type.
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    public A a;
    public void Dispose()
    {
    }
}

class Test
{
    public void M(B b)
    {
        b.a = new A();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Public a As A

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class Test
    Public Sub M(b As B)
        b.a = new A()
    End Sub
End Class
");
        }

        [Fact]
        public async Task DisposableOwnershipTransferSpecialCases_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Resources;

class A : IDisposable
{
    private Stream s;
    private TextReader tr;
    private TextWriter tw;
    private IResourceReader rr;

    public A(Stream s)
    {
        this.s = s;
    }

    public A(TextReader tr)
    {
        this.tr = tr;
    }

    public A(TextWriter tw)
    {
        this.tw = tw;
    }

    public A(IResourceReader rr)
    {
        this.rr = rr;
    }

    public void Dispose()
    {
        if (s != null)
        {
            s.Dispose();
        }

        if (tr != null)
        {
            tr.Dispose();
        }

        if (tw != null)
        {
            tw.Dispose();
        }

        if (rr != null)
        {
            rr.Dispose();
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Resources

Class A
    Implements IDisposable

    Private s As Stream
    Private tr As TextReader
    Private tw As TextWriter
    Private rr As IResourceReader

    Public Sub New(ByVal s As Stream)
        Me.s = s
    End Sub

    Public Sub New(ByVal tr As TextReader)
        Me.tr = tr
    End Sub

    Public Sub New(ByVal tw As TextWriter)
        Me.tw = tw
    End Sub

    Public Sub New(ByVal rr As IResourceReader)
        Me.rr = rr
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If s IsNot Nothing Then
            s.Dispose()
        End If

        If tr IsNot Nothing Then
            tr.Dispose()
        End If

        If tw IsNot Nothing Then
            tw.Dispose()
        End If

        If rr IsNot Nothing Then
            rr.Dispose()
        End If
    End Sub
End Class
");
        }

        [Fact]
        public async Task DisposableOwnershipTransferSpecialCases_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Resources;

class A : IDisposable
{
    private Stream s;
    private TextReader tr;
    private TextWriter tw;
    private IResourceReader rr;

    public A(Stream s)
    {
        this.s = s;
    }

    public A(TextReader tr)
    {
        this.tr = tr;
    }

    public A(TextWriter tw)
    {
        this.tw = tw;
    }

    public A(IResourceReader rr)
    {
        this.rr = rr;
    }

    public void Dispose()
    {
    }
}
",
            // Test0.cs(8,20): warning CA2213: 'A' contains field 's' that is of IDisposable type 'Stream', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetCSharpResultAt(8, 20, "A", "s", "Stream"),
            // Test0.cs(9,24): warning CA2213: 'A' contains field 'tr' that is of IDisposable type 'TextReader', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetCSharpResultAt(9, 24, "A", "tr", "TextReader"),
            // Test0.cs(10,24): warning CA2213: 'A' contains field 'tw' that is of IDisposable type 'TextWriter', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetCSharpResultAt(10, 24, "A", "tw", "TextWriter"),
            // Test0.cs(11,29): warning CA2213: 'A' contains field 'rr' that is of IDisposable type 'IResourceReader', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetCSharpResultAt(11, 29, "A", "rr", "IResourceReader"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Resources

Class A
    Implements IDisposable

    Private s As Stream
    Private tr As TextReader
    Private tw As TextWriter
    Private rr As IResourceReader

    Public Sub New(ByVal s As Stream)
        Me.s = s
    End Sub

    Public Sub New(ByVal tr As TextReader)
        Me.tr = tr
    End Sub

    Public Sub New(ByVal tw As TextWriter)
        Me.tw = tw
    End Sub

    Public Sub New(ByVal rr As IResourceReader)
        Me.rr = rr
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
",
            // Test0.vb(9,13): warning CA2213: 'A' contains field 's' that is of IDisposable type 'Stream', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetBasicResultAt(9, 13, "A", "s", "Stream"),
            // Test0.vb(10,13): warning CA2213: 'A' contains field 'tr' that is of IDisposable type 'TextReader', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetBasicResultAt(10, 13, "A", "tr", "TextReader"),
            // Test0.vb(11,13): warning CA2213: 'A' contains field 'tw' that is of IDisposable type 'TextWriter', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetBasicResultAt(11, 13, "A", "tw", "TextWriter"),
            // Test0.vb(12,13): warning CA2213: 'A' contains field 'rr' that is of IDisposable type 'IResourceReader', but it is never disposed. Change the Dispose method on 'A' to call Close or Dispose on this field.
            GetBasicResultAt(12, 13, "A", "rr", "IResourceReader"));
        }

        [Fact]
        public async Task DisposableAllocation_DisposedWithConditionalAccess_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        a?.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a?.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedToLocal_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        A l = a;
        l.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        Dim l = a
        l.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AssignedToLocal_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        A l = a;
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        Dim l = a
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact]
        public async Task DisposableAllocation_IfElseStatement_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    private A b;

    public B(bool flag)
    {
        A l = new A();
        if (flag)
        {
            a = l;
        }
        else
        {
            b = l;
        }
    }

    public void Dispose()
    {
        A l = null;
        if (a != null)
        {
            l = a;
        }
        else if (b != null)
        {
            l = b;
        }

        l.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Private b As A

    Public Sub New(ByVal flag As Boolean)
        Dim l As A = New A()
        If flag Then
            a = l
        Else
            b = l
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Dim l As A = Nothing
        If a IsNot Nothing Then
            l = a
        ElseIf b IsNot Nothing Then
            l = b
        End If
        l.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_IfElseStatement_NotDisposed_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a;
    private A b;

    public B(bool flag)
    {
        A l = new A();
        if (flag)
        {
            a = l;
        }
        else
        {
            b = l;
        }
    }

    public void Dispose()
    {
        A l = null;
        if (a != null)
        {
            l = a;
        }
        else if (b != null)
        {
            l = b;
        }
    }
}
",
            // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(13, 15, "B", "a", "A"),
            // Test0.cs(14,15): warning CA2213: 'B' contains field 'b' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(14, 15, "B", "b", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A
    Private b As A

    Public Sub New(ByVal flag As Boolean)
        Dim l As A = New A()
        If flag Then
            a = l
        Else
            b = l
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Dim l As A = Nothing
        If a IsNot Nothing Then
            l = a
        ElseIf b IsNot Nothing Then
            l = b
        End If
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"),
            // Test0.vb(14,13): warning CA2213: 'B' contains field 'b' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(14, 13, "B", "b", "A"));
        }

        [Fact]
        public async Task DisposableAllocation_EscapedField_NotDisposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        DisposeA(ref this.a);
    }

    private static void DisposeA(ref A a)
    {
        a.Dispose();
        a = null;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeA(a)
    End Sub

    Private Shared Sub DisposeA(ByRef a As A)
        a.Dispose()
        a = Nothing
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_OptimisticPointsToAnalysis_NoDiagnosticAsync()
        {
            // Invoking an instance method may likely invalidate all the instance field analysis state, i.e.
            // reference type fields might be re-assigned to point to different objects in the called method.
            // An optimistic points to analysis assumes that the points to values of instance fields don't change on invoking an instance method.
            // A pessimistic points to analysis resets all the instance state and assumes the instance field might point to any object, hence has unknown state.
            // For dispose analysis, we want to perform an optimistic points to analysis as we assume a disposable field is not likely to be re-assigned to a separate object in helper method invocations in Dispose.

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
    public void PerformSomeCleanup()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        a.PerformSomeCleanup();
        ClearMyState();
        a.Dispose();
    }

    private void ClearMyState()
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub PerformSomeCleanup()
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a.PerformSomeCleanup()
        ClearMyState()
        a.Dispose()
    End Sub

    Private Sub ClearMyState()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_OptimisticPointsToAnalysis_WithReturn_NoDiagnosticAsync()
        {
            // Invoking an instance method may likely invalidate all the instance field analysis state, i.e.
            // reference type fields might be re-assigned to point to different objects in the called method.
            // An optimistic points to analysis assumes that the points to values of instance fields don't change on invoking an instance method.
            // A pessimistic points to analysis resets all the instance state and assumes the instance field might point to any object, hence has unknown state.
            // For dispose analysis, we want to perform an optimistic points to analysis as we assume a disposable field is not likely to be re-assigned to a separate object in helper method invocations in Dispose.

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
    public void PerformSomeCleanup()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    public bool Disposed;
    
    public void Dispose()
    {
        if (Disposed)
        {
            return;
        }

        a.PerformSomeCleanup();
        ClearMyState();
        a.Dispose();
    }

    private void ClearMyState()
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub PerformSomeCleanup()
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()
    Public Disposed As Boolean

    Public Sub Dispose() Implements IDisposable.Dispose
        If Disposed Then
            Return
        End If

        a.PerformSomeCleanup()
        ClearMyState()
        a.Dispose()
    End Sub

    Private Sub ClearMyState()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_IfStatementInDispose_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

public class Test : IDisposable
{
    private readonly A a = new A();
    private bool cancelled;

    public void Dispose()
    {
        if (cancelled)
        {
            a.GetType();
        }

        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Public Class Test
    Implements IDisposable
    Private ReadOnly a As A = New A()
    Private cancelled As Boolean

    Public Sub Dispose() Implements IDisposable.Dispose
        If cancelled Then
            a.GetType()
        End If
        a.Dispose()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_DisposedinDisposeOverride_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

abstract class Base : IDisposable
{
    public virtual void Dispose()
    {
    }
}

class Derived : Base
{
    private readonly A a = new A();
    public override void Dispose()
    {
        base.Dispose();
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

MustInherit Class Base
    Implements IDisposable
    Public Overridable Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class Derived
    Inherits Base

    Private ReadOnly a As A = New A()

    Public Overrides Sub Dispose()
        MyBase.Dispose()
        a.Dispose()
    End Sub
End Class
");
        }

        [Fact]
        public async Task DisposableAllocation_DisposedWithDisposeBoolInvocation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }

    public void Dispose(bool disposed)
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        a.Dispose(true);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub Dispose(disposed As Boolean)
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose(True)
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_DisposedInsideDisposeBool_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }

    public void Dispose(bool disposed)
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        Dispose(true);
    }

    public void Dispose(bool disposed)
    {
        a.Dispose(disposed);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub Dispose(disposed As Boolean)
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
    End Sub

    Public Sub Dispose(disposed As Boolean)
        a.Dispose(disposed)
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_DisposedWithDisposeCloseInvocation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }

    public void Close()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        a.Close();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub Close()
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Close()
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_AllDisposedMethodsMixed_Disposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }

    public void Dispose(bool disposed)
    {
    }

    public void Close()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    private A a2 = new A();
    private A a3 = new A();
    
    public void Dispose()
    {
        a.Close();
    }
    
    public void Dispose(bool disposed)
    {
        a2.Dispose();
    }
    
    public void Close()
    {
        a3.Dispose(true);
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub Dispose(disposed As Boolean)
    End Sub

    Public Sub Close()
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()
    Private a2 As A = New A()
    Private a3 As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a.Close()
    End Sub

    Public Sub Dispose(disposed As Boolean)
        a2.Dispose()
    End Sub

    Public Sub Close()
        a3.Dispose(True)
    End Sub
End Class");
        }

        [Fact]
        public async Task DisposableAllocation_DisposedInsideDisposeClose_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }

    public void Close()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        Close();
    }

    public void Close()
    {
        a.Close();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub

    Public Sub Dispose(disposed As Boolean)
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
    End Sub

    Public Sub Dispose(disposed As Boolean)
        a.Dispose(disposed)
    End Sub
End Class");
        }

        [Fact]
        public async Task SystemThreadingTask_SpecialCase_NotDisposed_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading.Tasks;

public class A: IDisposable
{
    private readonly Task t;
    public A()
    {
        t = new Task(null);
    }
    public void Dispose()
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading.Tasks

Public Class A
    Implements IDisposable

    Private ReadOnly t As Task

    Public Sub New()
        t = New Task(Nothing)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class");
        }

        [Fact, WorkItem(1796, "https://github.com/dotnet/roslyn-analyzers/issues/1796")]
        public async Task DisposableAllocation_DisposedWithDisposeAsyncInvocation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading.Tasks;

class A : IDisposable
{
    public void Dispose() => DisposeAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        a.DisposeAsync();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading.Tasks

Class A
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeAsync()
    End Sub

    Public Function DisposeAsync() As Task
        Return Task.CompletedTask
    End Function
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        a.DisposeAsync()
    End Sub
End Class");
        }

        [Fact, WorkItem(1796, "https://github.com/dotnet/roslyn-analyzers/issues/1796")]
        public async Task DisposableAllocation_DisposedInsideDisposeCoreAsync_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading.Tasks;

abstract class A : IDisposable
{
    public void Dispose() => DisposeAsync();

    public Task DisposeAsync() => DisposeCoreAsync(true);

    protected abstract Task DisposeCoreAsync(bool initialized);
}

class A2 : A
{
    protected override Task DisposeCoreAsync(bool initialized)
    {
        return Task.CompletedTask;
    }
}

class B : A
{
    private A2 a = new A2();
    
    protected override Task DisposeCoreAsync(bool initialized)
    {
        return a.DisposeAsync();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading.Tasks

MustInherit Class A
    Implements IDisposable

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeAsync()
    End Sub

    Public Function DisposeAsync() As Task
        Return Task.CompletedTask
    End Function

    Protected MustOverride Function DisposeCoreAsync(initialized As Boolean) As Task
End Class

Class A2
    Inherits A

    Protected Overrides Function DisposeCoreAsync(initialized As Boolean) As Task
        Return Task.CompletedTask
    End Function
End Class

Class B
    Inherits A

    Private a As New A2()

    Protected Overrides Function DisposeCoreAsync(initialized As Boolean) As Task
        Return a.DisposeAsync()
    End Function
End Class");
        }

        [Fact, WorkItem(1813, "https://github.com/dotnet/roslyn-analyzers/issues/1813")]
        public async Task DisposableAllocation_DisposedInInvokedMethod_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        DisposeHelper();
    }

    private void DisposeHelper()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeHelper()
    End Sub

    Public Sub DisposeHelper()
        a.Dispose()
    End Sub
End Class");
        }

        [Fact, WorkItem(1813, "https://github.com/dotnet/roslyn-analyzers/issues/1813")]
        public async Task DisposableAllocation_NotDisposedInInvokedMethod_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private A a = new A();
    
    public void Dispose()
    {
        DisposeHelper();
    }

    private void DisposeHelper()
    {
    }
}
",
        // Test0.cs(13,15): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
        GetCSharpResultAt(13, 15, "B", "a", "A"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private a As A = New A()

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeHelper()
    End Sub

    Public Sub DisposeHelper()
    End Sub
End Class",
            // Test0.vb(13,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(13, 13, "B", "a", "A"));
        }

        [Fact, WorkItem(1813, "https://github.com/dotnet/roslyn-analyzers/issues/1813")]
        public async Task DisposableAllocation_DisposedInInvokedMethod_DisposableTypeInMetadata_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

class B : IDisposable
{
    private FileStream a = File.Open("""", FileMode.Create);

    public void Dispose()
    {
        DisposeHelper();
    }

    private void DisposeHelper()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO

Class B
    Implements IDisposable

    Private a As FileStream = File.Open("""", FileMode.Create)

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeHelper()
    End Sub

    Private Sub DisposeHelper()
        a.Dispose()
    End Sub
End Class
");
        }

        [Fact, WorkItem(1813, "https://github.com/dotnet/roslyn-analyzers/issues/1813")]
        public async Task DisposableAllocation_NotDisposedInInvokedMethod_DisposableTypeInMetadata_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

class B : IDisposable
{
    private FileStream a = File.Open("""", FileMode.Create);

    public void Dispose()
    {
        DisposeHelper();
    }

    private void DisposeHelper()
    {
    }
}
",
            // Test0.cs(7,24): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'FileStream', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(7, 24, "B", "a", "FileStream"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO

Class B
    Implements IDisposable

    Private a As FileStream = File.Open("""", FileMode.Create)

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeHelper()
    End Sub

    Private Sub DisposeHelper()
    End Sub
End Class
",
            // Test0.vb(8,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'FileStream', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(8, 13, "B", "a", "FileStream"));
        }

        [Fact, WorkItem(1813, "https://github.com/dotnet/roslyn-analyzers/issues/1813")]
        public async Task DisposableAllocation_DisposedInInvokedMethodMultipleLevelsDown_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

class B : IDisposable
{
    private FileStream a = File.Open("""", FileMode.Create);

    public void Dispose()
    {
        DisposeHelper();
    }

    private void DisposeHelper()
    {
        Helper.PerformDispose(a);
    }
}

static class Helper
{
    public static void PerformDispose(IDisposable a)
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO

Class B
    Implements IDisposable

    Private a As FileStream = File.Open("""", FileMode.Create)

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeHelper()
    End Sub

    Private Sub DisposeHelper()
        Helper.PerformDispose(a)
    End Sub
End Class

Public Module Helper
    Public Sub PerformDispose(ByVal a As IDisposable)
        a.Dispose()
    End Sub
End Module
");
        }

        [Fact, WorkItem(1813, "https://github.com/dotnet/roslyn-analyzers/issues/1813")]
        public async Task DisposableAllocation_NotDisposedInInvokedMethodMultipleLevelsDown_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

class B : IDisposable
{
    private FileStream a = File.Open("""", FileMode.Create);

    public void Dispose()
    {
        DisposeHelper();
    }

    private void DisposeHelper()
    {
        Helper.PerformDispose(a);
    }
}

static class Helper
{
    public static void PerformDispose(IDisposable a)
    {
    }
}
",
            // Test0.cs(7,24): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'FileStream', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetCSharpResultAt(7, 24, "B", "a", "FileStream"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO

Class B
    Implements IDisposable

    Private a As FileStream = File.Open("""", FileMode.Create)

    Public Sub Dispose() Implements IDisposable.Dispose
        DisposeHelper()
    End Sub

    Private Sub DisposeHelper()
        Helper.PerformDispose(a)
    End Sub
End Class

Public Module Helper
    Public Sub PerformDispose(ByVal a As IDisposable)
    End Sub
End Module
",
            // Test0.vb(8,13): warning CA2213: 'B' contains field 'a' that is of IDisposable type 'FileStream', but it is never disposed. Change the Dispose method on 'B' to call Close or Dispose on this field.
            GetBasicResultAt(8, 13, "B", "a", "FileStream"));
        }

        [Fact, WorkItem(2182, "https://github.com/dotnet/roslyn-analyzers/issues/2182")]
        public async Task DisposableAllocation_NonReadOnlyField_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public sealed class B : IDisposable
{
    public void Dispose()
    {
    }
}

public sealed class A : IDisposable
{
    private B _b;

    public A()
    {
        _b = new B();
    }

    public void Dispose()
    {
        if (_b == null)
        {
            return;
        }

        _b.Dispose();
        _b = null;
    }
}
");
        }

        [Fact, WorkItem(2306, "https://github.com/dotnet/roslyn-analyzers/issues/2306")]
        public async Task DisposableAllocationInConstructor_DisposedInGeneratedCodeFile_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private readonly A a;
    public B()
    {
        a = new A();
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("""", """")]
    public void Dispose()
    {
        a.Dispose();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class B
    Implements IDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    <System.CodeDom.Compiler.GeneratedCodeAttribute("""", """")> _
    Public Sub Dispose() Implements IDisposable.Dispose
        a.Dispose()
    End Sub
End Class");
        }

        [Fact, WorkItem(2182, "https://github.com/dotnet/roslyn-analyzers/issues/2182")]
        public async Task DisposableAllocation_FieldDisposedInOverriddenHelper_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class B : IDisposable
{
    private readonly object _gate = new object();

    public void Dispose()
    {
        lock (_gate)
        {
            DisposeUnderLock();
        }
    }

    protected virtual void DisposeUnderLock()
    {
    }
}

class C : B
{
    // Ensure this field is not flagged
    private readonly A _a = new A();

    protected override void DisposeUnderLock()
    {
        _a.Dispose();
        base.DisposeUnderLock();
    }
}
");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = BB")]
        [InlineData("dotnet_code_quality.CA2213.excluded_symbol_names = BB")]
        [InlineData("dotnet_code_quality.CA2213.excluded_symbol_names = B*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = BB")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOptionAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class BB : IDisposable
{
    private readonly A a;
    public BB()
    {
        a = new A();
    }

    public void Dispose()
    {
    }
}
"                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    // Test0.cs(13,24): warning CA2213: 'BB' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
                    GetCSharpResultAt(13, 24, "BB", "a", "A"));
            }

            await csharpTest.RunAsync();

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System

Class A
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class

Class BB
    Implements IDisposable

    Private ReadOnly a As A
    Sub New()
        a = New A()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length == 0)
            {
                basicTest.ExpectedDiagnostics.Add(
                    // Test0.vb(13,22): warning CA2213: 'BB' contains field 'a' that is of IDisposable type 'A', but it is never disposed. Change the Dispose method on 'B' to call Dispose or Close on this field.
                    GetBasicResultAt(13, 22, "BB", "a", "A"));
            }

            await basicTest.RunAsync();
        }

        [Fact, WorkItem(3042, "https://github.com/dotnet/roslyn-analyzers/issues/3042")]
        public async Task CloseAsyncDisposable_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.Threading.Tasks;

class A : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        return default(ValueTask);
    }

    public Task CloseAsync() => null;
}

class B : IAsyncDisposable
{
    private A a;

    public async ValueTask DisposeAsync()
    {
        if (a != null)
        {
            await a.CloseAsync().ConfigureAwait(false);
            a = null;
        }
    }
}
"
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
Imports System
Imports System.Threading.Tasks

Class A
    Implements IAsyncDisposable

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        Return Nothing
    End Function

    Public Function CloseAsync() As Task
        Return Nothing
    End Function
End Class

Class B
    Implements IAsyncDisposable

    Private a As A

    Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
        If a IsNot Nothing Then
            a.CloseAsync().ConfigureAwait(False)
            a = Nothing
        End If
    End Function
End Class"
            }.RunAsync();
        }

        [Fact]
        public async Task DisposeCoreAsync_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.Threading.Tasks;

class A : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        return default(ValueTask);
    }
}

// No diagnostic for DisposeCoreAsync.
class B : IAsyncDisposable
{
    private readonly object disposedValueLock = new object();
    private bool disposedValue;
    private readonly A a;

    public B() 
    {
        a = new A();
    }

    protected virtual async ValueTask DisposeCoreAsync()
    {
        lock (disposedValueLock)
        {
            if (disposedValue)
            {
                return;
            }

            disposedValue = true;
        }

        await a.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}

// No diagnostic for DisposeAsyncCore.
class C : IAsyncDisposable
{
    private readonly object disposedValueLock = new object();
    private bool disposedValue;
    private readonly A a;

    public C() 
    {
        a = new A();
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        lock (disposedValueLock)
        {
            if (disposedValue)
            {
                return;
            }

            disposedValue = true;
        }

        await a.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
"
            }.RunAsync();
        }

        [Fact, WorkItem(5099, "https://github.com/dotnet/roslyn-analyzers/issues/5099")]
        public async Task OwnDisposableButDoesNotOverrideDisposableMember_Dispose()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

class MyBase : IDisposable
{
    public virtual void Dispose()
    {
    }
}

class Sub : MyBase
{
}

class SubSub : Sub
{
    private readonly FileStream [|disposableField|] = new FileStream("""", FileMode.Create);
}");
        }

        [Fact, WorkItem(5099, "https://github.com/dotnet/roslyn-analyzers/issues/5099")]
        public async Task OwnDisposableButDoesNotOverrideDisposableMember_DisposeBool()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

class MyBase : IDisposable
{
    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

class Sub : MyBase
{
}

class SubSub : Sub
{
    private readonly FileStream [|disposableField|] = new FileStream("""", FileMode.Create);
}");
        }

        [Fact, WorkItem(5099, "https://github.com/dotnet/roslyn-analyzers/issues/5099")]
        public async Task OwnDisposableButDoesNotOverrideDisposableMember_DisposeAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAsyncInterfaces,
                TestCode = @"
using System;
using System.IO;
using System.Threading.Tasks;

class MyBase : IAsyncDisposable
{
    public virtual ValueTask DisposeAsync()
    {
        return default(ValueTask);
    }
}

class Sub : MyBase
{
}

class SubSub : Sub
{
    private readonly FileStream [|disposableField|] = new FileStream("""", FileMode.Create);
}"
            }.RunAsync();
        }

        [Fact]
        public async Task FieldDisposableThatDoNotRequireToBeDisposed()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading.Tasks;

public class BaseClass : IDisposable
{
    private readonly MemoryStream _stream = new MemoryStream();
    private readonly StringReader _stringReader = new StringReader(""something"");
    private readonly Task _task = new Task(() => {});

    public void Dispose()
    {
    }
}
");
        }

        [Fact, WorkItem(6172, "https://github.com/dotnet/roslyn-analyzers/issues/6172")]
        public async Task FieldIsDisposedInSubClassFollowingDisposePattern()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

public class BaseClass : IDisposable
{
    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class SubClass1 : BaseClass
{
    private readonly MemoryStream _stream;

    public SubClass1()
    {
        _stream = new MemoryStream();
    }

    protected override void Dispose(bool disposing)
    {
        _stream.Dispose();
    }
}

public class SubClass2 : BaseClass
{
    private readonly MemoryStream _stream;
    private bool _isDisposed = false;

    public SubClass2()
    {
        _stream = new MemoryStream();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            // free managed resources
            _stream.Dispose();
        }

        _isDisposed = true;
    }
}");
        }
    }
}
