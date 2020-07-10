// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.RuntimePlatformCheckAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class RuntimePlatformCheckAnalyzerTests
    {
        private readonly string PlatformCheckApiSource = @"
namespace System.Runtime.InteropServices
{
    public class RuntimeInformationHelper
    {
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major) => true;
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor) => true;
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build) => true;
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build, int revision) => true;

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major) => true;
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor) => true;
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build) => true;
        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build, int revision) => true;
    }   
}";

        [Fact]
        public async Task SimpleIfTest()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }

        if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 1, 1))
        {
            {|#2:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Windows;1.1'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformEarlierThan;Windows;1.1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfTest_02()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#0:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }

        if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 1, 1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Windows;1.1'
        }

        {|#2:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformEarlierThan;Windows;1.1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseTest()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }
        else
        {
            {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseIfElseTest()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }
        else if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Linux, 1, 1))
        {
            {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1 && IsOSPlatformEarlierThan;Linux;1.1'
        }
        else
        {
            {|#3:M2()|};        // Platform checks:'!IsOSPlatformEarlierThan;Linux;1.1 && !IsOSPlatformOrLater;Windows;1'
        }

        {|#4:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Windows;1 && IsOSPlatformEarlierThan;Linux;1.1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformEarlierThan;Linux;1.1 && !IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfTestWithNegation()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
        }

        if(!RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 1, 1))
        {
            {|#2:M2()|};        // Platform checks:'!IsOSPlatformEarlierThan;Windows;1.1'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformEarlierThan;Windows;1.1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseTestWithNegation()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
        }
        else
        {
            {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseIfElseTestWithNegation()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(!RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Windows;1'
        }
        else if(RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Linux, 1, 1))
        {
            {|#2:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1'
        }
        else
        {
            {|#3:M2()|};        // Platform checks:'!IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1'
        }

        {|#4:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("!IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformEarlierThan;Linux;1.1 && IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfTestWithLogicalAnd()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) &&
           RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 2))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformEarlierThan;Windows;2 && IsOSPlatformOrLater;Windows;1'
        }

        {|#2:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformEarlierThan;Windows;2 && IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseTestWithLogicalAnd()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) &&
           RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Windows;1'
        }
        else
        {
            {|#2:M2()|};        // Platform checks:''
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfTestWithLogicalOr()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
           RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
        {
            {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
        }

        {|#2:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseTestWithLogicalOr()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
           RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
        {
            {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
        }
        else
        {
            {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseIfElseTestWithLogicalOr()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
           RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
        {
            {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
        }
        else if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3))
        {
            {|#2:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Windows;3'
        }
        else
        {
            {|#3:M2()|};        // Platform checks:'!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && !IsOSPlatformOrLater;Windows;3'
        }

        {|#4:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Windows;3"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformOrLater;Linux;1 && !IsOSPlatformOrLater;Windows;1 && !IsOSPlatformOrLater;Windows;3"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments(""));
        }

        [Fact]
        public async Task SimpleIfElseIfTestWithLogicalOr_02()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if((RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1) ||
            RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1)) &&
            (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 2) ||
            RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 2)))
        {
            {|#1:M2()|};        // Platform checks:'((!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && !IsOSPlatformOrLater;Windows;2 && IsOSPlatformOrLater;Linux;2 || (!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && IsOSPlatformOrLater;Windows;2)'
        }
        else if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3) ||
                 RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 3) ||
                 RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 4))
        {
            {|#2:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Linux;3 && !IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;4 || (!IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;3 || IsOSPlatformOrLater;Windows;3))'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("((!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && !IsOSPlatformOrLater;Windows;2 && IsOSPlatformOrLater;Linux;2 || (!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1) && IsOSPlatformOrLater;Windows;2)"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("(!IsOSPlatformOrLater;Linux;3 && !IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;4 || (!IsOSPlatformOrLater;Windows;3 && IsOSPlatformOrLater;Linux;3 || IsOSPlatformOrLater;Windows;3))"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments(""));
        }

        [Fact]
        public async Task ControlFlowAndMultipleChecks()
        {
            var source = @"
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1))
        {
            {|#1:M2()|};      // Platform checks:'IsOSPlatformOrLater;Linux;1'

            if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 2, 0))
            {
                {|#2:M2()|};    // Platform checks:'IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Linux;2'
            }
            else if (!RuntimeInformationHelper.IsOSPlatformEarlierThan(OSPlatform.Windows, 3, 1, 1))
            {
                {|#3:M2()|};    // Platform checks:'!IsOSPlatformEarlierThan;Windows;3.1.1 && !IsOSPlatformOrLater;Linux;2 && IsOSPlatformOrLater;Linux;1'
            }

            {|#4:M2()|};    // Platform checks:'IsOSPlatformOrLater;Linux;1'
        }
        else
        {
            {|#5:M2()|};    // Platform checks:'!IsOSPlatformOrLater;Linux;1'
        }

        {|#6:M2()|};        // Platform checks:''

        if ({|#7:IsWindows3OrLater()|})    // Platform checks:''
        {
            {|#8:M2()|};    // Platform checks:''
        }

        {|#9:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }

    bool IsWindows3OrLater()
    {
        return RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3, 0, 0, 0);
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Linux;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Linux;1 && IsOSPlatformOrLater;Linux;2"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("!IsOSPlatformEarlierThan;Windows;3.1.1 && !IsOSPlatformOrLater;Linux;2 && IsOSPlatformOrLater;Linux;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments("IsOSPlatformOrLater;Linux;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(5).WithArguments("!IsOSPlatformOrLater;Linux;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(6).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(7).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(8).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(9).WithArguments(""));
        }

        [Fact]
        public async Task DebugAssertAnalysisTest()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        {|#1:Debug.Assert(RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3, 0, 0, 0))|};  // Platform checks:'IsOSPlatformOrLater;Windows;3'

        {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;3'
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;3"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;3"));
        }

        [Fact]
        public async Task ResultSavedInLocal()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        var x1 = RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1);
        var x2 = RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Linux, 1);
        if (x1 || x2)
        {
            {|#1:M2()|};        // Platform checks:'(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)'
        }

        {|#2:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("(!IsOSPlatformOrLater;Windows;1 && IsOSPlatformOrLater;Linux;1 || IsOSPlatformOrLater;Windows;1)"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
        }

        [Fact]
        public async Task VersionSavedInLocal()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        var v1 = 1;
        if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, v1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }

        {|#2:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
        }

        [Fact]
        public async Task PlatformSavedInLocal_NotYetSupported()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        var platform = OSPlatform.Windows;
        if (RuntimeInformationHelper.IsOSPlatformOrLater(platform, 1))
        {
            {|#1:M2()|};        // Platform checks:''
        }

        {|#2:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(""));
        }

        [Fact]
        public async Task UnrelatedConditionCheckDoesNotInvalidateState()
        {
            var source = @"
using System.Diagnostics;
using System.Runtime.InteropServices;

class Test
{
    void M1(bool flag1, bool flag2)
    {
        {|#0:M2()|};    // Platform checks:''

        if (RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 1))
        {
            {|#1:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'

            if (flag1 || flag2)
            {
                {|#2:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
            }
            else
            {
                {|#3:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
            }
            
            {|#4:M2()|};        // Platform checks:'IsOSPlatformOrLater;Windows;1'
        }

        {|#5:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }
}" + PlatformCheckApiSource;

            await VerifyCS.VerifyAnalyzerAsync(source,
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(4).WithArguments("IsOSPlatformOrLater;Windows;1"),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(5).WithArguments(""));
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive")]
        public async Task InterproceduralAnalysisTest(string editorconfig)
        {
            var source = @"
using System.Runtime.InteropServices;

class Test
{
    void M1()
    {
        {|#0:M2()|};    // Platform checks:''

        if ({|#1:IsWindows3OrLater()|})    // Platform checks:''
        {
            {|#2:M2()|};    // Platform checks:'IsOSPlatformOrLater;Windows;3'
        }

        {|#3:M2()|};        // Platform checks:''
    }

    void M2()
    {
    }

    bool IsWindows3OrLater()
    {
        return RuntimeInformationHelper.IsOSPlatformOrLater(OSPlatform.Windows, 3, 0, 0, 0);
    }
}" + PlatformCheckApiSource;

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AdditionalFiles = { (".editorconfig", editorconfig) }
                }
            };

            var argForInterprocDiagnostics = editorconfig.Length == 0 ? "" : "IsOSPlatformOrLater;Windows;3";
            test.ExpectedDiagnostics.AddRange(new[]
            {
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(0).WithArguments(""),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(1).WithArguments(argForInterprocDiagnostics),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(2).WithArguments(argForInterprocDiagnostics),
                VerifyCS.Diagnostic(RuntimePlatformCheckAnalyzer.Rule).WithLocation(3).WithArguments("")
            });

            await test.RunAsync();
        }
    }
}
