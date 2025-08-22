﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OperatorOverloadsHaveNamedAlternatesFixerTests
    {
        #region C# tests

        [Fact]
        public async Task AddAlternateMethod_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.DefaultRule).WithSpan(4, 30, 4, 31).WithArguments("Add", "op_Addition"),
@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }

    public static C Add(C left, C right)
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task AddAlternateOfMultiples_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 30, 4, 31).WithArguments("Mod", "Remainder", "op_Modulus"),
@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }

    public static C Mod(C left, C right)
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task AddAlternateProperty_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.PropertyRule).WithSpan(4, 33, 4, 37).WithArguments("IsTrue", "op_True"),
@"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }

    public bool IsTrue
    {
        get
        {
            throw new System.NotImplementedException();
        }
    }
}
");
        }

        [Fact]
        public async Task AddAlternateForConversion_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    public static implicit operator int(C item) { return 0; }
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 40).WithArguments("ToInt32", "FromC", "op_Implicit"),
@"
public class C
{
    public static implicit operator int(C item) { return 0; }

    public int ToInt32()
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task AddAlternateForCompare_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    public static bool operator {|CS0216:<|}(C left, C right) { return true; }   // error CS0216: The operator requires a matching operator '>' to also be defined
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 33, 4, 34).WithArguments("CompareTo", "Compare", "op_LessThan"),
@"
public class C
{
    public static bool operator {|CS0216:<|}(C left, C right) { return true; }   // error CS0216: The operator requires a matching operator '>' to also be defined

    public int CompareTo(C other)
    {
        if (ReferenceEquals(other, null))
        {
            return 1;
        }

        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task AddAlternateForStructCompare_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public struct C
{
    public static bool operator {|CS0216:<|}(C left, C right) { return true; }   // error CS0216: The operator requires a matching operator '>' to also be defined
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 33, 4, 34).WithArguments("CompareTo", "Compare", "op_LessThan"),
@"
public struct C
{
    public static bool operator {|CS0216:<|}(C left, C right) { return true; }   // error CS0216: The operator requires a matching operator '>' to also be defined

    public int CompareTo(C other)
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task AddAlternateForIncrement_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class C
{
    public static C operator ++(C item) { return new C(); }
}
",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.DefaultRule).WithSpan(4, 30, 4, 32).WithArguments("Increment", "op_Increment"),
@"
public class C
{
    public static C operator ++(C item) { return new C(); }

    public static C Increment(C item)
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task FixImproperMethodVisibility_CSharpAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
    protected static C {|#0:Add|}(C left, C right) { return new C(); }
}
",
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.VisibilityRule).WithLocation(0).WithArguments("Add", "op_Addition"),
                },
                FixedCode = @"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
    public static C Add(C left, C right) { return new C(); }
}
",
            }.RunAsync();
        }

        [Fact]
        public async Task FixImproperPropertyVisibility_CSharpAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }
    bool {|#0:IsTrue|} => true;
}
",
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.VisibilityRule).WithLocation(0).WithArguments("IsTrue", "op_True"),
                },
                FixedCode = @"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }
    public bool IsTrue => true;
}
",
            }.RunAsync();
        }

        #endregion

        #region VB tests

        [Fact]
        public async Task AddAlternateMethod_BasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator
End Class
",
                VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.DefaultRule).WithSpan(3, 28, 3, 29).WithArguments("Add", "op_Addition"),
@"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator

    Public Shared Function Add(left As C, right As C) As C
        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

        [Fact]
        public async Task AddAlternateOfMultiples_BasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Public Shared Operator Mod(left As C, right As C) As C
        Return New C()
    End Operator
End Class
",
                VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(3, 28, 3, 31).WithArguments("Mod", "Remainder", "op_Modulus"),
@"
Public Class C
    Public Shared Operator Mod(left As C, right As C) As C
        Return New C()
    End Operator

    Public Shared Function [Mod](left As C, right As C) As C
        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

        [Fact]
        public async Task AddAlternateProperty_BasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Public Shared Operator IsTrue(item As C) As Boolean
        Return True
    End Operator
    Public Shared Operator IsFalse(item As C) As Boolean
        Return False
    End Operator
End Class
",
                VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.PropertyRule).WithSpan(3, 28, 3, 34).WithArguments("IsTrue", "op_True"),
@"
Public Class C
    Public Shared Operator IsTrue(item As C) As Boolean
        Return True
    End Operator
    Public Shared Operator IsFalse(item As C) As Boolean
        Return False
    End Operator

    Public ReadOnly Property IsTrue As Boolean
        Get
            Throw New System.NotImplementedException()
        End Get
    End Property
End Class
");
        }

        [Fact]
        public async Task AddAlternateForConversion_BasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Public Shared Widening Operator CType(ByVal item As C) As Integer
        Return 0
    End Operator
End Class
",
                VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(3, 37, 3, 42).WithArguments("ToInt32", "FromC", "op_Implicit"),
@"
Public Class C
    Public Shared Widening Operator CType(ByVal item As C) As Integer
        Return 0
    End Operator

    Public Function ToInt32() As Integer
        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

        [Fact]
        public async Task AddAlternateForCompare_BasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class C
    Public Shared Operator {|BC33033:<|}(left As C, right As C) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator
End Class
",
                VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(3, 28, 3, 29).WithArguments("CompareTo", "Compare", "op_LessThan"),
@"
Public Class C
    Public Shared Operator {|BC33033:<|}(left As C, right As C) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator

    Public Function CompareTo(other As C) As Integer
        If ReferenceEquals(other, Nothing) Then
            Return 1
        End If

        Throw New System.NotImplementedException()
    End Function
End Class
");
        }

        [Fact]
        public async Task AddAlternateForStructCompare_BasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Structure C
    Public Shared Operator {|BC33033:<|}(left As C, right As C) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator
End Structure
",
                VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(3, 28, 3, 29).WithArguments("CompareTo", "Compare", "op_LessThan"),
@"
Public Structure C
    Public Shared Operator {|BC33033:<|}(left As C, right As C) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator

    Public Function CompareTo(other As C) As Integer
        Throw New System.NotImplementedException()
    End Function
End Structure
");
        }

        [Fact]
        public async Task FixImproperMethodVisibility_BasicAsync()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator

    Protected Shared Function {|#0:Add|}(left As C, right As C) As C
        Return New C()
    End Function
End Class
",
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.VisibilityRule).WithLocation(0).WithArguments("Add", "op_Addition"),
                },
                FixedCode = @"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator

    Public Shared Function Add(left As C, right As C) As C
        Return New C()
    End Function
End Class
",
            }.RunAsync();
        }

        [Fact]
        public async Task FixImproperPropertyVisibility_BasicAsync()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Public Class C
    Public Shared Operator IsTrue(item As C) As Boolean
        Return True
    End Operator
    Public Shared Operator IsFalse(item As C) As Boolean
        Return False
    End Operator

    Private ReadOnly Property {|#0:IsTrue|} As Boolean
        Get
            Return True
        End Get
    End Property
End Class
",
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.VisibilityRule).WithLocation(0).WithArguments("IsTrue", "op_True"),
                },
                FixedCode = @"
Public Class C
    Public Shared Operator IsTrue(item As C) As Boolean
        Return True
    End Operator
    Public Shared Operator IsFalse(item As C) As Boolean
        Return False
    End Operator

    Public ReadOnly Property IsTrue As Boolean
        Get
            Return True
        End Get
    End Property
End Class
",
            }.RunAsync();
        }

        #endregion
    }
}