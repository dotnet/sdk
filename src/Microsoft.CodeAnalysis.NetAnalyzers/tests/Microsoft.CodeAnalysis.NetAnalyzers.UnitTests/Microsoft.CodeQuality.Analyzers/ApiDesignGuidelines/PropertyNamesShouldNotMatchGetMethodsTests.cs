// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class PropertyNamesShouldNotMatchGetMethodsTests : DiagnosticAnalyzerTestBase
    {
        private const string CSharpTestTemplate = @"
using System;

public class Test
{{
    {0} DateTime Date {{ get; }}
    {1} string GetDate()
    {{
        return DateTime.Today.ToString();
    }}
}}";

        private const string CSharpNotExternallyVisibleTestTemplate = @"
using System;

internal class OuterClass
{{
    public class Test
    {{
        {0} DateTime Date {{ get; }}
        {1} string GetDate()
        {{
            return DateTime.Today.ToString();
        }}
    }}
}}";

        private const string BasicTestTemplate = @"
Imports System

Public Class Test
    {0} ReadOnly Property [Date]() As DateTime
        Get
            Return DateTime.Today
        End Get
    End Property
    {1} Function GetDate() As String
        Return Me.Date.ToString()
    End Function 
End Class";

        private const string BasicNotExternallyVisibleTestTemplate = @"
Imports System

Friend Class OuterClass
    Public Class Test
        {0} ReadOnly Property [Date]() As DateTime
            Get
                Return DateTime.Today
            End Get
        End Property
        {1} Function GetDate() As String
            Return Me.Date.ToString()
        End Function 
    End Class
End Class
";

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new PropertyNamesShouldNotMatchGetMethodsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new PropertyNamesShouldNotMatchGetMethodsAnalyzer();
        }

        [Fact]
        public void CSharp_CA1721_PropertyNameDoesNotMatchGetMethodName_Exposed_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;

