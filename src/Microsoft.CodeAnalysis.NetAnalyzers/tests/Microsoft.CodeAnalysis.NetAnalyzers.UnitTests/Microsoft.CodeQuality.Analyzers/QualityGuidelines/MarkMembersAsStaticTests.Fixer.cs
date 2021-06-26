// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.MarkMembersAsStaticAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpMarkMembersAsStaticFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.MarkMembersAsStaticAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicMarkMembersAsStaticFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class MarkMembersAsStaticFixerTests
    {
        [Fact]
        public async Task TestCSharp_SimpleMembers_NoReferences()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class MembersTests
{
    internal static int s_field;
    public const int Zero = 0;

    public int [|Method1|](string name)
    {
        return name.Length;
    }

    public void [|Method2|]() { }

    public void [|Method3|]()
    {
        s_field = 4;
    }

    public int [|Method4|]()
    {
        return Zero;
    }

    public int [|Property|]
    {
        get { return 5; }
    }

    public int [|Property2|]
    {
        set { s_field = value; }
    }

    public int [|MyProperty|]
    {
        get { return 10; }
        set { System.Console.WriteLine(value); }
    }

    public event System.EventHandler<System.EventArgs> [|CustomEvent|] { add {} remove {} }
}",
@"
public class MembersTests
{
    internal static int s_field;
    public const int Zero = 0;

    public static int Method1(string name)
    {
        return name.Length;
    }

    public static void Method2() { }

    public static void Method3()
    {
        s_field = 4;
    }

    public static int Method4()
    {
        return Zero;
    }

    public static int Property
    {
        get { return 5; }
    }

    public static int Property2
    {
        set { s_field = value; }
    }

    public static int MyProperty
    {
        get { return 10; }
        set { System.Console.WriteLine(value); }
    }

    public static event System.EventHandler<System.EventArgs> CustomEvent { add {} remove {} }
}");
        }

        [Fact]
        public async Task TestBasic_SimpleMembers_NoReferences()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Public Class MembersTests
    Shared s_field As Integer
    Public Const Zero As Integer = 0

    Public Function [|Method1|](name As String) As Integer
        Return name.Length
    End Function

    Public Sub [|Method2|]()
    End Sub

    Public Sub [|Method3|]()
        s_field = 4
    End Sub

    Public Function [|Method4|]() As Integer
        Return Zero
    End Function

    Public Property [|MyProperty|] As Integer
        Get
            Return 10
        End Get
        Set
            System.Console.WriteLine(Value)
        End Set
    End Property

    Public Custom Event [|CustomEvent|] As EventHandler(Of EventArgs)
        AddHandler(value As EventHandler(Of EventArgs))
        End AddHandler
        RemoveHandler(value As EventHandler(Of EventArgs))
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event
End Class",
@"
Imports System
Public Class MembersTests
    Shared s_field As Integer
    Public Const Zero As Integer = 0

    Public Shared Function Method1(name As String) As Integer
        Return name.Length
    End Function

    Public Shared Sub Method2()
    End Sub

    Public Shared Sub Method3()
        s_field = 4
    End Sub

    Public Shared Function Method4() As Integer
        Return Zero
    End Function

    Public Shared Property MyProperty As Integer
        Get
            Return 10
        End Get
        Set
            System.Console.WriteLine(Value)
        End Set
    End Property

    Public Shared Custom Event CustomEvent As EventHandler(Of EventArgs)
        AddHandler(value As EventHandler(Of EventArgs))
        End AddHandler
        RemoveHandler(value As EventHandler(Of EventArgs))
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event
End Class");
        }

        [Fact]
        public async Task TestCSharp_ReferencesInSameType_MemberReferences()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class C
{
    private C fieldC;
    private C PropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }

    public void M2(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = M1,
            m2 = paramC.M1,
            m3 = localC.M1,
            m4 = fieldC.M1,
            m5 = PropertyC.M1,
            m6 = fieldC.PropertyC.M1,
            m7 = this.M1;
    }
}",
@"
using System;

