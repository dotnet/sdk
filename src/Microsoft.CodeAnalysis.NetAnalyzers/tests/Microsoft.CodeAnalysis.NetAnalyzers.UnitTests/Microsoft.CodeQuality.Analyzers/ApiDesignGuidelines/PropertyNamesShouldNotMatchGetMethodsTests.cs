// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertyNamesShouldNotMatchGetMethodsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpPropertyNamesShouldNotMatchGetMethodsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.PropertyNamesShouldNotMatchGetMethodsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicPropertyNamesShouldNotMatchGetMethodsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class PropertyNamesShouldNotMatchGetMethodsTests
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

        [Fact]
        public async Task CSharp_CA1721_PropertyNameDoesNotMatchGetMethodName_Exposed_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_CA1721_PropertyNamesMatchGetMethodNames_Exposed_Diagnostics(string propertyAccessibility, string methodAccessibility)
        {
            await VerifyCS.VerifyAnalyzerAsync(
                string.Format(CultureInfo.InvariantCulture, CSharpTestTemplate, propertyAccessibility, methodAccessibility),
                GetCA1721CSharpResultAt(
                    line: 6,
                    column: $"    {propertyAccessibility} DateTime ".Length + 1,
                    identifierName: "Date",
                    otherIdentifierName: "GetDate"));

            await VerifyCS.VerifyAnalyzerAsync(
                string.Format(CultureInfo.InvariantCulture, CSharpNotExternallyVisibleTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Theory]
        [InlineData("private", "private")]
        [InlineData("private", "internal")]
        [InlineData("internal", "private")]
        [InlineData("internal", "internal")]
        [InlineData("", "")]
        public async Task CSharp_CA1721_PropertyNamesMatchGetMethodNames_Unexposed_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(CultureInfo.InvariantCulture, CSharpTestTemplate, propertyAccessibility, methodAccessibility));
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
        public async Task CSharp_CA1721_PropertyNamesMatchGetMethodNames_MixedExposure_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            await VerifyCS.VerifyAnalyzerAsync(string.Format(CultureInfo.InvariantCulture, CSharpTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Fact]
        public async Task CSharp_CA1721_PropertyNameMatchesBaseClassGetMethodName_Exposed_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class SomeClass
{
    public string GetDate()
    {
        return DateTime.Today.ToString();
    }
}

public class SometOtherClass : SomeClass
{
    public DateTime Date
    {
        get { return DateTime.Today; }
    }         
}",
            GetCA1721CSharpResultAt(line: 14, column: 21, identifierName: "Date", otherIdentifierName: "GetDate"));
        }

        [Fact]
        public async Task CSharp_CA1721_GetMethodNameMatchesBaseClassPropertyName_Exposed_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class SomeClass
{
    public DateTime Date
    {
        get { return DateTime.Today; }
    }         
}

public class SometOtherClass : SomeClass
{
    public string GetDate()
    {
        return DateTime.Today.ToString();
    }
}",
            GetCA1721CSharpResultAt(line: 14, column: 19, identifierName: "Date", otherIdentifierName: "GetDate"));
        }

        [Fact]
        public async Task Basic_CA1721_PropertyNameDoesNotMatchGetMethodName_Exposed_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task Basic_CA1721_PropertyNamesMatchGetMethodNames_Exposed_Diagnostics(string propertyAccessibility, string methodAccessibility)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                string.Format(CultureInfo.InvariantCulture, BasicTestTemplate, propertyAccessibility, methodAccessibility),
                GetCA1721BasicResultAt(
                    line: 5,
                    column: $"    {propertyAccessibility} ReadOnly Property ".Length + 1,
                    identifierName: "Date",
                    otherIdentifierName: "GetDate"));

            await VerifyVB.VerifyAnalyzerAsync(
                string.Format(CultureInfo.InvariantCulture, BasicNotExternallyVisibleTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Theory]
        [InlineData("Private", "Private")]
        [InlineData("Private", "Friend")]
        [InlineData("Friend", "Private")]
        [InlineData("Friend", "Friend")]
        public async Task Basic_CA1721_PropertyNamesMatchGetMethodNames_Unexposed_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            await VerifyVB.VerifyAnalyzerAsync(string.Format(CultureInfo.InvariantCulture, BasicTestTemplate, propertyAccessibility, methodAccessibility));
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
        public async Task Basic_CA1721_PropertyNamesMatchGetMethodNames_MixedExposure_NoDiagnostics(string propertyAccessibility, string methodAccessibility)
        {
            await VerifyVB.VerifyAnalyzerAsync(string.Format(CultureInfo.InvariantCulture, BasicTestTemplate, propertyAccessibility, methodAccessibility));
        }

        [Fact]
        public async Task Basic_CA1721_PropertyNameMatchesBaseClassGetMethodName_Exposed_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class SomeClass
    Public Function GetDate() As String
        Return DateTime.Today.ToString()
    End Function
End Class

Public Class SometOtherClass 
    Inherits SomeClass
    Public ReadOnly Property [Date]() As DateTime
        Get
            Return DateTime.Today
        End Get
    End Property
End Class",
            GetCA1721BasicResultAt(line: 12, column: 30, identifierName: "Date", otherIdentifierName: "GetDate"));
        }

        [Fact]
        public async Task Basic_CA1721_GetMethodNameMatchesBaseClassPropertyName_Exposed_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class SomeClass
    Public ReadOnly Property [Date]() As DateTime
        Get
            Return DateTime.Today
        End Get
    End Property
