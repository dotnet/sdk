// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.InterfaceMethodsShouldBeCallableByChildTypesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.InterfaceMethodsShouldBeCallableByChildTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.InterfaceMethodsShouldBeCallableByChildTypesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.InterfaceMethodsShouldBeCallableByChildTypesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class InterfaceMethodsShouldBeCallableByChildTypesFixerTests
    {
        #region CSharp

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesCSharp_MakeProtected()
        {
            var code = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();
    string Name { get; }
}

public class ImplementsGeneralThree : IGeneral
{
    public ImplementsGeneralThree()
    {
    }

    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.[|DoSomething|]() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }

    int DoSomething() { Console.WriteLine(this); return 0; }

    internal string Name
    {
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
}";
            var expectedFixedCode = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();
    string Name { get; }
}

public class ImplementsGeneralThree : IGeneral
{
    public ImplementsGeneralThree()
    {
    }

    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.DoSomething() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }

    protected int DoSomething() { Console.WriteLine(this); return 0; }

    protected string Name
    {
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        [Fact, WorkItem(2616, "https://github.com/dotnet/roslyn/issues/2616")]
        public async Task CA1033SimpleDiagnosticCasesCSharp_ImplicitImpl()
        {
            var code = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();
    string Name { get; }
}

public class ImplementsGeneral  : IGeneral
{
    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.[|DoSomething|]() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
}";
            var expectedFixedCode = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();
    string Name { get; }
}

public class ImplementsGeneral  : IGeneral
{
    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    public object DoSomething() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    public string Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        [Fact, WorkItem(2616, "https://github.com/dotnet/roslyn/issues/2616")]
        public async Task CA1033SimpleDiagnosticCasesCSharp_Indexer()
        {
            var code = @"
using System;

public interface IGeneral
{
    int this[int item] { get; }
}

public class ImplementsGeneral  : IGeneral
{
    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return item;
        }
    }
}

public class ImplementsGeneralThree : IGeneral
{
    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return item;
        }
    }

    int this[int item]
    {
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }
}";
            var expectedFixedCode = @"
using System;

public interface IGeneral
{
    int this[int item] { get; }
}

public class ImplementsGeneral  : IGeneral
{
    public int this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }
}

public class ImplementsGeneralThree : IGeneral
{
    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }

    protected int this[int item]
    {
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }
}";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expectedFixedCode } },
                NumberOfFixAllIterations = 2,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesCSharp_MakeSealed()
        {
            var code = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();

    int this[int item] { get; }
    string Name { get; }
}

public class ImplementsGeneral  : IGeneral
{
    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.[|DoSomething|]() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return item;
        }
    }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
}
";
            var expectedFixedCode = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();

    int this[int item] { get; }
    string Name { get; }
}

public sealed class ImplementsGeneral  : IGeneral
{
    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.DoSomething() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expectedFixedCode } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix3,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesCSharp_MakeSealed_2()
        {
            var code = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();

    int this[int item] { get; }
    string Name { get; }
}

public class ImplementsGeneralThree : IGeneral
{
    public ImplementsGeneralThree()
    {
        DoSomething();
        int i = this[0];
        i = i + 1;
        string name = Name;
        Console.WriteLine(name);
    }

    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.[|DoSomething|]() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return item;
        }
    }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        [|get|]
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }

    int DoSomething() { Console.WriteLine(this); return 0; }
    internal string Name
    {
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
    int this[int item]
    {
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }
}
";
            var expectedFixedCode = @"
using System;

public interface IGeneral
{
    object DoSomething();
    void DoNothing();
    void JustThrow();

    int this[int item] { get; }
    string Name { get; }
}

public sealed class ImplementsGeneralThree : IGeneral
{
    public ImplementsGeneralThree()
    {
        DoSomething();
        int i = this[0];
        i = i + 1;
        string name = Name;
        Console.WriteLine(name);
    }

    // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
    object IGeneral.DoSomething() { return null; }

    void IGeneral.DoNothing() { }
    void IGeneral.JustThrow() { throw new Exception(); }

