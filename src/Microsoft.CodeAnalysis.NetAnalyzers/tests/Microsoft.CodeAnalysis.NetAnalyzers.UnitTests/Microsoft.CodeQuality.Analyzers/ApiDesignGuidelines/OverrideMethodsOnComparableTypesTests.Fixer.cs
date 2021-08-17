// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideMethodsOnComparableTypesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideMethodsOnComparableTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideMethodsOnComparableTypesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideMethodsOnComparableTypesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public partial class OverrideMethodsOnComparableTypesTests
    {
        [Fact]
        public async Task CA1036ClassGenerateAllCSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class A : IComparable
{    
    public int CompareTo(object obj)
    {
        return 1;
    }
}
",
                VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 14, 4, 15).WithArguments("A", "==, !=, <, <=, >, >="),
@"
using System;

public class A : IComparable
{    
    public int CompareTo(object obj)
    {
        return 1;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (ReferenceEquals(obj, null))
        {
            return false;
        }

        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(A left, A right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator !=(A left, A right)
    {
        return !(left == right);
    }

    public static bool operator <(A left, A right)
    {
        return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
    }

    public static bool operator <=(A left, A right)
    {
        return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
    }

    public static bool operator >(A left, A right)
    {
        return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
    }

    public static bool operator >=(A left, A right)
    {
        return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
    }
}
");
        }

        [Fact]
        public async Task CA1036StructGenerateAllCSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public struct A : IComparable
{    
    public int CompareTo(object obj)
    {
        return 1;
    }
}
",
                VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 15, 4, 16).WithArguments("A", "==, !=, <, <=, >, >="),
@"
using System;

public struct A : IComparable
{    
    public int CompareTo(object obj)
    {
        return 1;
    }

    public override bool Equals(object obj)
    {
        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(A left, A right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(A left, A right)
    {
        return !(left == right);
    }

    public static bool operator <(A left, A right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(A left, A right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(A left, A right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(A left, A right)
    {
        return left.CompareTo(right) >= 0;
    }
}
");
        }

        [Fact]
        public async Task CA1036ClassGenerateSomeCSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class A : IComparable
{    
    public override int GetHashCode()
    {
        return 1234;
    }

    public int CompareTo(object obj)
    {
        return 1;
    }

    public static bool operator {|CS0216:!=|}(A objLeft, A objRight)   // error CS0216: The operator requires a matching operator '==' to also be defined
    {
        return true;
    }
}
",
                VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 14, 4, 15).WithArguments("A", "==, <, <=, >, >="),
@"
using System;

public class A : IComparable
{    
    public override int GetHashCode()
    {
        return 1234;
    }

    public int CompareTo(object obj)
    {
        return 1;
    }

    public static bool operator !=(A objLeft, A objRight)   // error CS0216: The operator requires a matching operator '==' to also be defined
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (ReferenceEquals(obj, null))
        {
            return false;
        }

        throw new NotImplementedException();
    }

    public static bool operator ==(A left, A right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator <(A left, A right)
    {
        return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
    }

    public static bool operator <=(A left, A right)
    {
        return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
    }

    public static bool operator >(A left, A right)
    {
        return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
    }

    public static bool operator >=(A left, A right)
    {
        return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
    }
}
");
        }

        [Fact]
        public async Task CA1036StructGenerateSomeCSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public struct A : IComparable
{    
    public override int GetHashCode()
    {
        return 1234;
    }

    public int CompareTo(object obj)
    {
        return 1;
    }

    public static bool operator {|CS0216:!=|}(A objLeft, A objRight)   // error CS0216: The operator requires a matching operator '==' to also be defined
    {
        return true;
    }
}
",
                VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 15, 4, 16).WithArguments("A", "==, <, <=, >, >="),
@"
using System;

public struct A : IComparable
{    
    public override int GetHashCode()
    {
        return 1234;
    }

    public int CompareTo(object obj)
    {
        return 1;
    }

    public static bool operator !=(A objLeft, A objRight)   // error CS0216: The operator requires a matching operator '==' to also be defined
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(A left, A right)
    {
        return left.Equals(right);
    }

    public static bool operator <(A left, A right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(A left, A right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(A left, A right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(A left, A right)
    {
        return left.CompareTo(right) >= 0;
    }
}
");
        }

        [Fact]
        public async Task CA1036ClassGenerateAllVisualBasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Class A : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

End Class
",
                VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 14, 4, 15).WithArguments("A", "=, <>, <, <=, >, >="),
@"
Imports System

Public Class A : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        If ReferenceEquals(Me, obj) Then
            Return True
        End If