End Class
Public Class SometOtherClass 
    Inherits SomeClass
    Public Function GetDate() As String
        Return DateTime.Today.ToString()
    End Function
End Class",
            GetCA1721BasicResultAt(line: 13, column: 21, identifierName: "Date", otherIdentifierName: "GetDate"));
        }

        [Fact, WorkItem(1374, "https://github.com/dotnet/roslyn-analyzers/issues/1374")]
        public async Task CA1721_TypePropertyNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class T { }
class C
{
    public T Type { get; }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Class T
End Class
Class C
    Public Property Type As T
End Class");
        }

        [Fact, WorkItem(2085, "https://github.com/dotnet/roslyn-analyzers/issues/2085")]
        public async Task CA1721_StaticAndInstanceMismatchNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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

        [Fact, WorkItem(2914, "https://github.com/dotnet/roslyn-analyzers/issues/2914")]
        public async Task CA1721_OverrideNoDiagnosticButVirtualDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class BaseClass
{
    public virtual int Value { get; }
    public virtual int GetValue(int i) => i;
}

public class C1 : BaseClass
{
    public override int Value => 42;
}

public class C2 : BaseClass
{
    public override int GetValue(int i) => i * 2;
}

public class C3 : BaseClass
{
    public override int Value => 42;
    public override int GetValue(int i) => i * 2;
}
",
            GetCA1721CSharpResultAt(line: 4, column: 24, identifierName: "Value", otherIdentifierName: "GetValue"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class BaseClass
    Public Overridable ReadOnly Property Value As Integer

    Public Overridable Function GetValue(ByVal i As Integer) As Integer
        Return i
    End Function
End Class

Public Class C1
    Inherits BaseClass

    Public Overrides ReadOnly Property Value As Integer
        Get
            Return 42
        End Get
    End Property
End Class

Public Class C2
    Inherits BaseClass

    Public Overrides Function GetValue(ByVal i As Integer) As Integer
        Return i * 2
    End Function
End Class

Public Class C3
    Inherits BaseClass

    Public Overrides ReadOnly Property Value As Integer
        Get
            Return 42
        End Get
    End Property

    Public Overrides Function GetValue(ByVal i As Integer) As Integer
        Return i * 2
    End Function
End Class
",
        GetCA1721BasicResultAt(line: 3, column: 42, identifierName: "Value", otherIdentifierName: "GetValue"));
        }

        [Fact, WorkItem(2914, "https://github.com/dotnet/roslyn-analyzers/issues/2914")]
        public async Task CA1721_OverrideWithLocalMemberDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class BaseClass1
{
    public virtual int Value { get; }
}

public class C1 : BaseClass1
{
    public override int Value => 42;
    public int GetValue(int i) => i;
}

public class BaseClass2
{
    public virtual int GetValue(int i) => i;
}

public class C2 : BaseClass2
{
    public int Value => 42;
    public override int GetValue(int i) => i * 2;
}
",
            GetCA1721CSharpResultAt(line: 10, column: 16, identifierName: "Value", otherIdentifierName: "GetValue"),
            GetCA1721CSharpResultAt(line: 20, column: 16, identifierName: "Value", otherIdentifierName: "GetValue"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class BaseClass1
    Public Overridable ReadOnly Property Value As Integer
End Class

Public Class C1
    Inherits BaseClass1

    Public Overrides ReadOnly Property Value As Integer
        Get
            Return 42
        End Get
    End Property

    Public Function GetValue(ByVal i As Integer) As Integer
        Return i
    End Function
End Class

Public Class BaseClass2
    Public Overridable Function GetValue(ByVal i As Integer) As Integer
        Return i
    End Function
End Class

Public Class C2
    Inherits BaseClass2

    Public ReadOnly Property Value As Integer
        Get
            Return 42
        End Get
    End Property

    Public Overrides Function GetValue(ByVal i As Integer) As Integer
        Return i * 2
    End Function
End Class

",
            GetCA1721BasicResultAt(line: 15, column: 21, identifierName: "Value", otherIdentifierName: "GetValue"),
            GetCA1721BasicResultAt(line: 29, column: 30, identifierName: "Value", otherIdentifierName: "GetValue"));
        }

        [Fact, WorkItem(2914, "https://github.com/dotnet/roslyn-analyzers/issues/2914")]
        public async Task CA1721_OverrideMultiLevelDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class MyBaseClass
{
    public virtual int GetValue(int i) => i;
    public virtual int Something { get; }
}

public class MyClass : MyBaseClass
{
    public virtual int Value { get; }
    public virtual int GetSomething(int i) => i;
}

