// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidExcessiveParametersOnGenericTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidExcessiveParametersOnGenericTypes,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class AvoidExcessiveParametersOnGenericTypesTests
    {
        [TestMethod]
        public async Task ClassWithMoreThanTwoTypeParameters_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C<T1, T2, T3> {}",
                VerifyCS.Diagnostic().WithSpan(2, 14, 2, 15).WithArguments("C", AvoidExcessiveParametersOnGenericTypes.MaximumNumberOfTypeParameters));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C(Of T1, T2, T3)
End Class",
                VerifyVB.Diagnostic().WithSpan(2, 14, 2, 15).WithArguments("C", AvoidExcessiveParametersOnGenericTypes.MaximumNumberOfTypeParameters));
        }

        [TestMethod]
        public async Task InterfaceWithMoreThanTwoTypeParameters_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface [|I|]<T1, T2, T3> {}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface [|I|](Of T1, T2, T3)
End Interface");
        }

        [TestMethod]
        public async Task StructWithMoreThanTwoTypeParameters_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct [|S|]<T1, T2, T3> {}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure [|S|](Of T1, T2, T3)
End Structure");
        }

        [TestMethod]
        public async Task ClassImplementsInterfaceWithMoreThanTwoTypeParameters_DiagnosticAsync()
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

        [TestMethod]
        public async Task ClassInheritsClassWithMoreThanTwoTypeParameters_DiagnosticAsync()
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
        [DataRow("internal", "dotnet_code_quality.CA1005.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [DataRow("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1005.api_surface = all")]
        // Case-insensitive analyzer option
        [DataRow("internal", "DOTNET_code_quality.CA1005.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1005.api_surface_2 = private")]
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
    {accessibility} class [|C|]<T1, T2, T3> {{ }}
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
        [DataRow("Friend", "dotnet_code_quality.CA1005.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [DataRow("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1005.api_surface = All")]
        // Case-insensitive analyzer option
        [DataRow("Friend", "DOTNET_code_quality.CA1005.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1005.api_surface_2 = Private")]
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
    {accessibility} Class [|C|](Of T1, T2, T3)
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
    }
}