public class C
{
    private C fieldC;
    private C PropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }

    public void M2(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = M1,
            m2 = M1,
            m3 = M1,
            m4 = M1,
            m5 = M1,
            m6 = M1,
            m7 = M1;
    }
}");
        }

        [Fact]
        public async Task TestBasic_ReferencesInSameType_MemberReferences()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Class C
    Private fieldC As C
    Private Property PropertyC As C

    Public Function [|M1|]() As Integer
        Return 0
    End Function

    Public Sub M2(paramC As C)
        Dim localC = fieldC
        Dim m As Func(Of Integer) = AddressOf M1,
            m2 As Func(Of Integer) = AddressOf paramC.M1,
            m3 As Func(Of Integer) = AddressOf localC.M1,
            m4 As Func(Of Integer) = AddressOf fieldC.M1,
            m5 As Func(Of Integer) = AddressOf PropertyC.M1,
            m6 As Func(Of Integer) = AddressOf fieldC.PropertyC.M1,
            m7 As Func(Of Integer) = AddressOf Me.M1
    End Sub
End Class",
@"
Imports System

Public Class C
    Private fieldC As C
    Private Property PropertyC As C

    Public Shared Function M1() As Integer
        Return 0
    End Function

    Public Sub M2(paramC As C)
        Dim localC = fieldC
        Dim m As Func(Of Integer) = AddressOf M1,
            m2 As Func(Of Integer) = AddressOf M1,
            m3 As Func(Of Integer) = AddressOf M1,
            m4 As Func(Of Integer) = AddressOf M1,
            m5 As Func(Of Integer) = AddressOf M1,
            m6 As Func(Of Integer) = AddressOf M1,
            m7 As Func(Of Integer) = AddressOf M1
    End Sub
End Class");
        }

        [Fact]
        public async Task TestCSharp_ReferencesInSameType_Invocations()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }
    private static C staticFieldC;
    private static C StaticPropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = M1() + paramC.M1() + localC.M1() + fieldC.M1() + PropertyC.M1() + fieldC.PropertyC.M1() + this.M1() + C.staticFieldC.M1() + StaticPropertyC.M1();
    }
}",
@"
public class C
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }
    private static C staticFieldC;
    private static C StaticPropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = M1() + M1() + M1() + M1() + M1() + M1() + M1() + M1() + M1();
    }
}");
        }

        [Fact]
        public async Task TestBasic_ReferencesInSameType_Invocations()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Private x As Integer
    Private fieldC As C
    Private Property PropertyC As C

    Public Function [|M1|]() As Integer
        Return 0
    End Function

    Public Sub M2(paramC As C)
        Dim localC = fieldC
        x = M1() + paramC.M1() + localC.M1() + fieldC.M1() + PropertyC.M1() + fieldC.PropertyC.M1() + Me.M1()
    End Sub
End Class",
@"
Public Class C
    Private x As Integer
    Private fieldC As C
    Private Property PropertyC As C

    Public Shared Function M1() As Integer
        Return 0
    End Function

    Public Sub M2(paramC As C)
        Dim localC = fieldC
        x = M1() + M1() + M1() + M1() + M1() + M1() + M1()
    End Sub
End Class");
        }

        [Fact]
        public async Task TestCSharp_ReferencesInSameFile_MemberReferences()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class C
{
    public C PropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }
}

class C2
{
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = paramC.M1,
            m2 = localC.M1,
            m3 = fieldC.M1,
            m4 = PropertyC.M1,
            m5 = fieldC.PropertyC.M1;
    }
}",
@"
using System;

public class C
{
    public C PropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }
}

class C2
{
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = C.M1,
            m2 = C.M1,
            m3 = C.M1,
            m4 = C.M1,
            m5 = C.M1;
    }
}");
        }

        [Fact]
        public async Task TestCSharp_ReferencesInSameFile_Invocations()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class C
{
    public C PropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }
}