        If ReferenceEquals(obj, Nothing) Then
            Return False
        End If

        Throw New NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New NotImplementedException()
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean
        If ReferenceEquals(left, Nothing) Then
            Return ReferenceEquals(right, Nothing)
        End If

        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator

    Public Shared Operator <(left As A, right As A) As Boolean
        Return If(ReferenceEquals(left, Nothing), Not ReferenceEquals(right, Nothing), left.CompareTo(right) < 0)
    End Operator

    Public Shared Operator <=(left As A, right As A) As Boolean
        Return ReferenceEquals(left, Nothing) OrElse left.CompareTo(right) <= 0
    End Operator

    Public Shared Operator >(left As A, right As A) As Boolean
        Return Not ReferenceEquals(left, Nothing) AndAlso left.CompareTo(right) > 0
    End Operator

    Public Shared Operator >=(left As A, right As A) As Boolean
        Return If(ReferenceEquals(left, Nothing), ReferenceEquals(right, Nothing), left.CompareTo(right) >= 0)
    End Operator
End Class
");
        }

        [Fact]
        public async Task CA1036StructGenerateAllVisualBasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Structure A : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

End Structure
",
                VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 18, 4, 19).WithArguments("A", "=, <>, <, <=, >, >="),
@"
Imports System

Public Structure A : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New NotImplementedException()
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator

    Public Shared Operator <(left As A, right As A) As Boolean
        Return left.CompareTo(right) < 0
    End Operator

    Public Shared Operator <=(left As A, right As A) As Boolean
        Return left.CompareTo(right) <= 0
    End Operator

    Public Shared Operator >(left As A, right As A) As Boolean
        Return left.CompareTo(right) > 0
    End Operator

    Public Shared Operator >=(left As A, right As A) As Boolean
        Return left.CompareTo(right) >= 0
    End Operator
End Structure
");
        }

        [Fact]
        public async Task CA1036ClassGenerateSomeVisualBasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Class A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Shared Operator {|BC33033:<|}(objLeft As A, objRight As A) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

End Class
",
                VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 14, 4, 15).WithArguments("A", "=, <>, <=, >, >="),
@"
Imports System

Public Class A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        If ReferenceEquals(Me, obj) Then
            Return True
        End If

        If ReferenceEquals(obj, Nothing) Then
            Return False
        End If

        Throw New NotImplementedException()
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean
        If ReferenceEquals(left, Nothing) Then
            Return ReferenceEquals(right, Nothing)
        End If

        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator

    Public Shared Operator <=(left As A, right As A) As Boolean
        Return ReferenceEquals(left, Nothing) OrElse left.CompareTo(right) <= 0
    End Operator

    Public Shared Operator >(left As A, right As A) As Boolean
        Return Not ReferenceEquals(left, Nothing) AndAlso left.CompareTo(right) > 0
    End Operator

    Public Shared Operator >=(left As A, right As A) As Boolean
        Return If(ReferenceEquals(left, Nothing), ReferenceEquals(right, Nothing), left.CompareTo(right) >= 0)
    End Operator
End Class
");
        }

        [Fact]
        public async Task CA1036StructGenerateSomeVisualBasicAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Structure A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Shared Operator {|BC33033:<|}(objLeft As A, objRight As A) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

End Structure
",
                VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 18, 4, 19).WithArguments("A", "=, <>, <=, >, >="),
@"
Imports System

Public Structure A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean   ' error BC33033: Matching '>' operator is required
        Return True
    End Operator

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New NotImplementedException()
    End Function

    Public Shared Operator =(left As A, right As A) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As A, right As A) As Boolean
        Return Not left = right
    End Operator

    Public Shared Operator <=(left As A, right As A) As Boolean
        Return left.CompareTo(right) <= 0
    End Operator

    Public Shared Operator >(left As A, right As A) As Boolean
        Return left.CompareTo(right) > 0
    End Operator

    Public Shared Operator >=(left As A, right As A) As Boolean
        Return left.CompareTo(right) >= 0
    End Operator
End Structure
");
        }

        [Fact, WorkItem(1395, "https://github.com/dotnet/roslyn-analyzers/issues/1395")]
        public async Task CA1036_CSharp_MultipleViolationsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class SomeClass : IComparable
{
    public int CompareTo(object obj)
    {
        return 1;
    }
}

public struct SomeOtherClass : IComparable
{
    public int CompareTo(object obj)
    {
        return 1;
    }
}",
                new[]
                {
                    VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 14, 4, 23).WithArguments("SomeClass", "==, !=, <, <=, >, >="),
                    VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(12, 15, 12, 29).WithArguments("SomeOtherClass", "==, !=, <, <=, >, >="),
                },
