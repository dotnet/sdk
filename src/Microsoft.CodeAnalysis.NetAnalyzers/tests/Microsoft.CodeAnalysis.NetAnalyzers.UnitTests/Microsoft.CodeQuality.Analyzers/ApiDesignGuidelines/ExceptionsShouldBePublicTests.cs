// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ExceptionsShouldBePublicFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class ExceptionsShouldBePublicTests
    {
        private static readonly IEnumerable<OutputKind> ExecutableOutputKinds =
        [
            OutputKind.ConsoleApplication,
            OutputKind.WindowsRuntimeApplication,
            OutputKind.WindowsApplication
        ];

        public static readonly TheoryData<OutputKind> DiagnosticSuppressingOutputKinds = new(ExecutableOutputKinds);

        public static readonly TheoryData<OutputKind> DiagnosticTriggeringOutputKinds = new(Enum.GetValues<OutputKind>().Except(ExecutableOutputKinds));

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestCSharpNonPublicExceptionAsync(OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           using System;
                           class [|InternalException|] : Exception
                           {
                           }
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestCSharpNonPublicException2Async(OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           using System;
                           internal class Outer
                           {
                               private class [|PrivateException|] : SystemException
                               {
                               }
                           }
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestCSharpPublicExceptionAsync(OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           using System;
                           public class BasicException : Exception
                           {
                           }
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestCSharpNonExceptionTypeAsync(OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           using System.IO;
                           public class NonException : StringWriter
                           {
                           }
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestVBasicNonPublicExceptionAsync(OutputKind outputKind)
        {
            await new VerifyVB.Test
            {
                TestCode = """
                           Imports System
                           Class [|InternalException|]
                              Inherits Exception
                           End Class
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestVBasicNonPublicException2Async(OutputKind outputKind)
        {
            await new VerifyVB.Test
            {
                TestCode = """
                           Imports System
                           Public Class Outer
                               Private Class [|PrivateException|]
                                   Inherits SystemException
                               End Class
                           End Class
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestVBasicPublicExceptionAsync(OutputKind outputKind)
        {
            await new VerifyVB.Test
            {
                TestCode = """
                           Imports System
                           Public Class BasicException
                              Inherits Exception
                           End Class
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticTriggeringOutputKinds))]
        public async Task TestVBasicNonExceptionTypeAsync(OutputKind outputKind)
        {
            await new VerifyVB.Test
            {
                TestCode = """
                           Imports System.IO
                           Imports System.Text
                           Public Class NonException
                              Inherits StringWriter
                           End Class
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticSuppressingOutputKinds))]
        public async Task TestCSharpWithExecutableAsync(OutputKind outputKind)
        {
            await new VerifyCS.Test
            {
                TestCode = """
                           using System;
                           class InternalException : Exception
                           {
                           }
                           
                           class Program
                           {
                               static void Main() {}
                           }
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }

        [Theory]
        [MemberData(nameof(DiagnosticSuppressingOutputKinds))]
        public async Task TestVBasicWithExecutableAsync(OutputKind outputKind)
        {
            await new VerifyVB.Test
            {
                TestCode = """
                           Imports System
                           Class InternalException
                              Inherits Exception
                           End Class
                           
                           Class Program
                               Shared Sub Main()
                               End Sub
                           End Class
                           """,
                TestState = { OutputKind = outputKind }
            }.RunAsync();
        }
    }
}