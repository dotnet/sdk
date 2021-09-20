// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class ExceptionsShouldBePublicFixerTests
    {
        [Fact]
        public async Task TestCSharpNonPublicExceptionAsync()
        {
            var original = @"
using System;

class [|InternalException|] : Exception
{
}";

            var expected = @"
using System;

public class InternalException : Exception
{
}";

            await VerifyCS.VerifyCodeFixAsync(original, expected);
        }

        [Fact]
        public async Task TestCSharpNonPublicException2Async()
        {
            var original = @"
using System;

public class Outer
{
    private class [|PrivateException|] : SystemException
    {
    }
}";

            var expected = @"
using System;

public class Outer
{
    public class PrivateException : SystemException
    {
    }
}";

            await VerifyCS.VerifyCodeFixAsync(original, expected);
        }

        [Fact]
        public async Task TestVBasicNonPublicExceptionAsync()
        {
            var original = @"
Imports System

Class [|InternalException|]
   Inherits Exception
End Class";

            var expected = @"
Imports System

Public Class InternalException
   Inherits Exception
End Class";

            await VerifyVB.VerifyCodeFixAsync(original, expected);
        }

        [Fact]
        public async Task TestVBasicNonPublicException2Async()
        {
            var original = @"
Imports System

public class Outer
    Private Class [|PrivateException|]
       Inherits SystemException
    End Class
End Class";

            var expected = @"
Imports System

public class Outer
    Public Class PrivateException
       Inherits SystemException
    End Class
End Class";

            await VerifyVB.VerifyCodeFixAsync(original, expected);
        }
    }
}