// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Test.Utilities;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EnumsShouldHaveZeroValueTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new EnumsShouldHaveZeroValueAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EnumsShouldHaveZeroValueAnalyzer();
        }

        [Fact]
        public void CSharp_EnumsShouldZeroValueFlagsRename()
        {
            // In enum '{0}', change the name of '{1}' to 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E", "A");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E2", "A2");
            string expectedMessage3 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E3", "A3");
            string expectedMessage4 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E4", "A4");

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
            VerifyCSharp(code,
                GetCSharpResultAt(7, 9, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetCSharpResultAt(15, 5, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2),
                GetCSharpResultAt(22, 5, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage3),
                GetCSharpResultAt(29, 5, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage4));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_EnumsShouldZeroValueFlagsRename_Internal()
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
            VerifyCSharp(code);
        }

        [Fact]
        public void CSharp_EnumsShouldZeroValueFlagsMultipleZero()
        {
            // Remove all members that have the value zero from {0} except for one member that is named 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E2");

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
            VerifyCSharp(code,
                GetCSharpResultAt(5, 17, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetCSharpResultAt(14, 13, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_EnumsShouldZeroValueFlagsMultipleZero_Internal()
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
            VerifyCSharp(code);
        }

        [Fact]
        public void CSharp_EnumsShouldZeroValueNotFlagsNoZeroValue()
        {
            // Add a member to {0} that has a value of zero with a suggested name of 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E2");

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
            VerifyCSharp(code,
                GetCSharpResultAt(4, 17, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetCSharpResultAt(9, 17, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharp_EnumsShouldZeroValueNotFlagsNoZeroValue_Internal()
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
            VerifyCSharp(code);
        }

        [Fact]
        public void VisualBasic_EnumsShouldZeroValueFlagsRename()
        {
            // In enum '{0}', change the name of '{1}' to 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E", "A");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E2", "A2");
            string expectedMessage3 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E3", "A3");

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
            VerifyBasic(code,
                GetBasicResultAt(5, 9, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetBasicResultAt(12, 5, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2),
                GetBasicResultAt(18, 5, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage3));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VisualBasic_EnumsShouldZeroValueFlagsRename_Internal()
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
            VerifyBasic(code);
        }

        [WorkItem(836193, "DevDiv")]
        [Fact]
        public void VisualBasic_EnumsShouldZeroValueFlagsRename_AttributeListHasTrivia()
        {
            // In enum '{0}', change the name of '{1}' to 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E", "A");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E2", "A2");
            string expectedMessage3 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsRename, "E3", "A3");

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
            VerifyBasic(code,
                GetBasicResultAt(5, 6, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetBasicResultAt(12, 2, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2),
                GetBasicResultAt(18, 2, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage3));
        }

        [Fact]
        public void VisualBasic_EnumsShouldZeroValueFlagsMultipleZero()
        {
            // Remove all members that have the value zero from {0} except for one member that is named 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E2");
            string expectedMessage3 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageFlagsMultipleZeros, "E3");

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

            VerifyBasic(code,
                GetBasicResultAt(4, 17, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetBasicResultAt(11, 13, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2),
                GetBasicResultAt(17, 13, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage3));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VisualBasic_EnumsShouldZeroValueFlagsMultipleZero_Internal()
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

            VerifyBasic(code);
        }

        [Fact]
        public void VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValue()
        {
            // Add a member to {0} that has a value of zero with a suggested name of 'None'.
            string expectedMessage1 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E");
            string expectedMessage2 = string.Format(MicrosoftCodeQualityAnalyzersResources.EnumsShouldHaveZeroValueMessageNotFlagsNoZeroValue, "E2");

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

            VerifyBasic(code,
                GetBasicResultAt(3, 17, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage1),
                GetBasicResultAt(7, 17, EnumsShouldHaveZeroValueAnalyzer.RuleId, expectedMessage2));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VisualBasic_EnumsShouldZeroValueNotFlagsNoZeroValue_Internal()
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

            VerifyBasic(code);
        }
    }
}
