// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidEmptyInterfacesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpAvoidEmptyInterfacesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidEmptyInterfacesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicAvoidEmptyInterfacesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class AvoidEmptyInterfacesTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new AvoidEmptyInterfacesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AvoidEmptyInterfacesAnalyzer();
        }

        [Fact]
        public void TestCSharpEmptyPublicInterface()
        {
            VerifyCSharp(@"
public interface I
{
}", CreateCSharpResult(2, 18));
        }

        [Fact]
        public void TestBasicEmptyPublicInterface()
        {
            VerifyBasic(@"
Public Interface I
End Interface", CreateBasicResult(2, 18));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestCSharpEmptyInternalInterface()
        {
            VerifyCSharp(@"
interface I
{
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void TestBasicEmptyInternalInterface()
        {
            VerifyBasic(@"
Interface I
End Interface");
        }

        [Fact]
        public void TestCSharpNonEmptyInterface1()
        {
            VerifyCSharp(@"
public interface I
{
    void DoStuff();
}");
        }

        [Fact]
        public void TestBasicNonEmptyInterface1()
        {
            VerifyBasic(@"
Public Interface I
    Function GetStuff() as Integer
End Interface");
        }

        [Fact]
        public void TestCSharpEmptyInterfaceWithNoInheritedMembers()
        {
            VerifyCSharp(@"
public interface I : IBase
{
}

public interface IBase { }", CreateCSharpResult(2, 18), CreateCSharpResult(6, 18));
        }

        [Fact]
        public void TestBasicEmptyInterfaceWithNoInheritedMembers()
        {
            VerifyBasic(@"
Public Interface I
    Inherits IBase
End Interface

Public Interface IBase
End Interface", CreateBasicResult(2, 18), CreateBasicResult(6, 18));
        }

        [Fact]
        public void TestCSharpEmptyInterfaceWithInheritedMembers()
        {
            VerifyCSharp(@"
public interface I : IBase
{
}

public interface IBase 
{
    void DoStuff(); 
}");
        }

        [Fact]
        public void TestBasicEmptyInterfaceWithInheritedMembers()
        {
            VerifyBasic(@"
Public Interface I
    Inherits IBase
End Interface

Public Interface IBase
    Sub DoStuff()
End Interface");
        }

        [Theory]
        // General analyzer option
        [InlineData("public", "dotnet_code_quality.api_surface = public")]
        [InlineData("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("public", "dotnet_code_quality.api_surface = all")]
        [InlineData("protected", "dotnet_code_quality.api_surface = public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.api_surface = internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = private, internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = all")]
        [InlineData("private", "dotnet_code_quality.api_surface = private")]
        [InlineData("private", "dotnet_code_quality.api_surface = private, public")]
        [InlineData("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [InlineData("internal", "dotnet_code_quality.CA1040.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1040.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1040.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1040.api_surface_2 = private")]
        public void TestCSharpEmptyInterface_AnalyzerOptions_Diagnostic(string accessibility, string editorConfigText)
        {
            VerifyCSharp($@"
public class C
{{
    {accessibility} interface I {{ }}
}}",
                GetEditorConfigAdditionalFile(editorConfigText),
                CreateCSharpResult(4, 16 + accessibility.Length));
        }

        [Theory]
        // General analyzer option
        [InlineData("Public", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = All")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = All")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [InlineData("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [InlineData("Friend", "dotnet_code_quality.CA1040.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1040.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1040.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1040.api_surface_2 = Private")]
        public void TestBasicEmptyInterface_AnalyzerOptions_Diagnostic(string accessibility, string editorConfigText)
        {
            VerifyBasic($@"
Public Class C
    {accessibility} Interface I
    End Interface
End Class",
                GetEditorConfigAdditionalFile(editorConfigText),
                CreateBasicResult(3, 16 + accessibility.Length));
        }

        [Theory]
        [InlineData("public", "dotnet_code_quality.api_surface = private")]
        [InlineData("public", "dotnet_code_quality.CA1040.api_surface = internal, private")]
        [InlineData("public", "dotnet_code_quality.Design.api_surface = internal, private")]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1040.api_surface = private")]
        public void TestCSharpEmptyInterface_AnalyzerOptions_NoDiagnostic(string accessibility, string editorConfigText)
        {
            VerifyCSharp($@"
public class C
{{
    {accessibility} interface I {{ }}
}}",
                GetEditorConfigAdditionalFile(editorConfigText));
        }

        [Theory]
        [InlineData("Public", "dotnet_code_quality.api_surface = Private")]
        [InlineData("Public", "dotnet_code_quality.CA1040.api_surface = Friend, Private")]
        [InlineData("Public", "dotnet_code_quality.Design.api_surface = Friend, Private")]
        [InlineData("Public", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1040.api_surface = Private")]
        public void TestBasicEmptyInterface_AnalyzerOptions_NoDiagnostic(string accessibility, string editorConfigText)
        {
            VerifyBasic($@"
Public Class C
    {accessibility} Interface I
    End Interface
End Class",
                GetEditorConfigAdditionalFile(editorConfigText));
        }

        private static DiagnosticResult CreateCSharpResult(int line, int col)
        {
            return GetCSharpResultAt(line, col, AvoidEmptyInterfacesAnalyzer.RuleId, MicrosoftCodeQualityAnalyzersResources.AvoidEmptyInterfacesMessage);
        }

        private static DiagnosticResult CreateBasicResult(int line, int col)
        {
            return GetBasicResultAt(line, col, AvoidEmptyInterfacesAnalyzer.RuleId, MicrosoftCodeQualityAnalyzersResources.AvoidEmptyInterfacesMessage);
        }
    }
}