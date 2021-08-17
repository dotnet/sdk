// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotExposeGenericLists,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotExposeGenericLists,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotExposeGenericListsTests
    {
        [Fact]
        public async Task CA1002_Fields()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public class Class1
    {
        public List<int> field;
    }

    public struct Struct1
    {
        public List<int> field;
    }
}",
                GetCSharpExpectedResult(8, 26, "List<int>", "Class1.field"),
                GetCSharpExpectedResult(13, 26, "List<int>", "Struct1.field"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Namespace Namespace1
    Public Class Class1
        Public field As List(Of Integer)
    End Class

    Public Structure Struct1
        Public field As List(Of Integer)
    End Structure
End Namespace",
                GetBasicExpectedResult(6, 16, "List(Of Integer)", "Class1.field"),
                GetBasicExpectedResult(10, 16, "List(Of Integer)", "Struct1.field"));
        }

        [Fact]
        public async Task CA1002_Methods()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public interface Interface1
    {
        List<int> [|ReturnsList|]();
    }

    public class BaseClass
    {
        public virtual void TakesList(List<string> [|list|]) {}
    }

    public class Class1 : BaseClass, Interface1
    {
        public List<int> ReturnsList() => null; // no issue as this is an inteface implementation
        public override void TakesList(List<string> list) { } // no issue this is an override

        public List<int> [|MultiIssues|](List<string> [|list|], List<List<string>> [|listOfList|]) => null;

        private List<int> PrivateReturnsList() => null;
    }

    public struct Struct1 : Interface1
    {
        public List<int> ReturnsList() => null;

        public List<double> [|GetList|]() => null;
        private List<double> PrivateGetList() => null;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Namespace Namespace1
    Public Interface Interface1
        Function [|ReturnsList|]() As List(Of Integer)
    End Interface

    Public Class BaseClass
        Public Overridable Sub TakesList(ByVal [|list|] As List(Of String))
        End Sub
    End Class

    Public Class Class1
        Inherits BaseClass
        Implements Interface1

        Public Function ReturnsList() As List(Of Integer) Implements Interface1.ReturnsList ' no issue as this is an inteface implementation
            Return Nothing
        End Function

        Public Overrides Sub TakesList(ByVal list As List(Of String)) ' no issue this is an override
        End Sub

        Public Function [|MultiIssues|](ByVal [|list|] As List(Of String), ByVal [|listOfList|] As List(Of List(Of String))) As List(Of Integer)
            Return Nothing
        End Function

        Private Function PrivateReturnsList() As List(Of Integer)
            Return Nothing
        End Function
    End Class

    Public Structure Struct1
        Implements Interface1

        Public Function ReturnsList() As List(Of Integer) Implements Interface1.ReturnsList
            Return Nothing
        End Function

        Public Function [|GetList|]() As List(Of Double)
            Return Nothing
        End Function

        Private Function PrivateGetList() As List(Of Double)
            Return Nothing
        End Function
    End Structure
End Namespace
");
        }

        [Fact]
        public async Task CA1002_Properties()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public interface Interface1
    {
        List<int> [|InterfaceProperty|] { get; }
    }

    public class BaseClass
    {
        public virtual List<string> [|VirtualProperty|] { get; }
    }

    public class Class1 : BaseClass, Interface1
    {
        public List<int> InterfaceProperty { get; } // no issue as this is an inteface implementation
        public override List<string> VirtualProperty { get; } // no issue this is an override

        public List<int> [|AutoProperty|] { get; set; }
        public List<int> [|ReadonlyAutoProperty|] { get; }
        public List<int> [|ReadonlyArrowProperty|] => null;

        private List<int> PrivateAutoProperty { get; set; }
    }

    public struct Struct1 : Interface1
    {
        public List<int> InterfaceProperty { get; }

        public List<int> [|ReadonlyArrowProperty|] => null;
        private List<int> PrivateAutoProperty { get; set; }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic

Namespace Namespace1
    Public Interface Interface1
        ReadOnly Property [|InterfaceProperty|] As List(Of Integer)
    End Interface

    Public Class BaseClass
        Public Overridable ReadOnly Property [|VirtualProperty|] As List(Of String)
    End Class

    Public Class Class1
        Inherits BaseClass
        Implements Interface1

        Public ReadOnly Property InterfaceProperty As List(Of Integer) Implements Interface1.InterfaceProperty ' no issue as this is an inteface implementation
        Public Overrides ReadOnly Property VirtualProperty As List(Of String) ' no issue this is an override

        Public Property [|AutoProperty|] As List(Of Integer)
        Public ReadOnly Property [|ReadonlyAutoProperty|] As List(Of Integer)

        Private Property PrivateAutoProperty As List(Of Integer)
    End Class

    Public Structure Struct1
        Implements Interface1

        Public ReadOnly Property InterfaceProperty As List(Of Integer) Implements Interface1.InterfaceProperty

        Public ReadOnly Property [|ReadonlyAutoProperty|] As List(Of Integer)

        Private Property PrivateAutoProperty As List(Of Integer)
    End Structure
End Namespace
");
        }

        [Fact]
        public async Task CA1002_ExtensionMethods()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

namespace Namespace1
{
    public static class Helper
    {
        public static bool Ext1(this List<int> list) => true;
        public static List<int> [|Ext2|](this string s) => null;
        public static int Ext3(this bool b, List<string> [|l|]) => 42;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic
Imports System.Runtime.CompilerServices

Namespace Namespace1
    Public Module Helper
        <Extension()>
        Public Sub Ext1(ByVal list As List(Of String))
        End Sub

        <Extension()>
        Public Function [|Ext2|](ByVal s As String) As List(Of Integer)
            Return Nothing
        End Function

        <Extension()>
        Public Function Ext2(ByVal s As String, ByVal [|list|] As List(Of Integer)) As Integer
            Return 42
        End Function
    End Module
End Namespace
");
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
        // Specific analyzer option
        [InlineData("internal", "dotnet_code_quality.CA1002.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1002.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1002.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1002.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOption(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System.Collections.Generic;
public class OuterClass
{{
    {accessibility} List<int> [|field|];
    {accessibility} List<int> [|ReturnsList|]() => null;
    {accessibility} List<int> [|AutoProperty|] {{ get; set; }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync();
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
        [InlineData("Friend", "dotnet_code_quality.CA1002.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1002.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1002.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1002.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOption(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System.Collections.Generic
Public Class OuterClass
    Public Class C
        {accessibility} [|field|] As List(Of Integer)

        {accessibility} Function [|ReturnsList|]() As List(Of Integer)
            Return Nothing
        End Function

        {accessibility} Property [|AutoProperty|] As List(Of Integer)
    End Class
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpExpectedResult(int line, int col, string returnTypeName, string typeDotMemberName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, col)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(returnTypeName, typeDotMemberName);

        private static DiagnosticResult GetBasicExpectedResult(int line, int col, string returnTypeName, string typeDotMemberName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, col)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(returnTypeName, typeDotMemberName);
    }
}
