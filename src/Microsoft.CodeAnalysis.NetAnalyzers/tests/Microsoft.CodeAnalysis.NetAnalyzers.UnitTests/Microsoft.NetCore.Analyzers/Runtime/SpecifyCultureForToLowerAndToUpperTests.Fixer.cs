// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpSpecifyCultureForToLowerAndToUpperAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpSpecifyCultureForToLowerAndToUpperFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicSpecifyCultureForToLowerAndToUpperAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicSpecifyCultureForToLowerAndToUpperFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class SpecifyCultureForToLowerAndToUpperFixerTests
    {
        [Fact]
        public async Task CA1311_FixToLowerCSharpAsync_SpecifyCurrentCulture()
        {
            const string source = @"
using System.Globalization;

class C
{
    void M()
    {
        var a = ""test"";
        a.[|ToLower|]();
        a?.[|ToLower|]();
    }
}
";

            const string fixedSource = @"
using System.Globalization;

class C
{
    void M()
    {
        var a = ""test"";
        a.ToLower(CultureInfo.CurrentCulture);
        a?.ToLower(CultureInfo.CurrentCulture);
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 0,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture),
            }.RunAsync();
        }

        [Fact]
        public async Task CA1311_FixToLowerCSharpAsync_UseInvariantVersion()
        {
            const string source = @"
class C
{
    void M()
    {
        var a = ""test"";
        a.[|ToLower|]();
        a?.[|ToLower|]();
    }
}
";

            const string fixedSource = @"
class C
{
    void M()
    {
        var a = ""test"";
        a.ToLowerInvariant();
        a?.ToLowerInvariant();
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion),
            }.RunAsync();
        }

        [Fact]
        public async Task CA1311_FixToLowerBasicAsync_SpecifyCurrentCulture()
        {
            var source = @"
Imports System.Globalization

Class C
    Sub M()
        Dim a = ""test""
        a.[|ToLower|]()
        a?.[|ToLower|]()
    End Sub
End Class
";

            var fixedSource = @"
Imports System.Globalization

Class C
    Sub M()
        Dim a = ""test""
        a.ToLower(CultureInfo.CurrentCulture)
        a?.ToLower(CultureInfo.CurrentCulture)
    End Sub
End Class
";
            await new VerifyVB.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 0,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture),
            }.RunAsync();
        }


        [Fact]
        public async Task CA1311_FixToLowerBasicAsync_UseInvariantVersion()
        {
            const string source = @"
Class C
    Sub M()
        Dim a = ""test""
        a.[|ToLower|]()
        a?.[|ToLower|]()
    End Sub
End Class
";

            const string fixedSource = @"
Class C
    Sub M()
        Dim a = ""test""
        a.ToLowerInvariant()
        a?.ToLowerInvariant()
    End Sub
End Class
";

            await new VerifyVB.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion),
            }.RunAsync();
        }

        [Fact]
        public async Task CA1311_FixToUpperCSharpAsync_SpecifyCurrentCulture()
        {
            const string source = @"
using System.Globalization;

class C
{
    void M()
    {
        var a = ""test"";
        a.[|ToUpper|]();
        a?.[|ToUpper|]();
    }
}
";

            const string fixedSource = @"
using System.Globalization;

class C
{
    void M()
    {
        var a = ""test"";
        a.ToUpper(CultureInfo.CurrentCulture);
        a?.ToUpper(CultureInfo.CurrentCulture);
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 0,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture),
            }.RunAsync();
        }

        [Fact]
        public async Task CA1311_FixToUpperCSharpAsync_UseInvariantVersion()
        {
            const string source = @"
class C
{
    void M()
    {
        var a = ""test"";
        a.[|ToUpper|]();
        a?.[|ToUpper|]();
    }
}
";

            const string fixedSource = @"
class C
{
    void M()
    {
        var a = ""test"";
        a.ToUpperInvariant();
        a?.ToUpperInvariant();
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion),
            }.RunAsync();
        }

        [Fact]
        public async Task CA1311_FixToUpperBasicAsync_SpecifyCurrentCulture()
        {
            var source = @"
Imports System.Globalization

Class C
    Sub M()
        Dim a = ""test""
        a.[|ToUpper|]()
        a?.[|ToUpper|]()
    End Sub
End Class
";

            var fixedSource = @"
Imports System.Globalization

Class C
    Sub M()
        Dim a = ""test""
        a.ToUpper(CultureInfo.CurrentCulture)
        a?.ToUpper(CultureInfo.CurrentCulture)
    End Sub
End Class
";
            await new VerifyVB.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 0,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture),
            }.RunAsync();
        }


        [Fact]
        public async Task CA1311_FixToUpperBasicAsync_UseInvariantVersion()
        {
            const string source = @"
Class C
    Sub M()
        Dim a = ""test""
        a.[|ToUpper|]()
        a?.[|ToUpper|]()
    End Sub
End Class
";

            const string fixedSource = @"
Class C
    Sub M()
        Dim a = ""test""
        a.ToUpperInvariant()
        a?.ToUpperInvariant()
    End Sub
End Class
";

            await new VerifyVB.Test
            {
                TestState = { Sources = { source } },
                FixedState = { Sources = { fixedSource } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion),
            }.RunAsync();
        }
    }
}
