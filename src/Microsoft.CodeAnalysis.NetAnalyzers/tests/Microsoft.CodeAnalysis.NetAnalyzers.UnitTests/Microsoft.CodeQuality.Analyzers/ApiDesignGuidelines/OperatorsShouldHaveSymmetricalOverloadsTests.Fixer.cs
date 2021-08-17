// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorsShouldHaveSymmetricalOverloadsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorsShouldHaveSymmetricalOverloadsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorsShouldHaveSymmetricalOverloadsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorsShouldHaveSymmetricalOverloadsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OperatorsShouldHaveSymmetricalOverloadsFixerTests
    {
        [Fact]
        public async Task CSharpTestEquality()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|==|]|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '!=' to also be defined
}", @"
public class A
{
    public static bool operator==(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '!=' to also be defined

    public static bool operator !=(A a1, A a2)
    {
        return !(a1 == a2);
    }
}");
        }

        [Fact]
        public async Task CSharpTestOverloads1()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|==|]|}(A a1, A a2) { return false; }      // error CS0216: The operator requires a matching operator '!=' to also be defined
    public static bool operator{|CS0216:[|==|]|}(A a1, bool a2) { return false; }   // error CS0216: The operator requires a matching operator '!=' to also be defined
}", @"
public class A
{
    public static bool operator==(A a1, A a2) { return false; }      // error CS0216: The operator requires a matching operator '!=' to also be defined

    public static bool operator !=(A a1, A a2)
    {
        return !(a1 == a2);
    }

    public static bool operator==(A a1, bool a2) { return false; }   // error CS0216: The operator requires a matching operator '!=' to also be defined

    public static bool operator !=(A a1, bool a2)
    {
        return !(a1 == a2);
    }
}");
        }

        [Fact]
        public async Task CSharpTestInequality()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|!=|]|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '==' to also be defined
}", @"
public class A
{
    public static bool operator!=(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '==' to also be defined

    public static bool operator ==(A a1, A a2)
    {
        return !(a1 != a2);
    }
}");
        }

        [Fact]
        public async Task CSharpTestLessThan()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|<|]|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>' to also be defined
}", @"
public class A
{
    public static bool operator<(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>' to also be defined

    public static bool operator >(A a1, A a2)
    {
        throw new System.NotImplementedException();
    }
}");
        }

        [Fact]
        public async Task CSharpTestLessThanOrEqual()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|<=|]|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>=' to also be defined
}", @"
public class A
{
    public static bool operator<=(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>=' to also be defined

    public static bool operator >=(A a1, A a2)
    {
        throw new System.NotImplementedException();
    }
}");
        }

        [Fact]
        public async Task CSharpTestGreaterThan()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|>|]|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '<' to also be defined
}", @"
public class A
{
    public static bool operator>(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '<' to also be defined

    public static bool operator <(A a1, A a2)
    {
        throw new System.NotImplementedException();
    }
}");
        }

        [Fact]
        public async Task CSharpTestGreaterThanOrEqual()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
public class A
{
    public static bool operator{|CS0216:[|>=|]|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '<=' to also be defined
}", @"
public class A
{
    public static bool operator>=(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '<=' to also be defined

    public static bool operator <=(A a1, A a2)
    {
        throw new System.NotImplementedException();
    }
}");
        }

        [Fact]
        public async Task VisualBasicTestEquality()
        {
            await VerifyVB.VerifyCodeFixAsync(
                @"
Public class A
    public shared operator {|BC33033:[|=|]|}(a1 as A, a2 as A) as boolean   ' error BC33033: Matching '<>' operator is required
        return false
    end operator
end class", @"
Public class A
    public shared operator =(a1 as A, a2 as A) as boolean   ' error BC33033: Matching '<>' operator is required
        return false
    end operator

    Public Shared Operator <>(a1 As A, a2 As A) As Boolean
        Return Not a1 = a2
    End Operator
end class");
        }

        [Fact]
        public async Task VisualBasicTestInequality()
        {
            await VerifyVB.VerifyCodeFixAsync(
                @"
Public class A
    public shared operator {|BC33033:[|<>|]|}(a1 as A, a2 as A) as boolean   ' error BC33033: Matching '=' operator is required
        return false
    end operator
end class", @"
Public class A
    public shared operator <>(a1 as A, a2 as A) as boolean   ' error BC33033: Matching '=' operator is required
        return false
    end operator

    Public Shared Operator =(a1 As A, a2 As A) As Boolean
        Return Not a1 <> a2
    End Operator
end class");
        }

        [Fact]
        public async Task VisualBasicTestLessThan()
        {
            await VerifyVB.VerifyCodeFixAsync(
                @"
Public class A
    public shared operator {|BC33033:[|<|]|}(a1 as A, a2 as A) as boolean   ' error BC33033: Matching '>' operator is required
        return false
    end operator
end class", @"
Public class A
    public shared operator <(a1 as A, a2 as A) as boolean   ' error BC33033: Matching '>' operator is required
        return false
    end operator

    Public Shared Operator >(a1 As A, a2 As A) As Boolean
        Throw New System.NotImplementedException()
    End Operator
end class");
        }
    }
}