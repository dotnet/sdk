// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OperatorOverloadsHaveNamedAlternatesTests : DiagnosticAnalyzerTestBase
    {
        #region Boilerplate

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new OperatorOverloadsHaveNamedAlternatesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OperatorOverloadsHaveNamedAlternatesAnalyzer();
        }

        private static DiagnosticResult GetCA2225CSharpDefaultResultAt(int line, int column, string alternateName, string operatorName)
        {
            // Provide a method named '{0}' as a friendly alternate for operator {1}.
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageDefault, alternateName, operatorName);
            return GetCSharpResultAt(line, column, OperatorOverloadsHaveNamedAlternatesAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA2225CSharpPropertyResultAt(int line, int column, string alternateName, string operatorName)
        {
            // Provide a property named '{0}' as a friendly alternate for operator {1}.
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageProperty, alternateName, operatorName);
            return GetCSharpResultAt(line, column, OperatorOverloadsHaveNamedAlternatesAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA2225CSharpMultipleResultAt(int line, int column, string alternateName1, string alternateName2, string operatorName)
        {
            // Provide a method named '{0}' or '{1}' as an alternate for operator {2}
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageMultiple, alternateName1, alternateName2, operatorName);
            return GetCSharpResultAt(line, column, OperatorOverloadsHaveNamedAlternatesAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA2225CSharpVisibilityResultAt(int line, int column, string alternateName, string operatorName)
        {
            // Mark {0} as public because it is a friendly alternate for operator {1}.
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageVisibility, alternateName, operatorName);
            return GetCSharpResultAt(line, column, OperatorOverloadsHaveNamedAlternatesAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA2225BasicDefaultResultAt(int line, int column, string alternateName, string operatorName)
        {
            // Provide a method named '{0}' as a friendly alternate for operator {1}.
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageDefault, alternateName, operatorName);
            return GetBasicResultAt(line, column, OperatorOverloadsHaveNamedAlternatesAnalyzer.RuleId, message);
        }

        #endregion

        #region C# tests

        [Fact]
        public void HasAlternateMethod_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
    public static C Add(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public void HasMultipleAlternatePrimary_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
    public static C Mod(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public void HasMultipleAlternateSecondary_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
    public static C Remainder(C left, C right) { return new C(); }
}
");
        }

        [Fact]
        public void HasAppropriateConversionAlternate_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static implicit operator int(C item) { return 0; }
    public int ToInt32() { return 0; }
}
");
        }

        [Fact, WorkItem(1717, "https://github.com/dotnet/roslyn-analyzers/issues/1717")]
        public void HasAppropriateConversionAlternate02_CSharp()
        {
            VerifyCSharp(@"
public class Bar
{	
	public int i {get; set;}

	public Bar(int i) => this.i = i;	
}

public class Foo
{	
	public int i {get; set;}

	public Foo(int i) => this.i = i;

	public static implicit operator Foo(Bar b) => new Foo(b.i);

	public static Foo FromBar(Bar b) => new Foo(b.i);
}
");
        }

        [Fact]
        public void MissingAlternateMethod_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
}
",
            GetCA2225CSharpDefaultResultAt(4, 30, "Add", "op_Addition"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void MissingAlternateMethod_CSharp_Internal()
        {
            VerifyCSharp(@"
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
        public void MissingAlternateProperty_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static bool operator true(C item) { return true; }
    public static bool operator false(C item) { return false; }
}
",
            GetCA2225CSharpPropertyResultAt(4, 33, "IsTrue", "op_True"));
        }

        [Fact]
        public void MissingMultipleAlternates_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static C operator %(C left, C right) { return new C(); }
}
",
            GetCA2225CSharpMultipleResultAt(4, 30, "Mod", "Remainder", "op_Modulus"));
        }

        [Fact]
        public void ImproperAlternateMethodVisibility_CSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static C operator +(C left, C right) { return new C(); }
    protected static C Add(C left, C right) { return new C(); }
}
",
                GetCA2225CSharpVisibilityResultAt(5, 24, "Add", "op_Addition"));
        }

        [Fact]
        public void ImproperAlternatePropertyVisibility_CSharp()
        {
            VerifyCSharp(@"
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
        public void StructHasAlternateMethod_CSharp()
        {
            VerifyCSharp(@"
struct C
{
    public static C operator +(C left, C right) { return new C(); }
    public static C Add(C left, C right) { return new C(); }
}
");
        }

        #endregion

        //
        // Since the analyzer is symbol-based, only a few VB tests are added as a sanity check
        //

        #region VB tests

        [Fact]
        public void HasAlternateMethod_VisualBasic()
        {
            VerifyBasic(@"
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
        public void MissingAlternateMethod_VisualBasic()
        {
            VerifyBasic(@"
Public Class C
    Public Shared Operator +(left As C, right As C) As C
        Return New C()
    End Operator
End Class
",
            GetCA2225BasicDefaultResultAt(3, 28, "Add", "op_Addition"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void MissingAlternateMethod_VisualBasic_Internal()
        {
            VerifyBasic(@"
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
        public void StructHasAlternateMethod_VisualBasic()
        {
            VerifyBasic(@"
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