// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotOverloadOperatorEqualsOnReferenceTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotOverloadOperatorEqualsOnReferenceTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class DoNotOverloadOperatorEqualsOnReferenceTypesTests
    {
        [TestMethod]
        public async Task OperatorEqual_ReferenceType_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static bool operator ==(C left, C right) => true;
    public static bool operator !=(C left, C right) => true;
}",
                VerifyCS.Diagnostic().WithSpan(4, 33, 4, 35).WithArguments("C"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator
End Class",
                VerifyVB.Diagnostic().WithSpan(3, 28, 3, 29).WithArguments("C"));
        }

        [TestMethod]
        public async Task OperatorEqual_ValueType_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct C
{
    public static bool operator ==(C left, C right) => true;
    public static bool operator !=(C left, C right) => true;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure C
    Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator
End Structure");
        }

        [TestMethod]
        public async Task OperatorEqualAndAdditionSubtraction_ReferenceType_DiagnosticAsync()
        {
            // Doc states that if type behaves as a value-type and has addition/subtraction it might be safe.

            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public static bool operator [|==|](C left, C right) => true;
    public static bool operator !=(C left, C right) => true;

    public static C operator +(C left, C right) => left;
    public static C operator -(C left, C right) => left;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Shared Operator [|=|](ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator +(ByVal left As C, ByVal right As C) As C
        Return left
    End Operator

    Public Shared Operator -(ByVal left As C, ByVal right As C) As C
        Return left
    End Operator
End Class");
        }

        [TestMethod]
        // General analyzer option
        [DataRow("public", "dotnet_code_quality.api_surface = public")]
        [DataRow("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("public", "dotnet_code_quality.api_surface = all")]
        [DataRow("protected", "dotnet_code_quality.api_surface = public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.api_surface = internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = private, internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = all")]
        [DataRow("private", "dotnet_code_quality.api_surface = private")]
        [DataRow("private", "dotnet_code_quality.api_surface = private, public")]
        [DataRow("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [DataRow("internal", "dotnet_code_quality.CA1046.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [DataRow("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1046.api_surface = all")]
        // Case-insensitive analyzer option
        [DataRow("internal", "DOTNET_code_quality.CA1046.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1046.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOptionAsync(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class OuterClass
{{
    {accessibility} class C
    {{
        public static bool operator [|==|](C left, C right) => true;
        public static bool operator !=(C left, C right) => true;
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        // General analyzer option
        [DataRow("Public", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = All")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = All")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [DataRow("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [DataRow("Friend", "dotnet_code_quality.CA1046.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [DataRow("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1046.api_surface = All")]
        // Case-insensitive analyzer option
        [DataRow("Friend", "DOTNET_code_quality.CA1046.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1046.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOptionAsync(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class OuterClass
    {accessibility} Class C
        Public Shared Operator [|=|](ByVal left As C, ByVal right As C) As Boolean
            Return True
        End Operator

        Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
            Return True
        End Operator
    End Class
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("internal")]
        [DataRow("private")]

        public async Task CSharp_OperatorEqual_InternalReferenceType_NoDiagnosticAsync(string accessibility)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
public class OuterClass
{{
    {accessibility} class C
    {{
        public static bool operator ==(C left, C right) => true;
        public static bool operator !=(C left, C right) => true;
    }}
}}");
        }

        [TestMethod]
        [DataRow("Friend")]
        [DataRow("Private")]

        public async Task VisualBasic_OperatorEqual_InternalReferenceType_NoDiagnosticAsync(string accessibility)
        {
            await VerifyVB.VerifyAnalyzerAsync($@"
Public Class OuterClass
    {accessibility} Class C
        Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
            Return True
        End Operator

        Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
            Return True
        End Operator
    End Class
End Class");
        }

        [TestMethod]
        public async Task OperatorEqual_IEquatable_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C : IEquatable<C>
{
    public bool Equals(C other) => true;
    public static bool operator ==(C left, C right) => true;
    public static bool operator !=(C left, C right) => true;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class C
    Implements IEquatable(Of C)

    Public Function Equals(ByVal other As C) As Boolean Implements IEquatable(Of C).Equals
        Return True
    End Function

    Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator
End Class");
        }

        [TestMethod]
        public async Task OperatorEqual_OverrideObjectEquals_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public override bool Equals(object obj) => true;

    public static bool operator ==(C left, C right) => true;
    public static bool operator !=(C left, C right) => true;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overrides Function Equals(ByVal obj As Object) As Boolean
        Return True
    End Function

    Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator
End Class
");
        }

        [TestMethod]
        public async Task OperatorEqual_ImplementIComparable_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C : IComparable
{
    public int CompareTo(object obj) => 0;

    public static bool operator ==(C left, C right) => true;
    public static bool operator !=(C left, C right) => true;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Implements IComparable

    Public Function CompareTo(ByVal obj As Object) As Integer Implements IComparable.CompareTo
        Return 0
    End Function

    Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator
End Class
");
        }

        [TestMethod]
        public async Task OperatorEqual_ImplementIComparableT_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C : IComparable<C>
{
    public int CompareTo(C other) => 0;

    public static bool operator ==(C left, C right) => true;
    public static bool operator !=(C left, C right) => true;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Implements IComparable(Of C)

    Public Function CompareTo(ByVal other As C) As Integer Implements IComparable(Of C).CompareTo
        Return 0
    End Function

    Public Shared Operator =(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator

    Public Shared Operator <>(ByVal left As C, ByVal right As C) As Boolean
        Return True
    End Operator
End Class
");
        }
    }
}