class C2
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = paramC.M1() + localC.M1() + fieldC.M1() + PropertyC.M1() + fieldC.PropertyC.M1();
    }
}",
@"
using System;

public class C
{
    public C PropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }
}

class C2
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = C.M1() + C.M1() + C.M1() + C.M1() + C.M1();
    }
}");
        }

        [Fact]
        public async Task TestCSharp_ReferencesInMultipleFiles_MemberReferences()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public C PropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }
}

class C2
{
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = paramC.M1,
            m2 = localC.M1,
            m3 = fieldC.M1,
            m4 = PropertyC.M1,
            m5 = fieldC.PropertyC.M1;
    }
}",
                        @"
using System;

class C3
{
    private C fieldC;
    private C PropertyC { get; set; }

    public void M3(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = paramC.M1,
            m2 = localC.M1,
            m3 = fieldC.M1,
            m4 = PropertyC.M1,
            m5 = fieldC.PropertyC.M1;
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public C PropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }
}

class C2
{
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = C.M1,
            m2 = C.M1,
            m3 = C.M1,
            m4 = C.M1,
            m5 = C.M1;
    }
}",
                        @"
using System;

class C3
{
    private C fieldC;
    private C PropertyC { get; set; }

    public void M3(C paramC)
    {
        var localC = fieldC;
        Func<int> m1 = C.M1,
            m2 = C.M1,
            m3 = C.M1,
            m4 = C.M1,
            m5 = C.M1;
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_ReferencesInMultipleFiles_Invocations()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public C PropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }
}

class C2
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = paramC.M1() + localC.M1() + fieldC.M1() + PropertyC.M1() + fieldC.PropertyC.M1();
    }
}",
                        @"
using System;

class C3
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M3(C paramC)
    {
        var localC = fieldC;
        x = paramC.M1() + localC.M1() + fieldC.M1() + PropertyC.M1() + fieldC.PropertyC.M1();
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public C PropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }
}

class C2
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = C.M1() + C.M1() + C.M1() + C.M1() + C.M1();
    }
}",
                        @"
using System;

class C3
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M3(C paramC)
    {
        var localC = fieldC;
        x = C.M1() + C.M1() + C.M1() + C.M1() + C.M1();
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_ReferenceInArgument()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    private C fieldC;
    public C [|M1|](C c)
    {
        return c;
    }

    public C M2(C paramC)
    {
        var localC = fieldC;
        return this.M1(paramC.M1(localC));
    }
}",
@"
public class C
{
    private C fieldC;
    public static C M1(C c)
    {
        return c;
    }

    public C M2(C paramC)
    {
        var localC = fieldC;
        return M1(M1(localC));
    }
}");
        }

        [Fact]
        public async Task TestBasic_ReferenceInArgument()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Private fieldC As C

    Public Function [|M1|](c As C) As C
        Return c
    End Function

    Public Function M2(paramC As C) As C
        Dim localC = fieldC
        Return Me.M1(paramC.M1(localC))
    End Function
End Class",
@"
Public Class C
    Private fieldC As C

    Public Shared Function M1(c As C) As C
        Return c
    End Function

    Public Function M2(paramC As C) As C
        Dim localC = fieldC
        Return M1(M1(localC))
    End Function
End Class");
        }

        [Fact]
        public async Task TestCSharp_GenericMethod()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    private C fieldC;
    public C [|M1|]<T>(C c, T t)
    {
        return c;
    }

    public C M1<T>(T t, int i)
    {
        return fieldC;
    }
}

public class C2<T2>
{
    private C fieldC;
    public void M2(C paramC)
    {
        // Explicit type argument
        paramC.M1<int>(fieldC, 0);
        
        // Implicit type argument
        paramC.M1(fieldC, this);
    }
}",
@"
public class C
{
    private C fieldC;
    public static C M1<T>(C c, T t)
    {
        return c;
    }

