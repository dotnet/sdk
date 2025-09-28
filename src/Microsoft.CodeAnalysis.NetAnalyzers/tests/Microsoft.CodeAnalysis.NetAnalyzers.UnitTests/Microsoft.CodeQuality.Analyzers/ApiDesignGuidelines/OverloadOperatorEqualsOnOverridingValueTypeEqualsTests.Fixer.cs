// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverloadOperatorEqualsOnOverridingValueTypeEqualsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverloadOperatorEqualsOnOverridingValueTypeEqualsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public partial class OverloadOperatorEqualsOnOverridingValueTypeEqualsTests
    {
        [Fact]
        public async Task CA2231CSharpCodeFixNoEqualsOperatorAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

// value type without overriding Equals
public struct [|A|]
{    
    public override bool Equals(Object obj)
    {
        return true;
    }
}
",
@"
using System;

// value type without overriding Equals
public struct A
{    
    public override bool Equals(Object obj)
    {
        return true;
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
        public async Task CA2231BasicCodeFixNoEqualsOperatorAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Structure [|A|]
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Structure
",
@"
Imports System

Public Structure A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
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
        public async Task CA2231_CSharp_MultipleViolationsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public struct [|A|]
{
    public override bool Equals(object obj)
    {
        return true;
    }
}

public struct [|B|]
{
    public override bool Equals(object obj)
    {
        return true;
    }
}",
@"
using System;

public struct A
{
    public override bool Equals(object obj)
    {
        return true;
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

public struct B
{
    public override bool Equals(object obj)
    {
        return true;
    }

    public static bool operator ==(B left, B right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(B left, B right)
    {
        return !(left == right);
    }
}");
        }

        [Fact]
        public async Task CA2231_Basic_MultipleViolationsAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Structure [|A|]
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Structure

Public Structure [|B|]
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Structure",
@"
Imports System

Public Structure A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator
End Structure

Public Structure B
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(left As B, right As B) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As B, right As B) As Boolean
        Return Not left = right
    End Operator
End Structure");
        }
    }
}
