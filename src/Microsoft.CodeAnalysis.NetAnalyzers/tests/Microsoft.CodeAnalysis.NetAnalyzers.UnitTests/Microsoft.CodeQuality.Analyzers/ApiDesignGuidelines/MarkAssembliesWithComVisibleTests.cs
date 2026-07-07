// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithComVisibleAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithComVisibleFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class MarkAllAssembliesWithComVisibleTests
    {
        [Fact]
        public async Task NoTypesComVisibleMissingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("");
        }

        [Fact]
        public async Task NoTypesComVisibleTrueAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(true)]");
        }

        [Fact]
        public async Task NoTypesComVisibleFalseAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]");
        }

        [Fact]
        public async Task PublicTypeComVisibleMissingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
}",
                GetAddComVisibleFalseResult());
        }

        [Fact]
        public async Task PublicTypeComVisibleTrueAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(true)]

public class C
{
}",
                GetExposeIndividualTypesResult());
        }

        [Fact]
        public async Task PublicTypeComVisibleFalseAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]

public class C
{
}");
        }

        [Fact]
        public async Task InternalTypeComVisibleMissingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class C
{
}");
        }

        [Fact]
        public async Task InternalTypeComVisibleTrueAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(true)]

internal class C
{
}");
        }

        [Fact]
        public async Task InternalTypeComVisibleFalseAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]

internal class C
{
}");
        }

        private static DiagnosticResult GetExposeIndividualTypesResult()
            => VerifyCS.Diagnostic(MarkAssembliesWithComVisibleAnalyzer.RuleChangeComVisible)
                .WithArguments("TestProject");

        private static DiagnosticResult GetAddComVisibleFalseResult()
            => VerifyCS.Diagnostic(MarkAssembliesWithComVisibleAnalyzer.RuleAddComVisible)
                .WithArguments("TestProject");
    }
}