    public C M1<T>(T t, int i)
    {
        return fieldC;
    }
}

public class C2<T2>
{
    private C fieldC;
    public void M2(C paramC)
    {
        // Explicit type argument
        C.M1(fieldC, 0);

        // Implicit type argument
        C.M1(fieldC, this);
    }
}");
        }

        [Fact]
        public async Task TestCSharp_GenericMethod_02()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    private C fieldC;
    public C [|M1|]<T>(C c)
    {
        return c;
    }

    public C M1<T>(T t)
    {
        return fieldC;
    }
}

public class C2<T2>
{
    private C fieldC;
    public void M2(C paramC)
    {
        // Explicit type argument
        paramC.M1<int>(fieldC);
    }
}",
@"
public class C
{
    private C fieldC;
    public static C M1<T>(C c)
    {
        return c;
    }

    public C M1<T>(T t)
    {
        return fieldC;
    }
}

public class C2<T2>
{
    private C fieldC;
    public void M2(C paramC)
    {
        // Explicit type argument
        C.M1<int>(fieldC);
    }
}");
        }

        [Fact]
        public async Task TestBasic_GenericMethod()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Private fieldC As C

    Public Function [|M1|](Of T)(c As C, t1 As T) As C
        Return c
    End Function

    Public Function M1(Of T)(t1 As T, i As Integer) As C
        Return fieldC
    End Function
End Class

Public Class C2(Of T2)
    Private fieldC As C

    Public Sub M2(paramC As C)
        ' Explicit type argument
        paramC.M1(Of Integer)(fieldC, 0)

        ' Implicit type argument
        paramC.M1(fieldC, Me)
    End Sub
End Class",
@"
Public Class C
    Private fieldC As C

    Public Shared Function M1(Of T)(c As C, t1 As T) As C
        Return c
    End Function

    Public Function M1(Of T)(t1 As T, i As Integer) As C
        Return fieldC
    End Function
End Class

Public Class C2(Of T2)
    Private fieldC As C

    Public Sub M2(paramC As C)
        ' Explicit type argument
        C.M1(Of Integer)(fieldC, 0)

        ' Implicit type argument
        C.M1(fieldC, Me)
    End Sub
