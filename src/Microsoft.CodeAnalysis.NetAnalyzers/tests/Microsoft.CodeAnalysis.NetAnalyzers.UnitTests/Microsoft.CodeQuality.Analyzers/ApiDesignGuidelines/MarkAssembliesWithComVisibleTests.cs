// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithComVisibleAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAssembliesWithComVisibleFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class MarkAllAssembliesWithComVisibleTests
    {
        [TestMethod]
        public async Task NoTypesComVisibleMissingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("");
        }

        [TestMethod]
        public async Task NoTypesComVisibleTrueAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(true)]");
        }

        [TestMethod]
        public async Task NoTypesComVisibleFalseAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]");
        }

        [TestMethod]
        public async Task PublicTypeComVisibleMissingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
}",
                GetAddComVisibleFalseResult());
        }

        [TestMethod]
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

        [TestMethod]
        public async Task PublicTypeComVisibleFalseAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]

public class C
{
}");
        }

        [TestMethod]
        public async Task InternalTypeComVisibleMissingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class C
{
}");
        }

        [TestMethod]
        public async Task InternalTypeComVisibleTrueAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

[assembly: ComVisible(true)]

internal class C
{
}");
        }

        [TestMethod]
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