@"
using System;

public class SomeClass : IComparable
{
    public int CompareTo(object obj)
    {
        return 1;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (ReferenceEquals(obj, null))
        {
            return false;
        }

        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(SomeClass left, SomeClass right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator !=(SomeClass left, SomeClass right)
    {
        return !(left == right);
    }

    public static bool operator <(SomeClass left, SomeClass right)
    {
        return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
    }

    public static bool operator <=(SomeClass left, SomeClass right)
    {
        return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
    }

    public static bool operator >(SomeClass left, SomeClass right)
    {
        return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
    }

    public static bool operator >=(SomeClass left, SomeClass right)
    {
        return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
    }
}

public struct SomeOtherClass : IComparable
{
    public int CompareTo(object obj)
    {
        return 1;
    }

    public override bool Equals(object obj)
    {
        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(SomeOtherClass left, SomeOtherClass right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SomeOtherClass left, SomeOtherClass right)
    {
        return !(left == right);
    }

    public static bool operator <(SomeOtherClass left, SomeOtherClass right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(SomeOtherClass left, SomeOtherClass right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(SomeOtherClass left, SomeOtherClass right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(SomeOtherClass left, SomeOtherClass right)
    {
        return left.CompareTo(right) >= 0;
    }
}");
        }

        [Fact, WorkItem(1395, "https://github.com/dotnet/roslyn-analyzers/issues/1395")]
        public async Task CA1036_Basic_MultipleViolationsAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Class SomeClass : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

End Class

Public Structure SomeOtherClass : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

End Structure",
                new[]
                {
                    VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(4, 14, 4, 23).WithArguments("SomeClass", "=, <>, <, <=, >, >="),
                    VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth).WithSpan(12, 18, 12, 32).WithArguments("SomeOtherClass", "=, <>, <, <=, >, >="),
                },
@"
Imports System

Public Class SomeClass : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        If ReferenceEquals(Me, obj) Then
            Return True
        End If

        If ReferenceEquals(obj, Nothing) Then
            Return False
        End If

        Throw New NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New NotImplementedException()
    End Function

    Public Shared Operator =(left As SomeClass, right As SomeClass) As Boolean
        If ReferenceEquals(left, Nothing) Then
            Return ReferenceEquals(right, Nothing)
        End If

        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As SomeClass, right As SomeClass) As Boolean
        Return Not left = right
    End Operator

    Public Shared Operator <(left As SomeClass, right As SomeClass) As Boolean
        Return If(ReferenceEquals(left, Nothing), Not ReferenceEquals(right, Nothing), left.CompareTo(right) < 0)
    End Operator

    Public Shared Operator <=(left As SomeClass, right As SomeClass) As Boolean
        Return ReferenceEquals(left, Nothing) OrElse left.CompareTo(right) <= 0
    End Operator

    Public Shared Operator >(left As SomeClass, right As SomeClass) As Boolean
        Return Not ReferenceEquals(left, Nothing) AndAlso left.CompareTo(right) > 0
    End Operator

    Public Shared Operator >=(left As SomeClass, right As SomeClass) As Boolean
        Return If(ReferenceEquals(left, Nothing), ReferenceEquals(right, Nothing), left.CompareTo(right) >= 0)
    End Operator
End Class

Public Structure SomeOtherClass : Implements IComparable

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New NotImplementedException()
    End Function

    Public Overrides Function GetHashCode() As Integer
        Throw New NotImplementedException()
    End Function

    Public Shared Operator =(left As SomeOtherClass, right As SomeOtherClass) As Boolean
        Return left.Equals(right)
    End Operator

    Public Shared Operator <>(left As SomeOtherClass, right As SomeOtherClass) As Boolean
        Return Not left = right
    End Operator

    Public Shared Operator <(left As SomeOtherClass, right As SomeOtherClass) As Boolean
        Return left.CompareTo(right) < 0
    End Operator

    Public Shared Operator <=(left As SomeOtherClass, right As SomeOtherClass) As Boolean
        Return left.CompareTo(right) <= 0
    End Operator

    Public Shared Operator >(left As SomeOtherClass, right As SomeOtherClass) As Boolean
        Return left.CompareTo(right) > 0
    End Operator

    Public Shared Operator >=(left As SomeOtherClass, right As SomeOtherClass) As Boolean
        Return left.CompareTo(right) >= 0
    End Operator
End Structure");
        }
    }
}