End Class");
        }

        [Fact]
        public async Task TestCSharp_InvocationInInstance()
        {
            // We don't make the replacement if instance has an invocation.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    private C fieldC;
    public C [|M1|](C c)
    {
        return c;
    }

    public C M2(C paramC)
    {
        var localC = fieldC;
        return localC.M1(paramC).M1(paramC.M1(localC));
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
public class C
{
    private C fieldC;
    public static C M1(C c)
    {
        return c;
    }

    public C M2(C paramC)
    {
        var localC = fieldC;
        return {|CS0176:M1(paramC).M1|}(M1(localC));
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestBasic_InvocationInInstance()
        {
            // We don't make the replacement if instance has an invocation.
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Private fieldC As C

    Public Function [|M1|](c As C) As C
        Return c
    End Function

    Public Function M2(paramC As C) As C
        Dim localC = fieldC
        Return localC.M1(paramC).M1(paramC.M1(localC))
    End Function
End Class",
@"
Public Class C
    Private fieldC As C

    Public Shared Function M1(c As C) As C
        Return c
    End Function

    Public Function M2(paramC As C) As C
        Dim localC = fieldC
        Return M1(paramC).M1(M1(localC))
    End Function
End Class");
        }

        [Fact]
        public async Task TestCSharp_ConversionInInstance()
        {
            // We don't make the replacement if instance has a conversion.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    private C fieldC;
    public object [|M1|](C c)
    {
        return c;
    }

    public C M2(C paramC)
    {
        var localC = fieldC;
        return {|CS0266:((C)paramC).M1(localC)|};
    }
}"
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
public class C
{
    private C fieldC;
    public static object M1(C c)
    {
        return c;
    }

    public C M2(C paramC)
    {
        var localC = fieldC;
        return {|CS0176:((C)paramC).M1|}(localC);
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestBasic_ConversionInInstance()
        {
            // We don't make the replacement if instance has a conversion.
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Private fieldC As C

    Public Function [|M1|](c As C) As Object
        Return c
    End Function

    Public Function M2(paramC As C) As C
        Dim localC = fieldC
        Return (CType(paramC, C)).M1(localC)
    End Function
End Class",
@"
Public Class C
    Private fieldC As C

    Public Shared Function M1(c As C) As Object
        Return c
    End Function

    Public Function M2(paramC As C) As C
        Dim localC = fieldC
        Return (CType(paramC, C)).M1(localC)
    End Function
End Class");
        }

        [Fact]
        public async Task TestCSharp_FixAll()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public C PropertyC { get; set; }

    public int [|M1|]()
    {
        return 0;
    }

    public int [|M2|]()
    {
        return 0;
    }
}

class C2
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = paramC.M1() + localC.M2() + fieldC.M1() + PropertyC.M2() + fieldC.PropertyC.M1();
    }
}",
                        @"
using System;

class C3
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M3(C paramC)
    {
        var localC = fieldC;
        x = paramC.M2() + localC.M1() + fieldC.M2() + PropertyC.M1() + fieldC.PropertyC.M2();
    }
}",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public C PropertyC { get; set; }

    public static int M1()
    {
        return 0;
    }

    public static int M2()
    {
        return 0;
    }
}

class C2
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M2(C paramC)
    {
        var localC = fieldC;
        x = C.M1() + C.M2() + C.M1() + C.M2() + C.M1();
    }
}",
                        @"
using System;

class C3
{
    private int x;
    private C fieldC;
    private C PropertyC { get; set; }

    public void M3(C paramC)
    {
        var localC = fieldC;
        x = C.M2() + C.M1() + C.M2() + C.M1() + C.M2();
    }
}",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestBasic_FixAll()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System

Public Class C
    Public Property PropertyC As C

    Public Function [|M1|]() As Integer
        Return 0
    End Function

    Public Function [|M2|]() As Integer
        Return 0
    End Function
End Class

Class C2
    Private x As Integer
    Private fieldC As C
    Private Property PropertyC As C

    Public Sub M2(paramC As C)
        Dim localC = fieldC
        x = paramC.M1() + localC.M2() + fieldC.M1() + PropertyC.M2() + fieldC.PropertyC.M1()
    End Sub
End Class",
                        @"
Imports System

Class C3
    Private x As Integer
    Private fieldC As C
    Private Property PropertyC As C

    Public Sub M3(paramC As C)
        Dim localC = fieldC
        x = paramC.M2() + localC.M1() + fieldC.M2() + PropertyC.M1() + fieldC.PropertyC.M2()
    End Sub
End Class",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Imports System

Public Class C
    Public Property PropertyC As C

    Public Shared Function M1() As Integer
        Return 0
    End Function

    Public Shared Function M2() As Integer
        Return 0
    End Function
End Class

Class C2
    Private x As Integer
    Private fieldC As C
    Private Property PropertyC As C

    Public Sub M2(paramC As C)
        Dim localC = fieldC
        x = C.M1() + C.M2() + C.M1() + C.M2() + C.M1()
    End Sub
End Class",
                        @"
Imports System

Class C3
    Private x As Integer
    Private fieldC As C
    Private Property PropertyC As C

    Public Sub M3(paramC As C)
        Dim localC = fieldC
        x = C.M2() + C.M1() + C.M2() + C.M1() + C.M2()
    End Sub
End Class",
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestCSharp_PropertyWithReferences()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    private C fieldC;
    public C [|M1|] { get { return null; } set { } }

    public C M2(C paramC)
    {
        var x = this.M1;
        paramC.M1 = x;
        return fieldC;
    }
}",
@"
public class C
{
    private C fieldC;
    public static C M1 { get { return null; } set { } }

