// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotDeclareEventFieldsAsVirtual,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    [TestClass]
    public class DoNotDeclareEventFieldsAsVirtualTests
    {
        [TestMethod]
        public async Task EventFieldVirtual_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public virtual event EventHandler ThresholdReached;
}",
#pragma warning disable RS0030 // Do not use banned APIs
                VerifyCS.Diagnostic().WithLocation(5, 39).WithArguments("ThresholdReached"));
#pragma warning restore RS0030 // Do not use banned APIs
        }

        [TestMethod]
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
        // Specific analyzer option
        [DataRow("internal", "dotnet_code_quality.CA1070.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [DataRow("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1070.api_surface = all")]
        // Case-insensitive analyzer option
        [DataRow("internal", "DOTNET_code_quality.CA1070.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("internal", @"dotnet_code_quality.api_surface = all
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
            }.RunAsync(CancellationToken.None);
        }
    }
}
