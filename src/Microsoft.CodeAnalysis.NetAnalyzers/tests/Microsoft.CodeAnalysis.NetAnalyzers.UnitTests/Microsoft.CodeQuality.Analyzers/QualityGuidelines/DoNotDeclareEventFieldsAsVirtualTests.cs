// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotDeclareEventFieldsAsVirtual,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public class DoNotDeclareEventFieldsAsVirtualTests
    {
        [Fact]
        public async Task EventFieldVirtual_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public virtual event EventHandler ThresholdReached;
}",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic().WithLocation(5, 39).WithArguments("ThresholdReached"));
#pragma warning restore RS0030 // Do not used banned APIs
        }

        [Fact]
        public async Task EventPropertyVirtual_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public virtual event EventHandler ThresholdReached
    {
        add
        {
        }
        remove
        {
        }
    }
}");
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
        [InlineData("internal", "dotnet_code_quality.CA1070.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1070.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1070.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1070.api_surface_2 = private")]
        public async Task CSharp_ApiSurfaceOptionAsync(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;
public class OuterClass
{{
    {accessibility} virtual event EventHandler [|ThresholdReached|];
}}"
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
