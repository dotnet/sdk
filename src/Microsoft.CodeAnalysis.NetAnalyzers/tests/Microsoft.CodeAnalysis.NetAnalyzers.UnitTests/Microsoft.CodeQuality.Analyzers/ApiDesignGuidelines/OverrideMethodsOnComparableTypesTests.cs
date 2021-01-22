// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
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
        public async Task CA1036ClassNoWarningCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >=(A objLeft, A objRight)
        {
            return true;
        }
    }
");
        }

        [Fact]
        public async Task CA1036ClassWrongEqualsCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public bool Equals;

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }
    }
", GetCA1036CSharpBothResultAt(4, 18, "A", "<=, >="));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1036ClassWrongEqualsCSharp_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    internal class A : IComparable
    {    
        public override int GetHashCode()
        {
            return 1234;
        }

        public int CompareTo(object obj)
        {
            return 1;
        }

        public bool Equals;

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }
    }

    public class OuterClass
    {
        private class A : IComparable
        {    
            public override int GetHashCode()
            {
                return 1234;
            }

            public int CompareTo(object obj)
            {
                return 1;
            }

            public bool Equals;

            public static bool operator ==(A objLeft, A objRight)
            {
                return true;
            }

            public static bool operator !=(A objLeft, A objRight)
            {
                return true;
            }

            public static bool operator <(A objLeft, A objRight)
            {
                return true;
            }

            public static bool operator >(A objLeft, A objRight)
            {
                return true;
            }
        }
    }
");
        }

        [Fact]
        public async Task CA1036ClassWrongEquals2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >=(A objLeft, A objRight)
        {
            return true;
        }
    }

    public class B : IComparable
    {    
        public override int GetHashCode()
        {
            return 1234;
        }

        public int CompareTo(object obj)
        {
            return 1;
        }

        public bool Equals;

        public static bool operator ==(B objLeft, B objRight)
        {
            return true;
        }

        public static bool operator !=(B objLeft, B objRight)
        {
            return true;
        }

        public static bool operator <(B objLeft, B objRight)
        {
            return true;
        }

        public static bool operator <=(B objLeft, B objRight)
        {
            return true;
        }

        public static bool operator >(B objLeft, B objRight)
        {
            return true;
        }

        public static bool operator >=(B objLeft, B objRight)
        {
            return true;
        }
    }
",
                GetCA1036CSharpEqualsResultAt(52, 18, "B"));
        }

        [Fact]
        public async Task CA1036StructNoWarningCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >=(A objLeft, A objRight)
        {
            return true;
        }
    }
");
        }

        [Fact]
        public async Task CA1036PrivateClassNoOpLessThanNoWarningCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class class1
    {
        private class A : IComparable
        {    
            public override int GetHashCode()
            {
                return 1234;
            }

            public int CompareTo(object obj)
            {
                return 1;
            }

            public override bool Equals(object obj)
            {
                return true;
            }

            public static bool operator ==(A objLeft, A objRight)
            {
                return true;
            }

            public static bool operator !=(A objLeft, A objRight)
            {
                return true;
            }
        }
    }
");
        }

        [Fact]
        public async Task CA1036ClassNoEqualsOperatorCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }
    }
",
            GetCA1036CSharpBothResultAt(4, 18, "A", "<=, >="));
        }

        [Fact]
        public async Task CA1036ClassNoOpEqualsOperatorCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }
    }
",
            GetCA1036CSharpOperatorsResultAt(4, 18, "A", "==, !=, <=, >="));
        }

        [Fact]
        public async Task CA1036StructNoOpLessThanOperatorCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }
    }
