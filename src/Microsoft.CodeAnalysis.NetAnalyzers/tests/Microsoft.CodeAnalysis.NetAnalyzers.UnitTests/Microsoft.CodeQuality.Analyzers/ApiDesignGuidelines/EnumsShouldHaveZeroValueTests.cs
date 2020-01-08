// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using System.Globalization;
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
        public async Task CSharp_EnumsShouldZeroValueFlagsRename()
        {
            // In enum '{0}', change the name of '{1}' to 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E", "A");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E2", "A2");
            string expectedMessage3 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E3", "A3");
            string expectedMessage4 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E4", "A4");

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
                GetCSharpRenameResultAt(7, 9, expectedMessage1),
                GetCSharpRenameResultAt(15, 5, expectedMessage2),
                GetCSharpRenameResultAt(22, 5, expectedMessage3),
                GetCSharpRenameResultAt(29, 5, expectedMessage4));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumsShouldZeroValueFlagsRename_Internal()
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
        public async Task CSharp_EnumsShouldZeroValueFlagsMultipleZero()
        {
            // Remove all members that have the value zero from {0} except for one member that is named 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E2");

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
                GetCSharpMultipleZeroResultAt(5, 17, expectedMessage1),
                GetCSharpMultipleZeroResultAt(14, 13, expectedMessage2));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumsShouldZeroValueFlagsMultipleZero_Internal()
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
        public async Task CSharp_EnumsShouldZeroValueNotFlagsNoZeroValue()
        {
            // Add a member to {0} that has a value of zero with a suggested name of 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E2");

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
                GetCSharpNoZeroResultAt(4, 17, expectedMessage1),
                GetCSharpNoZeroResultAt(9, 17, expectedMessage2));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_EnumsShouldZeroValueNotFlagsNoZeroValue_Internal()
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
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename()
        {
            // In enum '{0}', change the name of '{1}' to 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E", "A");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E2", "A2");
            string expectedMessage3 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E3", "A3");

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
                GetBasicRenameResultAt(5, 9, expectedMessage1),
                GetBasicRenameResultAt(12, 5, expectedMessage2),
                GetBasicRenameResultAt(18, 5, expectedMessage3));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename_Internal()
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
        public async Task VisualBasic_EnumsShouldZeroValueFlagsRename_AttributeListHasTrivia()
        {
            // In enum '{0}', change the name of '{1}' to 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E", "A");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E2", "A2");
            string expectedMessage3 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E3", "A3");

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
                GetBasicRenameResultAt(5, 6, expectedMessage1),
                GetBasicRenameResultAt(12, 2, expectedMessage2),
                GetBasicRenameResultAt(18, 2, expectedMessage3));
        }

        [Fact]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsMultipleZero()
        {
            // Remove all members that have the value zero from {0} except for one member that is named 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E2");
            string expectedMessage3 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E3");

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
                GetBasicMultipleZeroResultAt(4, 17, expectedMessage1),
                GetBasicMultipleZeroResultAt(11, 13, expectedMessage2),
                GetBasicMultipleZeroResultAt(17, 13, expectedMessage3));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumsShouldZeroValueFlagsMultipleZero_Internal()
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
        public async Task VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValue()
        {
            // Add a member to {0} that has a value of zero with a suggested name of 'None'.
            string expectedMessage1 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E");
            string expectedMessage2 = string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E2");

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
                GetBasicNoZeroResultAt(3, 17, expectedMessage1),
                GetBasicNoZeroResultAt(7, 17, expectedMessage2));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValue_Internal()
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

        private static DiagnosticResult GetCSharpMultipleZeroResultAt(int line, int column, string message)
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero)
                .WithLocation(line, column)
                .WithMessage(message);

        private static DiagnosticResult GetBasicMultipleZeroResultAt(int line, int column, string message)
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleMultipleZero)
                .WithLocation(line, column)
                .WithMessage(message);

        private static DiagnosticResult GetCSharpNoZeroResultAt(int line, int column, string message)
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero)
                .WithLocation(line, column)
                .WithMessage(message);

        private static DiagnosticResult GetBasicNoZeroResultAt(int line, int column, string message)
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleNoZero)
                .WithLocation(line, column)
                .WithMessage(message);

        private static DiagnosticResult GetCSharpRenameResultAt(int line, int column, string message)
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename)
                .WithLocation(line, column)
                .WithMessage(message);

        private static DiagnosticResult GetBasicRenameResultAt(int line, int column, string message)
            => VerifyCS.Diagnostic(EnumsShouldHaveZeroValueAnalyzer.RuleRename)
                .WithLocation(line, column)
                .WithMessage(message);
    }
}
