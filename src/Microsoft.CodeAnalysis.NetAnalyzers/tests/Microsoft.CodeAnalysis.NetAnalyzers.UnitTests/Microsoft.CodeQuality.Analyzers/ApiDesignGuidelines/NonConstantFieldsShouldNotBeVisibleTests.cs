// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class NonConstantFieldsShouldNotBeVisibleTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new NonConstantFieldsShouldNotBeVisibleAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NonConstantFieldsShouldNotBeVisibleAnalyzer();
        }

        [Fact]
        public void DefaultVisibilityCS()
        {
            VerifyCSharp(@"
public class A
{
    string field; 
}");
        }

        [Fact]
        public void DefaultVisibilityVB()
        {
            VerifyBasic(@"
Public Class A
    Dim field As System.String
End Class");
        }

        [Fact]
        public void PublicVariableCS()
        {
            VerifyCSharp(@"
public class A
{
    public string field; 
}");
        }

        [Fact]
        public void PublicVariableVB()
        {
            VerifyBasic(@"
Public Class A
    Public field As System.String
End Class");
        }

        [Fact]
        public void ExternallyVisibleStaticVariableCS()
        {
            VerifyCSharp(@"
public class A
{
    public static string field; 
}", GetCSharpResultAt(4, 26, NonConstantFieldsShouldNotBeVisibleAnalyzer.Rule));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void PublicNotExternallyVisibleStaticVariableCS()
        {
            VerifyCSharp(@"
class A
{
    public static string field;
}

public class B
{
    private class C
    {
        public static string field;
    }
}
");
        }

        [Fact]
        public void ExternallyVisibleStaticVariableVB()
        {
            VerifyBasic(@"
Public Class A
    Public Shared field as System.String
End Class", GetBasicResultAt(3, 19, NonConstantFieldsShouldNotBeVisibleAnalyzer.Rule));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void PublicNotExternallyVisibleStaticVariableVB()
        {
            VerifyBasic(@"
Class A
    Public Shared field as System.String
End Class

Public Class B
    Private Class C
        Public Shared field as System.String
    End Class
End Class
");
        }

        [Fact]
        public void PublicStaticReadonlyVariableCS()
        {
            VerifyCSharp(@"
public class A
{
    public static readonly string field; 
}");
        }

        [Fact]
        public void PublicStaticReadonlyVariableVB()
        {
            VerifyBasic(@"
Public Class A
    Public Shared ReadOnly field as System.String
End Class");
        }

        [Fact]
        public void PublicConstVariableCS()
        {
            VerifyCSharp(@"
public class A
{
    public const string field = ""X""; 
}");
        }

        [Fact]
        public void PublicConstVariableVB()
        {
            VerifyBasic(@"
Public Class A
    Public Const field as System.String = ""X""
End Class");
        }
    }
}
