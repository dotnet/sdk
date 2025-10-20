﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseStringContainsCharOverloadWithSingleCharactersAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpUseStringContainsCharOverloadWithSingleCharactersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseStringContainsCharOverloadWithSingleCharactersAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicUseStringContainsCharOverloadWithSingleCharactersFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class UseStringContainsCharOverloadWithSingleCharactersTests
    {
        [Fact]
        public async Task CSharp_RegularStringLiteral_FixedAsync()
        {
            var violatingSourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains([|""a""|]);
    }
}";

            var fixedSourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains('a');
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(violatingSourceCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedSourceCode);
        }

        [Fact]
        public async Task CSharp_RegularStringLiteralWithStringComparisonOverload_FixedAsync()
        {
            var violatingSourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains([|""a""|], StringComparison.InvariantCultureIgnoreCase);
    }
}";

            var fixedSourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains('a', StringComparison.InvariantCultureIgnoreCase);
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(violatingSourceCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedSourceCode);
        }

        [Fact]
        public async Task CSharp_RegularStringLiteralWithStringComparisonOverloadAndReorderedNamedArguments_FixedAsync()
        {
            var violatingSourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains(comparisonType: StringComparison.InvariantCultureIgnoreCase, [|value: ""a""|]);
    }
}";

            var fixedSourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains(comparisonType: StringComparison.InvariantCultureIgnoreCase, value: 'a');
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(violatingSourceCode, ReferenceAssemblies.NetStandard.NetStandard21, fixedSourceCode);
        }

        [Fact]
        public async Task CSharp_ConstructedStringLiteral_UnchangedAsync()
        {
            var sourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains(""a""+""samplesuffix"");
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(sourceCode, ReferenceAssemblies.NetStandard.NetStandard21, sourceCode);
        }

        [Fact]
        public async Task CSharp_ConstructedStringLiteralWithStringComparisonOverload_UnchangedAsync()
        {
            var sourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        return ""testString"".Contains(""a""+""samplesuffix"", StringComparison.InvariantCultureIgnoreCase);
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(sourceCode, ReferenceAssemblies.NetStandard.NetStandard21, sourceCode);
        }

        [Fact]
        public async Task CSharp_SingleCharacterStringVariable_UnchangedAsync()
        {
            var sourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        var singleCharacterString = ""a"";
        return ""testString"".Contains(singleCharacterString);
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(sourceCode, ReferenceAssemblies.NetStandard.NetStandard21, sourceCode);
        }

        [Fact]
        public async Task CSharp_SingleCharacterStringVariableWithStringComparisonOverload_UnchangedAsync()
        {
            var sourceCode = @"using System;
public class TestClass
{
    public bool TestMethod(string input)
    {
        var singleCharacterString = ""a"";
        return ""testString"".Contains(singleCharacterString, StringComparison.InvariantCultureIgnoreCase);
    }
}";
            await VerifyCSCodeFixWithGivenReferenceAssembliesAsync(sourceCode, ReferenceAssemblies.NetStandard.NetStandard21, sourceCode);
        }

        [Fact]
        public async Task VB_RegularStringLiteralWithoutAvailableOverload_NoDiagnosticAsync()
        {
            var test = new VerifyVB.Test()
            {
                TestCode = @"Public Class Program
    Public Sub M()
        Dim a = ""testString"".Contains(""a"")
    End Sub
End Class",
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task VB_RegularStringLiteral_FixedAsync()
        {
            var test = new VerifyVB.Test()
            {
                TestCode = @"Public Class Program
    Public Sub M()
        Dim a = ""testString"".Contains([|""t""|])
    End Sub
End Class",
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard21,
                FixedCode = @"Public Class Program
    Public Sub M()
        Dim a = ""testString"".Contains(""t""c)
    End Sub
End Class"
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task VB_NamedStringLiteral_FixedAsync()
        {
            var test = new VerifyVB.Test()
            {
                TestCode = @"Public Class Program
    Public Sub M()
        Dim a = ""testString"".Contains([|value:=""t""|])
    End Sub
End Class",
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard21,
                FixedCode = @"Public Class Program
    Public Sub M()
        Dim a = ""testString"".Contains(value:=""t""c)
    End Sub
End Class"
            };
            await test.RunAsync();
        }

        private static async Task VerifyCSCodeFixWithGivenReferenceAssembliesAsync(string source, ReferenceAssemblies referenceAssemblies, string fixedSource)
        {
            await new VerifyCS.Test()
            {
                TestCode = source,
                ReferenceAssemblies = referenceAssemblies,
                FixedCode = fixedSource
            }.RunAsync();
        }
    }
}