public class MySubClass : MyClass
{
    public override int GetValue(int i) => 2;
    public override int Value => 2;
    public override int GetSomething(int i) => 2;
    public override int Something => 2;
}
",
            GetCA1721CSharpResultAt(line: 10, column: 24, identifierName: "Value", otherIdentifierName: "GetValue"),
            GetCA1721CSharpResultAt(line: 11, column: 24, identifierName: "Something", otherIdentifierName: "GetSomething"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class MyBaseClass
    Public Overridable Function GetValue(ByVal i As Integer) As Integer
        Return i
    End Function

    Public Overridable ReadOnly Property Something As Integer
End Class

Public Class [MyClass]
    Inherits MyBaseClass

    Public Overridable ReadOnly Property Value As Integer

    Public Overridable Function GetSomething(ByVal i As Integer) As Integer
        Return i
    End Function
End Class

Public Class MySubClass
    Inherits [MyClass]

    Public Overrides Function GetValue(ByVal i As Integer) As Integer
        Return 2
    End Function

    Public Overrides ReadOnly Property Value As Integer
        Get
            Return 2
        End Get
    End Property

    Public Overrides Function GetSomething(ByVal i As Integer) As Integer
        Return 2
    End Function

    Public Overrides ReadOnly Property Something As Integer
        Get
            Return 2
        End Get
    End Property
End Class
",
            GetCA1721BasicResultAt(line: 13, column: 42, identifierName: "Value", otherIdentifierName: "GetValue"),
            GetCA1721BasicResultAt(line: 15, column: 33, identifierName: "Something", otherIdentifierName: "GetSomething"));
        }

        [Fact, WorkItem(2956, "https://github.com/dotnet/roslyn-analyzers/issues/2956")]
        public async Task CA1721_Obsolete_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C1
{
    [Obsolete(""Use the method."")]
    public int PropertyValue => 1;

    public int GetPropertyValue()
    {
        return 1;
    }
}

public class C2
{
    public int PropertyValue => 1;

    [Obsolete(""Use the property."")]
    public int GetPropertyValue()
    {
        return 1;
    }
}

public class C3
{
    [Obsolete(""Deprecated"")]
    public int PropertyValue => 1;

    [Obsolete(""Deprecated"")]
    public int GetPropertyValue()
    {
        return 1;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C1
    <Obsolete(""Use the method."")>
    Public ReadOnly Property PropertyValue As Integer
        Get
            Return 1
        End Get
    End Property

    Public Function GetPropertyValue() As Integer
        Return 1
    End Function
End Class

Public Class C2
    Public ReadOnly Property PropertyValue As Integer
        Get
            Return 1
        End Get
    End Property

    <Obsolete(""Use the property."")>
    Public Function GetPropertyValue() As Integer
        Return 1
    End Function
End Class

Public Class C3
    <Obsolete(""Deprecated"")>
    Public ReadOnly Property PropertyValue As Integer
        Get
            Return 1
        End Get
    End Property

    <Obsolete(""Deprecated"")>
    Public Function GetPropertyValue() As Integer
        Return 1
    End Function
End Class");
        }

        [Fact, WorkItem(2956, "https://github.com/dotnet/roslyn-analyzers/issues/2956")]
        public async Task CA1721_OnlyOneOverloadObsolete_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int PropertyValue => 1;

    [Obsolete(""Use the property."")]
    public int GetPropertyValue()
    {
        return 1;
    }

    public int GetPropertyValue(int i)
    {
        return i;
    }
}",
                GetCA1721CSharpResultAt(6, 16, "PropertyValue", "GetPropertyValue"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public ReadOnly Property PropertyValue As Integer
        Get
            Return 1
        End Get
    End Property

    <Obsolete(""Use the property."")>
    Public Function GetPropertyValue() As Integer
        Return 1
    End Function

    Public Function GetPropertyValue(i As Integer) As Integer
        Return i
    End Function
End Class",
                GetCA1721BasicResultAt(5, 30, "PropertyValue", "GetPropertyValue"));
        }

        [Fact, WorkItem(2956, "https://github.com/dotnet/roslyn-analyzers/issues/2956")]
        public async Task CA1721_AllOverloadsObsolete_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int PropertyValue => 1;

    [Obsolete(""Use the property."")]
    public int GetPropertyValue()
    {
        return 1;
    }

    [Obsolete(""Use the property."")]
    public int GetPropertyValue(int i)
    {
        return i;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public ReadOnly Property PropertyValue As Integer
        Get
            Return 1
        End Get
    End Property

    <Obsolete(""Use the property."")>
    Public Function GetPropertyValue() As Integer
        Return 1
    End Function

    <Obsolete(""Use the property."")>
    Public Function GetPropertyValue(i As Integer) As Integer
        Return i
    End Function
End Class");
        }

        #region Helpers

        private static DiagnosticResult GetCA1721CSharpResultAt(int line, int column, string identifierName, string otherIdentifierName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(identifierName, otherIdentifierName);

        private static DiagnosticResult GetCA1721BasicResultAt(int line, int column, string identifierName, string otherIdentifierName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(identifierName, otherIdentifierName);

        #endregion
    }
}