",
            GetCA1036CSharpOperatorsResultAt(4, 19, "A", "<, <=, >, >="));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1036StructNoOpLessThanOperatorCSharp_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    internal struct A : IComparable
    {    
        public override int GetHashCode()
        {
            return 1234;
        }

        public int CompareTo(object obj)
        {
            return 1;
        }

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator ==(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator !=(A objLeft, A objRight)
        {
            return true;
        }
    }

    public class OuterClass
    {
        private struct A : IComparable
        {    
            public override int GetHashCode()
            {
                return 1234;
            }

            public int CompareTo(object obj)
            {
                return 1;
            }

            public override bool Equals(object obj)
            {
                return true;
            }

            public static bool operator ==(A objLeft, A objRight)
            {
                return true;
            }

            public static bool operator !=(A objLeft, A objRight)
            {
                return true;
            }
        }
    }
");
        }

        [Fact]
        public async Task CA1036ClassWithGenericIComparableCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class A : IComparable<int>
    {    
        public override int GetHashCode()
        {
            return 1234;
        }

        public int CompareTo(int obj)
        {
            return 1;
        }

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }
    }
",
            GetCA1036CSharpOperatorsResultAt(4, 18, "A", "==, !=, <=, >="));
        }

        [Fact]
        public async Task CA1036ClassWithDerivedIComparableCSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    interface  IDerived : IComparable<int> { }

    public class A : IDerived
    {    
        public override int GetHashCode()
        {
            return 1234;
        }

        public int CompareTo(int obj)
        {
            return 1;
        }

        public override bool Equals(object obj)
        {
            return true;
        }

        public static bool operator <(A objLeft, A objRight)
        {
            return true;
        }

        public static bool operator >(A objLeft, A objRight)
        {
            return true;
        }
    }
",
            GetCA1036CSharpOperatorsResultAt(6, 18, "A", "==, !=, <=, >="));
        }

        [Fact]
        public async Task CA1036ClassNoWarningBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <=(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >=(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Class
");
        }

        [Fact]
        public async Task CA1036StructWrongEqualsBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Shadows Property Equals

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Structure
",
            GetCA1036BasicBothResultAt(4, 18, "A", "<=, >="));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1036StructWrongEqualsBasic_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend Structure A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Shadows Property Equals

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Structure

Public Class OuterClass
    Private Structure A : Implements IComparable

        Public Overrides Function GetHashCode() As Integer
            Return 1234
        End Function

        Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
            Return 1
        End Function

        Public Shadows Property Equals

        Public Shared Operator =(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator <(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator >(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

    End Structure
End Class
");
        }

        [Fact]
        public async Task CA1036StructWrongEqualsBasic2()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <=(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >=(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Class

Public Structure B : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Shadows Property Equals

    Public Shared Operator =(objLeft As B, objRight As B) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As B, objRight As B) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As B, objRight As B) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As B, objRight As B) As Boolean
        Return True
    End Operator

End Structure
",
                GetCA1036BasicBothResultAt(44, 18, "B", "<=, >="));
        }

        [Fact]
        public async Task CA1036StructNoWarningBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <=(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >=(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Structure
");
        }

        [Fact]
        public async Task CA1036PrivateClassNoOpLessThanNoWarningBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class Class1
    Private Class A : Implements IComparable

        Public Overrides Function GetHashCode() As Integer
            Return 1234
        End Function

        Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
            Return 1
        End Function

        Public Overloads Overrides Function Equals(obj As Object) As Boolean
            Return True
        End Function

        Public Shared Operator =(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

    End Class
End Class
");
        }

        [Fact]
        public async Task CA1036ClassNoEqualsOperatorBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Class
",
            GetCA1036BasicBothResultAt(4, 14, "A", "<=, >="));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1036ClassNoEqualsOperatorBasic_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Friend Class A 
    Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Class

