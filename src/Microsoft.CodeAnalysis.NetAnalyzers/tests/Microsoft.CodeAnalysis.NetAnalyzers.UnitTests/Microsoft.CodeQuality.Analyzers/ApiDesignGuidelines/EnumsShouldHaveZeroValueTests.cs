// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumsShouldHaveZeroValueAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EquatableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EnumsShouldHaveZeroValueAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EquatableFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumsShouldHaveZeroValueTests
    {
        [Fact]
        public async Task CSharp_EnumsShouldZeroValueFlagsRenameAsync()
        {
            var code = @"
public class Outer
{
    [System.Flags]
    public enum E
    {
        A = 0,
        B = 3
    }
}

[System.Flags]
public enum E2
{
    A2 = 0,
    B2 = 1
}

[System.Flags]
public enum E3
{
    A3 = (ushort)0,
    B3 = (ushort)1
}

[System.Flags]
public enum E4
{
    A4 = 0,
    B4 = (int)2  // Sample comment
}

[System.Flags]
public enum NoZeroValuedField
{
    A5 = 1,
    B5 = 2
}";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpRenameResultAt(7, 9, "E", "A"),
                GetCSharpRenameResultAt(15, 5, "E2", "A2"),
                GetCSharpRenameResultAt(22, 5, "E3", "A3"),
                GetCSharpRenameResultAt(29, 5, "E4", "A4"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumsShouldZeroValueFlagsRename_InternalAsync()
        {
            var code = @"
class Outer
{
    [System.Flags]
    private enum E
    {
        A = 0,
        B = 3
    }
}

[System.Flags]
internal enum E2
{
    A2 = 0,
    B2 = 1
}

[System.Flags]
internal enum E3
{
    A3 = (ushort)0,
    B3 = (ushort)1
}

[System.Flags]
internal enum E4
{
    A4 = 0,
    B4 = (int)2  // Sample comment
}

[System.Flags]
internal enum NoZeroValuedField
{
    A5 = 1,
    B5 = 2
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharp_EnumsShouldZeroValueFlagsMultipleZeroAsync()
        {
            var code = @"// Some comment
public class Outer
{
    [System.Flags]
    public enum E
    {
        None = 0,
        A = 0
    }
}

// Some comment
[System.Flags]
public enum E2
{
    None = 0,
    A = None
}";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpMultipleZeroResultAt(5, 17, "E"),
                GetCSharpMultipleZeroResultAt(14, 13, "E2"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumsShouldZeroValueFlagsMultipleZero_InternalAsync()
        {
            var code = @"// Some comment
public class Outer
{
    [System.Flags]
    private enum E
    {
        None = 0,
        A = 0
    }
}

// Some comment
[System.Flags]
internal enum E2
{
    None = 0,
    A = None
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharp_EnumsShouldZeroValueNotFlagsNoZeroValueAsync()
        {
            var code = @"
public class Outer
{
    public enum E
    {
        A = 1
    }

    public enum E2
    {
        None = 1,
        A = 2
    }
}

public enum E3
{
    None = 0,
    A = 1
}

public enum E4
{
    None = 0,
    A = 0
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpNoZeroResultAt(4, 17, "E"),
                GetCSharpNoZeroResultAt(9, 17, "E2"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumsShouldZeroValueNotFlagsNoZeroValue_InternalAsync()
        {
            var code = @"
public class Outer
{
    private enum E
    {
        A = 1
    }

    private enum E2
    {
        None = 1,
        A = 2
    }
}

enum E3
{
    None = 0,
    A = 1
}

internal enum E4
{
    None = 0,
    A = 0
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRenameAsync()
        {
            var code = @"
Public Class C
    <System.Flags>
    Public Enum E
        A = 0
        B = 1
    End Enum
End Class

<System.Flags>
Public Enum E2
    A2 = 0
    B2 = 1
End Enum

<System.Flags>
Public Enum E3
    A3 = CUShort(0)
    B3 = CUShort(1)
End Enum

<System.Flags>
Public Enum NoZeroValuedField
    A5 = 1
    B5 = 2
End Enum
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicRenameResultAt(5, 9, "E", "A"),
                GetBasicRenameResultAt(12, 5, "E2", "A2"),
                GetBasicRenameResultAt(18, 5, "E3", "A3"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename_InternalAsync()
        {
            var code = @"
Public Class C
    <System.Flags>
    Private Enum E
        A = 0
        B = 1
    End Enum
End Class

<System.Flags>
Enum E2
    A2 = 0
    B2 = 1
End Enum

<System.Flags>
Friend Enum E3
    A3 = CUShort(0)
    B3 = CUShort(1)
End Enum

<System.Flags>
Friend Enum NoZeroValuedField
    A5 = 1
    B5 = 2
End Enum
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [WorkItem(836193, "DevDiv")]
        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename_AttributeListHasTriviaAsync()
        {
            var code = @"
Public Class Outer
    <System.Flags> _
    Public Enum E
	    A = 0
	    B = 1
    End Enum
End Class

<System.Flags> _
Public Enum E2
	A2 = 0
	B2 = 1
End Enum

<System.Flags> _
Public Enum E3
	A3 = CUShort(0)
	B3 = CUShort(1)
End Enum

<System.Flags> _
Public Enum NoZeroValuedField
	A5 = 1
	B5 = 2
End Enum
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicRenameResultAt(5, 6, "E", "A"),
                GetBasicRenameResultAt(12, 2, "E2", "A2"),
                GetBasicRenameResultAt(18, 2, "E3", "A3"));
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsMultipleZeroAsync()
        {
            var code = @"
Public Class Outer
    <System.Flags>
    Public Enum E
	    None = 0
	    A = 0
    End Enum
End Class

<System.Flags>
Public Enum E2
	None = 0
	A = None
End Enum

<System.Flags>
Public Enum E3
    A3 = 0
    B3 = CUInt(0)  ' Not a constant
End Enum";

            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicMultipleZeroResultAt(4, 17, "E"),
                GetBasicMultipleZeroResultAt(11, 13, "E2"),
                GetBasicMultipleZeroResultAt(17, 13, "E3"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsMultipleZero_InternalAsync()
        {
            var code = @"
Public Class Outer
    <System.Flags>
    Private Enum E
	    None = 0
	    A = 0
    End Enum
End Class

<System.Flags>
Enum E2
	None = 0
	A = None
End Enum

<System.Flags>
Friend Enum E3
	A3 = 0
	B3 = CUInt(0)  ' Not a constant
End Enum";

            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValueAsync()
        {
            var code = @"
Public Class Outer
    Public Enum E
	    A = 1
    End Enum

    Public Enum E2
	    None = 1
	    A = 2
    End Enum
End Class

Public Enum E3
    None = 0
    A = 1
End Enum

Public Enum E4
    None = 0
    A = 0
End Enum
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicNoZeroResultAt(3, 17, "E"),
                GetBasicNoZeroResultAt(7, 17, "E2"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValue_InternalAsync()
        {
            var code = @"
Public Class Outer
    Private Enum E
	    A = 1
    End Enum

    Friend Enum E2
	    None = 1
	    A = 2
    End Enum
End Class

Enum E3
    None = 0
    A = 1
End Enum

Friend Enum E4
    None = 0
    A = 0
End Enum
";

            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        private static DiagnosticResult GetCSharpMultipleZeroResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetBasicMultipleZeroResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCSharpNoZeroResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetBasicNoZeroResultAt(int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCSharpRenameResultAt(int line, int column, string typeName, string newName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, newName);

        private static DiagnosticResult GetBasicRenameResultAt(int line, int column, string typeName, string newName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName, newName);
    }
}
