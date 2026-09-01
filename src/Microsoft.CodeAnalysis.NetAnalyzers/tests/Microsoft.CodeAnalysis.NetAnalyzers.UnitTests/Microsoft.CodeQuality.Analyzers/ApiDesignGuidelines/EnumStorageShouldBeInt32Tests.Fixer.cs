// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumStorageShouldBeInt32Analyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpEnumStorageShouldBeInt32Fixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumStorageShouldBeInt32Analyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicEnumStorageShouldBeInt32Fixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class EnumStorageShouldBeInt32FixerTests
    {
        #region CSharpUnitTests
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public async Task CSharp_CA1028_FixAllRewritesEveryEnumAsync()
        {
            var code = @"
using System;
namespace Test
{
    public class Outer
    {
        public enum [|Nested|]: byte
        {
            Value1 = 1
        }
    }

    public enum [|TopLevel|]: long
    {
        Value1 = 1
    }
}
";
            var fix = @"
using System;
namespace Test
{
    public class Outer
    {
        public enum Nested
        {
            Value1 = 1
        }
    }

    public enum TopLevel
    {
        Value1 = 1
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fix);
        }

        [TestMethod]
        public async Task Basic_CA1028_FixAllRewritesEveryEnumAsync()
        {
            var code = @"
Imports System
Namespace Test
    Public Class Outer
        Public Enum [|Nested|] As Byte
            Value1 = 1
        End Enum
    End Class

    Public Enum [|TopLevel|] As Long
        Value1 = 1
    End Enum
End Namespace
";
            var fix = @"
Imports System
Namespace Test
    Public Class Outer
        Public Enum Nested 
            Value1 = 1
        End Enum
    End Class

    Public Enum TopLevel 
        Value1 = 1
    End Enum
End Namespace
";
            await VerifyVB.VerifyCodeFixAsync(code, fix);
        }

        #endregion
    }
}