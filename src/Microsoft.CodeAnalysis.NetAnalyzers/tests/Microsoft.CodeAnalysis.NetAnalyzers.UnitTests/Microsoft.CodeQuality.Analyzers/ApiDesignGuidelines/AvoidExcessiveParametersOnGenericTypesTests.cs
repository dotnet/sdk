// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidExcessiveParametersOnGenericTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidExcessiveParametersOnGenericTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class AvoidExcessiveParametersOnGenericTypesTests
    {
        [Fact]
        public async Task ClassWithMoreThanTwoTypeParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C<T1, T2, T3> {}",
                VerifyCS.Diagnostic().WithSpan(2, 14, 2, 15).WithArguments("C", AvoidExcessiveParametersOnGenericTypes.MaximumNumberOfTypeParameters));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C(Of T1, T2, T3)
End Class",
                VerifyVB.Diagnostic().WithSpan(2, 14, 2, 15).WithArguments("C", AvoidExcessiveParametersOnGenericTypes.MaximumNumberOfTypeParameters));
        }

        [Fact]
        public async Task InterfaceWithMoreThanTwoTypeParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface [|I|]<T1, T2, T3> {}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface [|I|](Of T1, T2, T3)
End Interface");
        }

        [Fact]
        public async Task StructWithMoreThanTwoTypeParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct [|S|]<T1, T2, T3> {}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure [|S|](Of T1, T2, T3)
End Structure");
        }

        [Fact]
        public async Task ClassImplementsInterfaceWithMoreThanTwoTypeParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class [|I|]<T1, T2, T3> {}

public class [|C|]<T1, T2, T3> : I<T1, T2, T3> {}

public class C2 : I<int, string, object> {}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface [|I|](Of T1, T2, T3)
End Interface

Public Class [|C|](Of T1, T2, T3)
    Implements I(Of T1, T2, T3)
End Class

Public Class C2
    Implements I(Of Integer, String, Object)
End Class");
        }

        [Fact]
        public async Task ClassInheritsClassWithMoreThanTwoTypeParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class [|C|]<T1, T2, T3> {}

public class [|C2|]<T1, T2, T3> : C<T1, T2, T3> {}

public class C3 : C<int, string, object> {}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class [|C|](Of T1, T2, T3)
End Class

Public Class [|C2|](Of T1, T2, T3)
    Inherits C(Of T1, T2, T3)
End Class

Public Class C3
    Inherits C(Of Integer, String, Object)
End Class");
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
        [InlineData("internal", "dotnet_code_quality.CA1005.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1005.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1005.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1005.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOption(string accessibility, string editorConfigText)
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
    {accessibility} class [|C|]<T1, T2, T3> {{ }}
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
        [InlineData("Friend", "dotnet_code_quality.CA1005.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1005.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1005.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1005.api_surface_2 = Private")]
        public async Task VisualBasic_ApiSurfaceOption(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Public Class OuterClass
    {accessibility} Class [|C|](Of T1, T2, T3)
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
    }
}
