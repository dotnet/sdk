// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UriParametersShouldNotBeStringsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UriParametersShouldNotBeStringsFixerTests
    {
        [Fact]
        public async Task CA1054WarningWithUrlAsync()
        {
            var code = @"
using System;

public class A
{
    public static void Method(string [|url|]) { }
}
";

            var fix = @"
using System;

public class A
{
    public static void Method(string url) { }

    public static void Method(Uri url)
    {
        throw new NotImplementedException();
    }
}
";

            await VerifyCS.VerifyCodeFixAsync(code, fix);
        }

        [Fact]
        public async Task CA1054MultipleWarningWithUrlAsync()
        {
            var code = @"
using System;

public class A
{
    public static void Method(string [|url|], string [|url2|]) { }
}
";
            var fix = @"
using System;

public class A
{
    public static void Method(string url, string url2) { }

    public static void Method(Uri url, string url2)
    {
        throw new NotImplementedException();
    }

    public static void Method(string url, Uri url2)
    {
        throw new NotImplementedException();
    }

    public static void Method(Uri url, Uri url2)
    {
        throw new NotImplementedException();
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fix } },
                NumberOfIncrementalIterations = 3,
                NumberOfFixAllIterations = 3,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1054MultipleWarningWithUrlWithOverloadAsync()
        {
            // Following original FxCop implementation. but this seems strange.
            var code = @"
using System;

public class A
{
    public static void Method(string [|url|], string [|url2|]) { }
    public static void Method(Uri url, Uri url2) { }
}
";
            var fix = @"
using System;

public class A
{
    public static void Method(string url, string url2) { }
    public static void Method(Uri url, Uri url2) { }

    public static void Method(Uri url, string url2)
    {
        throw new NotImplementedException();
    }

    public static void Method(string url, Uri url2)
    {
        throw new NotImplementedException();
    }
}
";
            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fix } },
                NumberOfIncrementalIterations = 2,
                NumberOfFixAllIterations = 2,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1054WarningVBAsync()
        {
            // C# and VB shares same implementation. so just one vb test
            var code = @"
Imports System

Public Class A
    Public Sub Method([|firstUri|] As String)
    End Sub
End Class
";
            var fix = @"
Imports System

Public Class A
    Public Sub Method(firstUri As String)
    End Sub

    Public Sub Method(firstUri As Uri)
        Throw New NotImplementedException()
    End Sub
End Class
";

            await VerifyVB.VerifyCodeFixAsync(code, fix);
        }
    }
}