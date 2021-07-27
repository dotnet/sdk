// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorOverloadsHaveNamedAlternatesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OperatorOverloadsHaveNamedAlternatesTests
    {
        #region Boilerplate

        private static DiagnosticResult GetCA2225CSharpDefaultResultAt(int line, int column, string alternateName, string operatorName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(alternateName, operatorName);

        private static DiagnosticResult GetCA2225CSharpPropertyResultAt(int line, int column, string alternateName, string operatorName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.PropertyRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(alternateName, operatorName);

        private static DiagnosticResult GetCA2225CSharpMultipleResultAt(int line, int column, string alternateName1, string alternateName2, string operatorName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(alternateName1, alternateName2, operatorName);

        private static DiagnosticResult GetCA2225CSharpVisibilityResultAt(int line, int column, string alternateName, string operatorName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.VisibilityRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(alternateName, operatorName);

        private static DiagnosticResult GetCA2225BasicDefaultResultAt(int line, int column, string alternateName, string operatorName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(alternateName, operatorName);

        #endregion

        #region C# tests

        [Fact]
        public async Task HasAlternateMethod_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
    public static C Add(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public async Task HasMultipleAlternatePrimary_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
    public static C Mod(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public async Task HasMultipleAlternateSecondary_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
    public static C Remainder(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public async Task HasAppropriateConversionAlternate_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static implicit operator int(C item) { return 0; }
    public int ToInt32() { return 0; }
}
");
        }

        [Fact, WorkItem(1717, "https://github.com/dotnet/roslyn-analyzers/issues/1717")]
        public async Task HasAppropriateConversionAlternate02_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Other
{	
	public int i {get; set;}

	public Other(int i) => this.i = i;	
}

public class SomeClass
{	
	public int i {get; set;}

	public SomeClass(int i) => this.i = i;

	public static implicit operator SomeClass(Other b) => new SomeClass(b.i);

	public static SomeClass FromOther(Other b) => new SomeClass(b.i);
}
");
        }

        [Fact]
        public async Task MissingAlternateMethod_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
}
",
            GetCA2225CSharpDefaultResultAt(4, 30, "Add", "op_Addition"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task MissingAlternateMethod_CSharp_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    public static C operator +(C left, C right) { return new C(); }
}

public class C2
{
    private class C3
    {
        public static C3 operator +(C3 left, C3 right) { return new C3(); }
    }
}
");
        }

        [Fact]
        public async Task MissingAlternateProperty_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }
}
",
            GetCA2225CSharpPropertyResultAt(4, 33, "IsTrue", "op_True"));
        }

        [Fact]
        public async Task MissingMultipleAlternates_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
}
",
            GetCA2225CSharpMultipleResultAt(4, 30, "Mod", "Remainder", "op_Modulus"));
        }

        [Fact]
        public async Task ImproperAlternateMethodVisibility_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
    protected static C Add(C left, C right) { return new C(); }
}
",
                GetCA2225CSharpVisibilityResultAt(5, 24, "Add", "op_Addition"));
        }

        [Fact]
        public async Task ImproperAlternatePropertyVisibility_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }
    private bool IsTrue => true;
}
",
            GetCA2225CSharpVisibilityResultAt(6, 18, "IsTrue", "op_True"));
        }

        [Fact]
        public async Task StructHasAlternateMethod_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
struct C
{
    public static C operator +(C left, C right) { return new C(); }
    public static C Add(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public async Task ImplicitCastToArray()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
public struct MyStruct
{
    public static implicit operator byte[](MyStruct myStruct)
    {
        return new byte[1];
    }
}",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 43).WithArguments("ToByteArray", "FromMyStruct", "op_Implicit"));
        }

        [Fact]
        public async Task ExplicitCastToArray()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
public struct MyStruct
{
    public static explicit operator byte[](MyStruct myStruct)
    {
        return new byte[1];
    }
}",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 43).WithArguments("ToByteArray", "FromMyStruct", "op_Explicit"));
        }

        [Fact]
        public async Task ImplicitCastToMultidimensionalArray()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
public struct MyStruct
{
    public static implicit operator byte[,](MyStruct myStruct)
    {
        return new byte[1,1];
    }
}",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 44).WithArguments("ToByteArray", "FromMyStruct", "op_Implicit"));
        }

        [Fact]
        public async Task ExplicitCastToMultidimensionalArray()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
public struct MyStruct
{
    public static explicit operator byte[,](MyStruct myStruct)
    {
        return new byte[1,1];
    }
}",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 44).WithArguments("ToByteArray", "FromMyStruct", "op_Explicit"));
        }

        [Fact]
        public async Task ImplicitCastToJaggedArray()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
public struct MyStruct
{
    public static implicit operator byte[][](MyStruct myStruct)
    {
        return new byte[1][];
    }
}",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 45).WithArguments("ToByteArray", "FromMyStruct", "op_Implicit"));
        }

        [Fact]
        public async Task ExplicitCastToJaggedArray()
        {
            await VerifyCS.VerifyAnalyzerAsync(
@"
public struct MyStruct
{
    public static explicit operator byte[][](MyStruct myStruct)
    {
        return new byte[1][];
    }
}",
                VerifyCS.Diagnostic(OperatorOverloadsHaveNamedAlternatesAnalyzer.MultipleRule).WithSpan(4, 37, 4, 45).WithArguments("ToByteArray", "FromMyStruct", "op_Explicit"));
        }

        #endregion

        //
        // Since the analyzer is symbol-based, only a few VB tests are added as a sanity check
        //

        #region VB tests

        [Fact]
        public async Task HasAlternateMethod_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator
    Public Shared Function Add(left As C, right As C) As C
        Return New C()
    End Function
End Class
");
        }

        [Fact]
        public async Task MissingAlternateMethod_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator
End Class
",
            GetCA2225BasicDefaultResultAt(3, 28, "Add", "op_Addition"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task MissingAlternateMethod_VisualBasic_Internal()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator
End Class

Public Class C2
    Private Class C3
        Public Shared Operator +(left As C3, right As C3) As C3
            Return New C3()
        End Operator
    End Class
End Class
");
        }

        [Fact]
        public async Task StructHasAlternateMethod_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator
    Public Shared Function Add(left As C, right As C) As C
        Return New C()
    End Function
End Structure
");
        }

        #endregion
    }
}