// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public partial class OverloadOperatorEqualsOnOverridingValueTypeEqualsTests
    {
        [Fact]
        public async Task CA2231NoWarningCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    // Non-value type
    public class A
    {    
        public override bool Equals(Object obj)
        {
            return true;
        }
    }

    // value type without overriding Equals
    public struct B
    {    
        public new bool Equals(Object obj)
        {
            return true;
        }
    }
");
        }

        [Fact]
        public async Task CA2231NoEqualsOperatorCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public struct A
    {
        public override bool Equals(Object obj)
        {
            return true;
        }
    }
",
            GetCA2231CSharpResultAt(4, 19));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA2231NoEqualsOperatorButNotExternallyVisibleCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    struct A
    {
        public override bool Equals(Object obj)
        {
            return true;
        }
    }

    public class A2
    {
        private struct B
        {
            public override bool Equals(Object obj)
            {
                return true;
            }
        }
    }
");
        }

        [Fact]
        public async Task CA2231NoEqualsOperatorCSharpOutofScopeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public struct [|A|]
    {
        public override bool Equals(Object obj)
        {
            return true;
        }
    }

    // value type without overriding Equals
    public struct B
    {    
        public new bool Equals(Object obj)
        {
            return true;
        }
    }
");
        }

        [Fact]
        public async Task CA2231CSharpInnerClassHasNoEqualsOperatorCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public struct A
    {
        public override bool Equals(Object obj)
        {
            return true;
        }

        public struct Aa
        {
            public override bool Equals(Object obj)
            {
                return true;
            }
        }
    }
",
            GetCA2231CSharpResultAt(4, 19),
            GetCA2231CSharpResultAt(11, 23));
        }

        [Fact]
        public async Task CA2231HasEqualsOperatorCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public struct A
    {
        public override bool Equals(Object obj)
        {
            return true;
        }

        public static bool operator ==(A a1, A a2)
        {
            return false;
        }

        public static bool operator !=(A a1, A a2)
        {
            return false;
        }
    }
");
        }

        [Fact]
        public async Task CA2231_CSharp_RefStruct_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public ref struct S
{
    public override bool Equals(object other)
    {
        return false;
    }
}
",
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Fact]
        public async Task CA2231NoWarningBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2231NoEqualsOperatorBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Structure
",
            GetCA2231BasicResultAt(4, 18));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA2231NoEqualsOperatorButNotExternallyVisibleBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Structure A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Structure

Public Class A2
    Private Structure B
        Public Overloads Overrides Function Equals(obj As Object) As Boolean
            Return True
        End Function
    End Structure
End Class
");
        }

        [Fact]
        public async Task CA2231NoEqualsOperatorBasicWithScopeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Class

Public Structure [|B|]
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function
End Structure
");
        }

        [Fact]
        public async Task CA2231BasicInnerClassHasNoEqualsOperatorBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Structure Aa
        Public Overloads Overrides Function Equals(obj As Object) As Boolean
            Return True
        End Function
    End Structure
End Structure
",
            GetCA2231BasicResultAt(4, 18),
            GetCA2231BasicResultAt(9, 22));
        }

        [Fact]
        public async Task CA2231HasEqualsOperatorBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Structure A
    Public Overloads Overrides Function Equals(obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(left As A, right As A)
        Return True
    End Operator

    Public Shared Operator <>(left As A, right As A)
        Return True
    End Operator
End Structure
");
        }

        private static DiagnosticResult GetCA2231CSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs

        private static DiagnosticResult GetCA2231BasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs
    }
}
