// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpOverrideEqualsAndOperatorEqualsOnValueTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideEqualsAndOperatorEqualsOnValueTypesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideEqualsAndOperatorEqualsOnValueTypesFixerTests
    {
        [Fact]
        public async Task CSharpCodeFixNoEqualsOverrideOrEqualityOperators()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public struct A
{
    public int X;
}
",
                new[]
                {
                    VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.EqualsRule).WithSpan(2, 15, 2, 16).WithArguments("A"),
                    VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule).WithSpan(2, 15, 2, 16).WithArguments("A"),
                },
@"
public struct A
{
    public int X;

    public override bool Equals(object obj)
    {
        throw new System.NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }

    public static bool operator ==(A left, A right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(A left, A right)
    {
        return !(left == right);
    }
}
");
        }

        [Fact]
        public async Task CSharpCodeFixNoEqualsOverride()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public struct A
{
    public static bool operator ==(A left, A right)
    {
        throw new System.NotImplementedException();
    }

    public static bool operator !=(A left, A right)
    {
        throw new System.NotImplementedException();
    }
}
",
                VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.EqualsRule).WithSpan(2, 15, 2, 16).WithArguments("A"),
@"
public struct A
{
    public static bool operator ==(A left, A right)
    {
        throw new System.NotImplementedException();
    }

    public static bool operator !=(A left, A right)
    {
        throw new System.NotImplementedException();
    }

    public override bool Equals(object obj)
    {
        throw new System.NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CSharpCodeFixNoEqualityOperator()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public struct A
{
    public override bool Equals(object obj)
    {
        throw new System.NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }

    public static bool operator {|CS0216:!=|}(A left, A right)   // error CS0216: The operator requires a matching operator '==' to also be defined
    {
        throw new System.NotImplementedException();
    }
}
",
                VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule).WithSpan(2, 15, 2, 16).WithArguments("A"),
@"
public struct A
{
    public override bool Equals(object obj)
    {
        throw new System.NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }

    public static bool operator !=(A left, A right)   // error CS0216: The operator requires a matching operator '==' to also be defined
    {
        throw new System.NotImplementedException();
    }

    public static bool operator ==(A left, A right)
    {
        return left.Equals(right);
    }
}
");
        }

        [Fact]
        public async Task CSharpCodeFixNoInequalityOperator()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public struct A
{
    public override bool Equals(object obj)
    {
        throw new System.NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }

    public static bool operator {|CS0216:==|}(A left, A right)   // error CS0216: The operator requires a matching operator '!=' to also be defined
    {
        throw new System.NotImplementedException();
    }
}
",
                VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule).WithSpan(2, 15, 2, 16).WithArguments("A"),
@"
public struct A
{
    public override bool Equals(object obj)
    {
        throw new System.NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new System.NotImplementedException();
    }

    public static bool operator ==(A left, A right)   // error CS0216: The operator requires a matching operator '!=' to also be defined
    {
        throw new System.NotImplementedException();
    }

    public static bool operator !=(A left, A right)
    {
        return !(left == right);
    }
}
");
        }
        [Fact]
        public async Task BasicCodeFixNoEqualsOverrideOrEqualityOperators()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Structure A
    Public X As Integer
End Structure
",
                new[]
                {
                    VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.EqualsRule).WithSpan(2, 18, 2, 19).WithArguments("A"),
                    VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule).WithSpan(2, 18, 2, 19).WithArguments("A"),
                },
@"
Public Structure A
    Public X As Integer

    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New System.NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator
End Structure
");
        }

        [Fact]
        public async Task BasicCodeFixNoEqualsOverride()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Structure A
    Public Shared Operator =(left As A, right As A) As Boolean
        Throw New System.NotImplementedException()
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Throw New System.NotImplementedException()
    End Operator
End Structure
",
                VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.EqualsRule).WithSpan(2, 18, 2, 19).WithArguments("A"),
@"
Public Structure A
    Public Shared Operator =(left As A, right As A) As Boolean
        Throw New System.NotImplementedException()
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Throw New System.NotImplementedException()
    End Operator

    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New System.NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function
End Structure
");
        }

        [Fact]
        public async Task BasicCodeFixNoEqualityOperator()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Structure A
    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New System.NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function

    Public Shared Operator {|BC33033:<>|}(left As A, right As A) As Boolean   ' error BC33033: Matching '=' operator is required
        Throw New System.NotImplementedException()
    End Operator
End Structure
",
                VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule).WithSpan(2, 18, 2, 19).WithArguments("A"),
@"
Public Structure A
    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New System.NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function

    Public Shared Operator <>(left As A, right As A) As Boolean   ' error BC33033: Matching '=' operator is required
        Throw New System.NotImplementedException()
    End Operator

    Public Shared Operator =(left As A, right As A) As Boolean
        Return left.Equals(right)
    End Operator
End Structure
");
        }

        [Fact]
        public async Task BasicCodeFixNoInequalityOperator()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Structure A
    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New System.NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function

    Public Shared Operator {|BC33033:=|}(left As A, right As A) As Boolean   ' error BC33033: Matching '<>' operator is required
        Throw New System.NotImplementedException()
    End Operator
End Structure
",
                VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule).WithSpan(2, 18, 2, 19).WithArguments("A"),
@"
Public Structure A
    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New System.NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New System.NotImplementedException()
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean   ' error BC33033: Matching '<>' operator is required
        Throw New System.NotImplementedException()
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator
End Structure
");
        }
    }
}