public class Test
{
    public DateTime Date { get; }
    public string GetTime()
    {
        return DateTime.Today.ToString();
    }
}");
        }

        [Theory]
        [InlineData("public", "public")]
        [InlineData("public", "protected")]
        [InlineData("public", "protected internal")]
        [InlineData("protected", "public")]
        [InlineData("protected", "protected")]
        [InlineData("protected", "protected internal")]
        [InlineData("protected internal", "public")]
        [InlineData("protected internal", "protected")]
        [InlineData("protected internal", "protected internal")]
        public void CSharp_CA1721_PropertyNamesMatchGetMethodNames_Exposed_Diagnostics(string propertyAccessibility, string methodAccessibility)
        {
            VerifyCSharp(
                string.Format(CSharpTestTemplate, propertyAccessibility, methodAccessibility),
                GetCA1721CSharpResultAt(
                    line: 6,
                    column: $"    {propertyAccessibility} DateTime ".Length + 1,
                    identifierName: "Date",
                    otherIdentifierName: "GetDate"));

            VerifyCSharp(
                string.Format(CSharpNotExternallyVisibleTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Theory]
        [InlineData("private", "private")]
        [InlineData("private", "internal")]
        [InlineData("internal", "private")]
        [InlineData("internal", "internal")]
        [InlineData("", "")]
        public void CSharp_CA1721_PropertyNamesMatchGetMethodNames_Unexposed_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            VerifyCSharp(string.Format(CSharpTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Theory, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        [InlineData("public", "private")]
        [InlineData("protected", "private")]
        [InlineData("protected internal", "private")]
        [InlineData("public", "internal")]
        [InlineData("protected", "internal")]
        [InlineData("protected internal", "internal")]
        [InlineData("public", "")]
        [InlineData("protected", "")]
        [InlineData("protected internal", "")]
        [InlineData("private", "public")]
        [InlineData("private", "protected")]
        [InlineData("private", "protected internal")]
        [InlineData("internal", "public")]
        [InlineData("internal", "protected")]
        [InlineData("internal", "protected internal")]
        [InlineData("", "public")]
        [InlineData("", "protected")]
        [InlineData("", "protected internal")]
        public void CSharp_CA1721_PropertyNamesMatchGetMethodNames_MixedExposure_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            VerifyCSharp(string.Format(CSharpTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Fact]
        public void CSharp_CA1721_PropertyNameMatchesBaseClassGetMethodName_Exposed_Diagnostic()
        {
            VerifyCSharp(@"
using System;

public class Foo
{
    public string GetDate()
    {
        return DateTime.Today.ToString();
    }
}

public class Bar : Foo
{
    public DateTime Date
    {
        get { return DateTime.Today; }
    }         
}",
            GetCA1721CSharpResultAt(line: 14, column: 21, identifierName: "Date", otherIdentifierName: "GetDate"));
        }


        [Fact]
        public void CSharp_CA1721_GetMethodNameMatchesBaseClassPropertyName_Exposed_Diagnostic()
        {
            VerifyCSharp(@"
using System;

public class Foo
{
    public DateTime Date
    {
        get { return DateTime.Today; }
    }         
}

public class Bar : Foo
{
    public string GetDate()
    {
        return DateTime.Today.ToString();
    }
}",
            GetCA1721CSharpResultAt(line: 14, column: 19, identifierName: "Date", otherIdentifierName: "GetDate"));
        }

        [Fact]
        public void Basic_CA1721_PropertyNameDoesNotMatchGetMethodName_Exposed_NoDiagnostic()
        {
            VerifyBasic(@"
Imports System

Public Class Test
    Public ReadOnly Property [Date]() As DateTime
        Get
            Return DateTime.Today
        End Get
    End Property
    Public Function GetTime() As String
        Return Me.Date.ToString()
    End Function 
End Class");
        }

        [Theory, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        [InlineData("Public", "Public")]
        [InlineData("Public", "Protected")]
        [InlineData("Public", "Protected Friend")]
        [InlineData("Protected", "Public")]
        [InlineData("Protected", "Protected")]
        [InlineData("Protected", "Protected Friend")]
        [InlineData("Protected Friend", "Public")]
        [InlineData("Protected Friend", "Protected")]
        [InlineData("Protected Friend", "Protected Friend")]
        public void Basic_CA1721_PropertyNamesMatchGetMethodNames_Exposed_Diagnostics(string propertyAccessibility, string methodAccessibility)
        {
            VerifyBasic(
                string.Format(BasicTestTemplate, propertyAccessibility, methodAccessibility),
                GetCA1721BasicResultAt(
                    line: 5,
                    column: $"    {propertyAccessibility} ReadOnly Property ".Length + 1,
                    identifierName: "Date",
                    otherIdentifierName: "GetDate"));

            VerifyBasic(
                string.Format(BasicNotExternallyVisibleTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Theory]
        [InlineData("Private", "Private")]
        [InlineData("Private", "Friend")]
        [InlineData("Friend", "Private")]
        [InlineData("Friend", "Friend")]
        public void Basic_CA1721_PropertyNamesMatchGetMethodNames_Unexposed_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            VerifyBasic(string.Format(BasicTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Theory]
        [InlineData("Public", "Private")]
        [InlineData("Protected", "Private")]
        [InlineData("Protected Friend", "Private")]
        [InlineData("Public", "Friend")]
        [InlineData("Protected", "Friend")]
        [InlineData("Protected Friend", "Friend")]
        [InlineData("Private", "Public")]
        [InlineData("Private", "Protected")]
        [InlineData("Private", "Protected Friend")]
        [InlineData("Friend", "Public")]
        [InlineData("Friend", "Protected")]
        [InlineData("Friend", "Protected Friend")]
        public void Basic_CA1721_PropertyNamesMatchGetMethodNames_MixedExposure_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            VerifyBasic(string.Format(BasicTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Fact]
        public void Basic_CA1721_PropertyNameMatchesBaseClassGetMethodName_Exposed_Diagnostic()
        {
            VerifyBasic(@"
Imports System

Public Class Foo
    Public Function GetDate() As String
        Return DateTime.Today.ToString()
    End Function
End Class

Public Class Bar 
    Inherits Foo
    Public ReadOnly Property [Date]() As DateTime
        Get
            Return DateTime.Today
        End Get
    End Property
End Class",
            GetCA1721BasicResultAt(line: 12, column: 30, identifierName: "Date", otherIdentifierName: "GetDate"));
        }


        [Fact]
        public void Basic_CA1721_GetMethodNameMatchesBaseClassPropertyName_Exposed_Diagnostic()
        {
            VerifyBasic(@"
Imports System

Public Class Foo
    Public ReadOnly Property [Date]() As DateTime
        Get
            Return DateTime.Today
        End Get
    End Property
End Class
Public Class Bar 
    Inherits Foo
    Public Function GetDate() As String
        Return DateTime.Today.ToString()
    End Function
End Class",
            GetCA1721BasicResultAt(line: 13, column: 21, identifierName: "Date", otherIdentifierName: "GetDate"));
        }

        [Fact, WorkItem(1374, "https://github.com/dotnet/roslyn-analyzers/issues/1374")]
        public void CA1721_TypePropertyNoDiagnostic()
        {
            VerifyCSharp(@"
class T { }
class C
{
    public T Type { get; }
}");

            VerifyBasic(@"
Class T
End Class
Class C
    Public Property Type As T
End Class");
        }

        [Fact, WorkItem(2085, "https://github.com/dotnet/roslyn-analyzers/issues/2085")]
        public void CA1721_StaticAndInstanceMismatchNoDiagnostic()
        {
            VerifyCSharp(@"
public class C1
{
    public int Value { get; }
    public static int GetValue(int i) => i;
}

public class C2
{
    public static int Value { get; }
    public int GetValue(int i) => i;
}
");

            VerifyBasic(@"
Public Class C1
    Public ReadOnly Property Value As Integer

    Public Shared Function GetValue(i As Integer) As Integer
        Return i
    End Function
End Class

Public Class C2
    Public Shared ReadOnly Property Value As Integer

    Public Function GetValue(i As Integer) As Integer
        Return i
    End Function
End Class");
        }

        #region Helpers

        private static DiagnosticResult GetCA1721CSharpResultAt(int line, int column, string identifierName, string otherIdentifierName)
        {
            // Add a public read-only property accessor for positional argument '{0}' of attribute '{1}'.
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsMessage, identifierName, otherIdentifierName);
            return GetCSharpResultAt(line, column, PropertyNamesShouldNotMatchGetMethodsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCA1721BasicResultAt(int line, int column, string identifierName, string otherIdentifierName)
        {
            // Add a public read-only property accessor for positional argument '{0}' of attribute '{1}'.
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsMessage, identifierName, otherIdentifierName);
            return GetBasicResultAt(line, column, PropertyNamesShouldNotMatchGetMethodsAnalyzer.RuleId, message);
        }

        #endregion
    }
}