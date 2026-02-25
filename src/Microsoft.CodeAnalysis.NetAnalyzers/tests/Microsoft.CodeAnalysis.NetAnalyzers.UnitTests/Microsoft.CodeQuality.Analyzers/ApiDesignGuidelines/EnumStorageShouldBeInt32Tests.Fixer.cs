// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumStorageShouldBeInt32Analyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpEnumStorageShouldBeInt32Fixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumStorageShouldBeInt32Analyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicEnumStorageShouldBeInt32Fixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumStorageShouldBeInt32FixerTests
    {
        #region CSharpUnitTests
        [Fact]
        public async Task CSharp_CA1028_TestFixForEnumTypeIsLongWithNoTriviaAsync()
        {
            var code = @"
using System;
namespace Test
{
    public enum [|TestEnum1|]: long
    {
        Value1 = 1,
        Value2 = 2
    }
}
";
            var fix = @"
using System;
namespace Test
{
    public enum TestEnum1
    {
        Value1 = 1,
        Value2 = 2
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fix);
        }

        [Fact]
        public async Task CSharp_CA1028_TestFixForEnumTypeIsLongWithTriviaAsync()
        {
            var code = @"
using System;
namespace Test
{
    public enum [|TestEnum1|]: long // with trivia
    {
        Value1 = 1,
        Value2 = 2
    }
}
";
            var fix = @"
using System;
namespace Test
{
    public enum TestEnum1 // with trivia
    {
        Value1 = 1,
        Value2 = 2
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fix);
        }
        #endregion

        #region BasicUnitTests

        [Fact]
        public async Task Basic_CA1028_TestFixForEnumTypeIsLongWithNoTriviaAsync()
        {
            var code = @"
Imports System
Public Module Module1
    Public Enum [|TestEnum1|] As Long
        Value1 = 1
        Value2 = 2
    End Enum
End Module
";
            var fix = @"
Imports System
Public Module Module1
    Public Enum TestEnum1 
        Value1 = 1
        Value2 = 2
    End Enum
End Module
";
            await VerifyVB.VerifyCodeFixAsync(code, fix);
        }

        [Fact]
        public async Task Basic_CA1028_TestFixForEnumTypeIsLongWithTriviaAsync()
        {
            var code = @"
Imports System
Public Module Module1
    Public Enum [|TestEnum1|] As Long 'with trivia 
        Value1 = 1
        Value2 = 2
    End Enum
End Module
";
            var fix = @"
Imports System
Public Module Module1
    Public Enum TestEnum1  'with trivia 
        Value1 = 1
        Value2 = 2
    End Enum
End Module
";
            await VerifyVB.VerifyCodeFixAsync(code, fix);
        }

        #endregion
    }
}