    int IGeneral.this[int item]
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }

    string IGeneral.Name
    {
        // [ExpectedWarning(""InterfaceMethodsShouldBeCallableByChildTypes"", ""DesignRules"")]
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }

    int DoSomething() { Console.WriteLine(this); return 0; }
    internal string Name
    {
        get
        {
            Console.WriteLine(this);
            return ""name"";
        }
    }
    int this[int item]
    {
        get
        {
            Console.WriteLine(this);
            return item;
        }
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expectedFixedCode } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix3,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesCSharp_Events()
        {
            var code = @"
using System;

public class NestedExplicitInterfaceImplementation
{
    public interface INestedGeneral
    {
        event EventHandler TheEvent;
    }

    public class ImplementsNestedGeneral : INestedGeneral
    {
        event EventHandler INestedGeneral.TheEvent
        {
            [|add|]
            { Console.WriteLine(this); }
            [|remove|]
            { Console.WriteLine(this); }
        }

        event EventHandler TheEvent
        {
            add
            { Console.WriteLine(this); }
            remove
            { Console.WriteLine(this); }
        }  
    }
}
";
            var expectedFixedCode = @"
using System;

public class NestedExplicitInterfaceImplementation
{
    public interface INestedGeneral
    {
        event EventHandler TheEvent;
    }

    public class ImplementsNestedGeneral : INestedGeneral
    {
        event EventHandler INestedGeneral.TheEvent
        {
            add
            { Console.WriteLine(this); }
            remove
            { Console.WriteLine(this); }
        }

        protected event EventHandler TheEvent
        {
            add
            { Console.WriteLine(this); }
            remove
            { Console.WriteLine(this); }
        }  
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        [WorkItem(2654, "https://github.com/dotnet/roslyn/issues/2654")]
        [Fact]
        public async Task CA1033SimpleDiagnosticCasesCSharp_Property()
        {
            var code = @"
using System;

public class NestedExplicitInterfaceImplementation
{
    public interface INestedGeneral
    {
        string Name { get; }
    }

    public class ImplementsNestedGeneral : INestedGeneral
    {
        string INestedGeneral.Name
        {
            [|get|]
            {
                Console.WriteLine(this);
                return ""name"";
            }
        }

        internal string {|CS0276:Name|}
        {
            // Cannot have accessibility on an accessor unless you have both get and set
            private get
            {
                Console.WriteLine(this);
                return ""name"";
            }
        }
    }
}
";
            var expectedFixedCode = @"
using System;

public class NestedExplicitInterfaceImplementation
{
    public interface INestedGeneral
    {
        string Name { get; }
    }

    public class ImplementsNestedGeneral : INestedGeneral
    {
        string INestedGeneral.Name
        {
            get
            {
                Console.WriteLine(this);
                return ""name"";
            }
        }

        protected string Name
        {
            // Cannot have accessibility on an accessor unless you have both get and set
            get
            {
                Console.WriteLine(this);
                return ""name"";
            }
        }
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        #endregion

        #region VisualBasic

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesBasic_MakeProtected()
        {
            var code = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Name() As String
End Interface

Public Class ImplementsGeneralThree
    Implements IGeneral
    Public Sub New()
    End Sub

    Private Function [|IGeneral_DoSomething|]() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        [|Get|]
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property

    Private Function DoSomething() As Integer
        Console.WriteLine(Me)
        Return 0
    End Function

    Friend ReadOnly Property Name() As String
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property
End Class
";
            var expectedFixedCode = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Name() As String
End Interface

Public Class ImplementsGeneralThree
    Implements IGeneral
    Public Sub New()
    End Sub

    Private Function IGeneral_DoSomething() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property

    Protected Function DoSomething() As Integer
        Console.WriteLine(Me)
        Return 0
    End Function

    Protected ReadOnly Property Name() As String
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property
End Class
";

            await VerifyVB.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        [Fact, WorkItem(2616, "https://github.com/dotnet/roslyn/issues/2616")]
        public async Task CA1033SimpleDiagnosticCasesBasic_ImplicitImpl()
        {
            var code = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Name() As String
End Interface

Public Class ImplementsGeneral
    Implements IGeneral

    Private Function [|IGeneral_DoSomething|]() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        [|Get|]
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property
End Class
";
            var expectedFixedCode = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Name() As String
End Interface

Public Class ImplementsGeneral
    Implements IGeneral

    Public Function DoSomething() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Public ReadOnly Property Name() As String Implements IGeneral.Name
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property
End Class
";

            await VerifyVB.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        [Fact, WorkItem(2650, "https://github.com/dotnet/roslyn/issues/2650")]
        public async Task CA1033SimpleDiagnosticCasesBasic_Indexer()
        {
            var code = @"
Imports System

Public Interface IGeneral
    Default ReadOnly Property Item(item_1 As Integer) As Integer
End Interface

Public Class ImplementsGeneral
    Implements IGeneral
    Private ReadOnly Property IGeneral_Item(item_1 As Integer) As Integer Implements IGeneral.Item
        [|Get|]
            Console.WriteLine(Me)
            Throw New NotImplementedException()
        End Get
    End Property
End Class

Public Class ImplementsGeneralThree
    Implements IGeneral

    Private ReadOnly Property IGeneral_Item(item As Integer) As Integer Implements IGeneral.Item
        [|Get|]
            Console.WriteLine(Me)
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property Item(item_1 As Integer) As Integer
        Get
            Console.WriteLine(Me)
            Throw New NotImplementedException()
        End Get
    End Property
End Class
";
            var expectedFixedCode = @"
Imports System

Public Interface IGeneral
    Default ReadOnly Property Item(item_1 As Integer) As Integer
End Interface

Public Class ImplementsGeneral
    Implements IGeneral

    Public ReadOnly Property Item(item_1 As Integer) As Integer Implements IGeneral.Item
        Get
            Console.WriteLine(Me)
            Throw New NotImplementedException()
        End Get
    End Property
End Class

Public Class ImplementsGeneralThree
    Implements IGeneral

    Private ReadOnly Property IGeneral_Item(item As Integer) As Integer Implements IGeneral.Item
        Get
            Console.WriteLine(Me)
            Throw New NotImplementedException()
        End Get
    End Property

    Protected ReadOnly Property Item(item_1 As Integer) As Integer
        Get
            Console.WriteLine(Me)
            Throw New NotImplementedException()
        End Get
    End Property
End Class
";

            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expectedFixedCode } },
                NumberOfFixAllIterations = 2,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesBasic_MakeSealed()
        {
            var code = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Item(item_1 As Integer) As Integer
    ReadOnly Property Name() As String
End Interface

Public Class ImplementsGeneral
    Implements IGeneral
    Private Function [|IGeneral_DoSomething|]() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Item(item As Integer) As Integer Implements IGeneral.Item
        [|Get|]
            Console.WriteLine(Me)
            Return item
        End Get
    End Property

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        [|Get|]
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property
End Class
";
            var expectedFixedCode = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Item(item_1 As Integer) As Integer
    ReadOnly Property Name() As String
End Interface

Public NotInheritable Class ImplementsGeneral
    Implements IGeneral
    Private Function IGeneral_DoSomething() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Item(item As Integer) As Integer Implements IGeneral.Item
        Get
            Console.WriteLine(Me)
            Return item
        End Get
    End Property

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property
End Class
";
            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expectedFixedCode } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix3,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesBasic_MakeSealed_2()
        {
            var code = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Item(item_1 As Integer) As Integer
    ReadOnly Property Name() As String
End Interface

Public Class ImplementsGeneralThree
    Implements IGeneral
    Public Sub New()
    End Sub

    Private Function [|IGeneral_DoSomething|]() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Item(item As Integer) As Integer Implements IGeneral.Item
        [|Get|]
            Console.WriteLine(Me)
            Return item
        End Get
    End Property

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        [|Get|]
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property

    ' private, this is no good
    Private Function DoSomething() As Integer
        Console.WriteLine(Me)
        Return 0
    End Function
    Friend ReadOnly Property Name() As String
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property

    Private ReadOnly Property Item(item_1 As Integer) As Integer
        Get
            Console.WriteLine(Me)
            Return item_1
        End Get
    End Property
End Class
";
            var expectedFixedCode = @"
Imports System

Public Interface IGeneral
    Function DoSomething() As Object
    ReadOnly Property Item(item_1 As Integer) As Integer
    ReadOnly Property Name() As String
End Interface

Public NotInheritable Class ImplementsGeneralThree
    Implements IGeneral
    Public Sub New()
    End Sub

    Private Function IGeneral_DoSomething() As Object Implements IGeneral.DoSomething
        Return Nothing
    End Function

    Private ReadOnly Property IGeneral_Item(item As Integer) As Integer Implements IGeneral.Item
        Get
            Console.WriteLine(Me)
            Return item
        End Get
    End Property

    Private ReadOnly Property IGeneral_Name() As String Implements IGeneral.Name
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property

    ' private, this is no good
    Private Function DoSomething() As Integer
        Console.WriteLine(Me)
        Return 0
    End Function
    Friend ReadOnly Property Name() As String
        Get
            Console.WriteLine(Me)
            Return ""name""
        End Get
    End Property

    Private ReadOnly Property Item(item_1 As Integer) As Integer
        Get
            Console.WriteLine(Me)
            Return item_1
        End Get
    End Property
End Class
";
            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { expectedFixedCode } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = MicrosoftCodeQualityAnalyzersResources.InterfaceMethodsShouldBeCallableByChildTypesFix3,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1033SimpleDiagnosticCasesBasic_Events()
        {
            var code = @"
Imports System

Public Class NestedExplicitInterfaceImplementation
    Public Interface INestedGeneral
        Event TheEvent As EventHandler
    End Interface

    Public Class ImplementsNestedGeneral
        Implements INestedGeneral
        Private Custom Event TheEvent_Impl As EventHandler Implements INestedGeneral.TheEvent
            [|AddHandler(ByVal value As EventHandler)|]
                Console.WriteLine(Me)
            End AddHandler
            [|RemoveHandler(ByVal value As EventHandler)|]
                Console.WriteLine(Me)
            End RemoveHandler
            RaiseEvent()
                Console.WriteLine(Me)
            End RaiseEvent
        End Event

        Private Custom Event TheEvent As EventHandler
            AddHandler(ByVal value As EventHandler)
                Console.WriteLine(Me)
            End AddHandler
            RemoveHandler(ByVal value As EventHandler)
                Console.WriteLine(Me)
            End RemoveHandler
            RaiseEvent()
                Console.WriteLine(Me)
            End RaiseEvent
        End Event
    End Class
End Class
";
            var expectedFixedCode = @"
Imports System

Public Class NestedExplicitInterfaceImplementation
    Public Interface INestedGeneral
        Event TheEvent As EventHandler
    End Interface

    Public Class ImplementsNestedGeneral
        Implements INestedGeneral
        Private Custom Event TheEvent_Impl As EventHandler Implements INestedGeneral.TheEvent
            AddHandler(ByVal value As EventHandler)
                Console.WriteLine(Me)
            End AddHandler
            RemoveHandler(ByVal value As EventHandler)
                Console.WriteLine(Me)
            End RemoveHandler
            RaiseEvent()
                Console.WriteLine(Me)
            End RaiseEvent
        End Event

        Protected Custom Event TheEvent As EventHandler
            AddHandler(ByVal value As EventHandler)
                Console.WriteLine(Me)
            End AddHandler
            RemoveHandler(ByVal value As EventHandler)
                Console.WriteLine(Me)
            End RemoveHandler
            RaiseEvent()
                Console.WriteLine(Me)
            End RaiseEvent
        End Event
    End Class
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        [WorkItem(2654, "https://github.com/dotnet/roslyn/issues/2654")]
        [Fact]
        public async Task CA1033SimpleDiagnosticCasesBasic_Property()
        {
            var code = @"
Imports System

Public Class NestedExplicitInterfaceImplementation
    Public Interface INestedGeneral
        ReadOnly Property Name() As String
    End Interface

    Public Class ImplementsNestedGeneral
        Implements INestedGeneral
        Private ReadOnly Property INestedGeneral_Name() As String Implements INestedGeneral.Name
            [|Get|]
                Console.WriteLine(Me)
                Return ""name""
            End Get
        End Property

        Friend Property Name() As String
            Private Get
                Console.WriteLine(Me)
                Return ""name""
            End Get
            Set(value As String)
                Console.WriteLine(Me)
            End Set
        End Property
    End Class
End Class
";
            var expectedFixedCode = @"
Imports System

Public Class NestedExplicitInterfaceImplementation
    Public Interface INestedGeneral
        ReadOnly Property Name() As String
    End Interface

    Public Class ImplementsNestedGeneral
        Implements INestedGeneral
        Private ReadOnly Property INestedGeneral_Name() As String Implements INestedGeneral.Name
            Get
                Console.WriteLine(Me)
                Return ""name""
            End Get
        End Property

        Protected Property Name() As String
            Get
                Console.WriteLine(Me)
                Return ""name""
            End Get
            Set(value As String)
                Console.WriteLine(Me)
            End Set
        End Property
    End Class
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, expectedFixedCode);
        }

        #endregion
    }
}