    public C M2(C paramC)
    {
        var x = M1;
        M1 = x;
        return fieldC;
    }
}");
        }

        [Fact]
        public async Task TestBasic_PropertyWithReferences()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Private fieldC As C

    Public Property [|M1|] As C
        Get
            Return Nothing
        End Get
        Set(ByVal value As C)
        End Set
    End Property

    Public Function M2(paramC As C) As C
        Dim x = Me.M1
        paramC.M1 = x
        Return fieldC
    End Function
End Class",
@"
Public Class C
    Private fieldC As C

    Public Shared Property M1 As C
        Get
            Return Nothing
        End Get
        Set(ByVal value As C)
        End Set
    End Property

    Public Function M2(paramC As C) As C
        Dim x = M1
        M1 = x
        Return fieldC
    End Function
End Class");
        }

        [Fact, WorkItem(2888, "https://github.com/dotnet/roslyn-analyzers/issues/2888")]
        public async Task CA1822_CSharp_AsyncModifier()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Threading.Tasks;

public class C
{
    public async Task<int> [|M1|]()
    {
        await Task.Delay(20).ConfigureAwait(false);
        return 20;
    }
}",
@"
using System.Threading.Tasks;

public class C
{
    public static async Task<int> M1()
    {
        await Task.Delay(20).ConfigureAwait(false);
        return 20;
    }
}");
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.Threading.Tasks

Public Class C
    Public Async Function [|M1|]() As Task(Of Integer)
        Await Task.Delay(20).ConfigureAwait(False)
        Return 20
    End Function
End Class",
@"
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function M1() As Task(Of Integer)
        Await Task.Delay(20).ConfigureAwait(False)
        Return 20
    End Function
End Class");
        }

        [Fact]
        [WorkItem(4733, "https://github.com/dotnet/roslyn-analyzers/issues/4733")]
        [WorkItem(5168, "https://github.com/dotnet/roslyn-analyzers/issues/5168")]
        public async Task CA1822_PartialMethod_CannotBeStatic()
        {
            string source = @"
using System.Threading;
using System.Threading.Tasks;

public partial class Class1
{
    public partial Task Example(CancellationToken token = default);
}

partial class Class1
{
    private readonly int timeout;

    public Class1(int timeout)
    {
        this.timeout = timeout;
    }
    
    public async partial Task Example(CancellationToken token)
    {
        await Task.Delay(timeout, token);
    }
}
";
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode = source,
                FixedCode = source,
            }.RunAsync();
        }

        [Fact]
        [WorkItem(4733, "https://github.com/dotnet/roslyn-analyzers/issues/4733")]
        [WorkItem(5168, "https://github.com/dotnet/roslyn-analyzers/issues/5168")]
        public async Task CA1822_PartialMethod_CanBeStatic()
        {
            string source = @"
using System.Threading;
using System.Threading.Tasks;

public partial class Class1
{
    public partial Task Example(CancellationToken token = default);
}

partial class Class1
{
    private readonly int timeout;

    public Class1(int timeout)
    {
        this.timeout = timeout;
    }

    public async partial Task [|Example|](CancellationToken token)
    {
        await Task.Delay(0);
    }
}
";
            // The fixed source shouldn't have diagnostics. Tracked by https://github.com/dotnet/roslyn-analyzers/issues/5171.
            string fixedSource = @"
using System.Threading;
using System.Threading.Tasks;

public partial class Class1
{
    public partial Task Example(CancellationToken token = default);
}

partial class Class1
{
    private readonly int timeout;

    public Class1(int timeout)
    {
        this.timeout = timeout;
    }

    public static async partial Task {|CS0763:Example|}(CancellationToken token)
    {
        await Task.Delay(0);
    }
}
";
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode = source,
                FixedCode = fixedSource,
            }.RunAsync();
        }
    }
}