Public Class OuterClass
    Private Class A 
        Implements IComparable

        Public Overrides Function GetHashCode() As Integer
            Return 1234
        End Function

        Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
            Return 1
        End Function

        Public Shared Operator =(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator <(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

        Public Shared Operator >(objLeft As A, objRight As A) As Boolean
            Return True
        End Operator

    End Class
End Class
");
        }

        [Fact]
        public async Task CA1036ClassNoOpEqualsOperatorBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator <(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator >(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Class
",
            GetCA1036BasicOperatorsResultAt(4, 14, "A", "=, <>, <=, >="));
        }

        [Fact]
        public async Task CA1036ClassNoOpLessThanOperatorBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A : Implements IComparable

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(obj As Object) As Integer Implements IComparable.CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Structure
",
            GetCA1036BasicOperatorsResultAt(4, 18, "A", "<, <=, >, >="));
        }

        [Fact]
        public async Task CA1036ClassWithGenericIComparableBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A : Implements IComparable(Of Integer)

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(other As Integer) As Integer Implements IComparable(Of Integer).CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Structure
",
            GetCA1036BasicOperatorsResultAt(4, 18, "A", "<, <=, >, >="));
        }

        [Fact]
        public async Task CA1036ClassWithDerivedIComparableBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Interface IDerived 
    Inherits IComparable(Of Integer)
End Interface

Public Structure A : Implements IDerived

    Public Overrides Function GetHashCode() As Integer
        Return 1234
    End Function

    Public Function CompareTo(other As Integer) As Integer  Implements IComparable(Of Integer).CompareTo
        Return 1
    End Function

    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(objLeft As A, objRight As A) As Boolean
        Return True
    End Operator

End Structure
",
            GetCA1036BasicOperatorsResultAt(8, 18, "A", "<, <=, >, >="));
        }

        [Fact]
        public async Task Bug1994CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync("enum MyEnum {}");
        }

        [Fact]
        public async Task Bug1994VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Enum MyEnum
    ValueOne
    ValueTwo
End Enum");
        }

        [Fact, WorkItem(1671, "https://github.com/dotnet/roslyn-analyzers/issues/1671")]
        public async Task CA1036BaseTypeComparable_NoWarningOnDerived_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class BaseClass : IComparable
{
    public int CompareTo(object obj)
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object obj)
    {
        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}

public class DerivedClass : BaseClass
{
}
",
            // Test0.cs(4,14): warning CA1036: BaseClass should define operator(s) '==, !=, <, <=, >, >=' since it implements IComparable.
            GetCA1036CSharpOperatorsResultAt(4, 14, "BaseClass", @"==, !=, <, <=, >, >="));
        }

        [Fact, WorkItem(1671, "https://github.com/dotnet/roslyn-analyzers/issues/1671")]
        public async Task CA1036BaseTypeGenericComparable_NoWarningOnDerived_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class BaseClass<T> : IComparable<T>
     where T : IComparable<T>
{
    public T Value { get; set; }


    public int CompareTo(T other)
    {
        return Value.CompareTo(other);
    }

    public override bool Equals(object obj)
    {
        if (obj is BaseClass<T> other)
        {
            return Value.Equals(other.Value);
        }

        return false;
    }

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}

public class DerivedClass<T> : BaseClass<T>
    where T : IComparable<T>
{
}
",
            // Test0.cs(4,14): warning CA1036: BaseClass should define operator(s) '==, !=, <, <=, >, >=' since it implements IComparable.
            GetCA1036CSharpOperatorsResultAt(4, 14, "BaseClass", @"==, !=, <, <=, >, >="));
        }

        private static DiagnosticResult GetCA1036CSharpOperatorsResultAt(int line, int column, string typeName, string operators)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleOperator)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, operators);

        private static DiagnosticResult GetCA1036BasicOperatorsResultAt(int line, int column, string typeName, string operators)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleOperator)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, operators);

        private static DiagnosticResult GetCA1036CSharpBothResultAt(int line, int column, string typeName, string operators)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, operators);

        private static DiagnosticResult GetCA1036BasicBothResultAt(int line, int column, string typeName, string operators)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleBoth)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, operators);

        private static DiagnosticResult GetCA1036CSharpEqualsResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OverrideMethodsOnComparableTypesAnalyzer.RuleEquals)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);
    }
}
