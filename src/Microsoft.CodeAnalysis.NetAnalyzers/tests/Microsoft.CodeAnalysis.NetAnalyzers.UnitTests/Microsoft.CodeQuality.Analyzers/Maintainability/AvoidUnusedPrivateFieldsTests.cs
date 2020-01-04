// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class AvoidUnusedPrivateFieldsTests : DiagnosticAnalyzerTestBase
    {
        private const string CSharpMEFAttributesDefinition = @"
namespace System.ComponentModel.Composition
{
    public class ExportAttribute: System.Attribute
    {
    }
}

namespace System.Composition
{
    public class ExportAttribute: System.Attribute
    {
    }
}
";
        private const string BasicMEFAttributesDefinition = @"
Namespace System.ComponentModel.Composition
    Public Class ExportAttribute
        Inherits System.Attribute
    End Class
End Namespace

Namespace System.Composition
    Public Class ExportAttribute
        Inherits System.Attribute
    End Class
End Namespace
";

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new AvoidUnusedPrivateFieldsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AvoidUnusedPrivateFieldsAnalyzer();
        }

        [Fact]
        public void CA1823_CSharp_AttributeUsage_NoDiagnostic()
        {
            VerifyCSharp(@"
[System.Obsolete(Message)]
public class Class
{
    private const string Message = ""Test"";
}
");
        }

        [Fact]
        public void CA1823_CSharp_InterpolatedStringUsage_NoDiagnostic()
        {
            VerifyCSharp(@"
public class Class
{
    private const string Message = ""Test"";
    public string PublicMessage = $""Test: {Message}"";
}
");
        }

        [Fact]
        public void CA1823_CSharp_CollectionInitializerUsage_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.Collections.Generic;

public class Class
{
    private const string Message = ""Test"";
    public List<string> PublicMessage = new List<string> { Message };
}
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_CSharp_FieldOffsetAttribute_NoDiagnostic()
        {
            VerifyCSharp(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
public class Class
{
    [System.Runtime.InteropServices.FieldOffsetAttribute(8)]
    private int fieldWithFieldOffsetAttribute;
}
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_CSharp_FieldOffsetAttributeError_NoDiagnostic()
        {
            VerifyCSharp(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
public class Class
{
    [System.Runtime.InteropServices.FieldOffsetAttribute]
    private int fieldWithFieldOffsetAttribute;
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_CSharp_StructLayoutAttribute_LayoutKindSequential_NoDiagnostic()
        {
            VerifyCSharp(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
class Class1
{
    private int field;
}

// System.Runtime.InteropServices.LayoutKind.Sequential has value 0
[System.Runtime.InteropServices.StructLayout((short)0)]
class Class2
{
    private int field;
}
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_CSharp_StructLayoutAttribute_LayoutKindAuto_Diagnostic()
        {
            VerifyCSharp(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
class Class
{
    private int field;
}
",
    // Test0.cs(5,17): warning CA1823: Unused field 'field'.
    GetCSharpResultAt(5, 17, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_CSharp_StructLayoutAttribute_LayoutKindExplicit_Diagnostic()
        {
            VerifyCSharp(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
class Class
{
    private int field;
}
", TestValidationMode.AllowCompileErrors,
    // Test0.cs(5,17): warning CA1823: Unused field 'field'.
    GetCSharpResultAt(5, 17, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_CSharp_StructLayoutAttributeError_NoLayoutKind_Diagnostic()
        {
            VerifyCSharp(@"
[System.Runtime.InteropServices.StructLayout]
class Class1
{
    private int field;
}

[System.Runtime.InteropServices.StructLayout(1000)]
class Class2
{
    private int field;
}
", TestValidationMode.AllowCompileErrors,
    // Test0.cs(5,17): warning CA1823: Unused field 'field'.
    GetCSharpResultAt(5, 17, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"),
    // Test0.cs(11,17): warning CA1823: Unused field 'field'.
    GetCSharpResultAt(11, 17, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"));
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public void CA1823_CSharp_MEFAttributes_NoDiagnostic()
        {
            VerifyCSharp(CSharpMEFAttributesDefinition + @"
public class Class
{
    [System.Composition.ExportAttribute]
    private int fieldWithMefV1ExportAttribute;

    [System.ComponentModel.Composition.ExportAttribute]
    private int fieldWithMefV2ExportAttribute;
}
");
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public void CA1823_CSharp_MEFAttributesError_NoDiagnostic()
        {
            VerifyCSharp(CSharpMEFAttributesDefinition + @"
public class Class
{
    [System.Composition.ExportAttribute(0)]
    private int fieldWithMefV1ExportAttribute;

    [System.ComponentModel.Composition.ExportAttribute(0)]
    private int fieldWithMefV2ExportAttribute;
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public void CA1823_CSharp_MEFAttributesUndefined_Diagnostic()
        {
            VerifyCSharp(@"
public class Class
{
    [System.Composition.ExportAttribute]
    private int fieldWithMefV1ExportAttribute;

    [System.ComponentModel.Composition.ExportAttribute]
    private int fieldWithMefV2ExportAttribute;
}
", TestValidationMode.AllowCompileErrors,
    // Test0.cs(5,17): warning CA1823: Unused field 'fieldWithMefV1ExportAttribute'.
    GetCSharpResultAt(5, 17, AvoidUnusedPrivateFieldsAnalyzer.Rule, "fieldWithMefV1ExportAttribute"),
    // Test0.cs(8,17): warning CA1823: Unused field 'fieldWithMefV2ExportAttribute'.
    GetCSharpResultAt(8, 17, AvoidUnusedPrivateFieldsAnalyzer.Rule, "fieldWithMefV2ExportAttribute"));
        }

        [Fact]
        public void CA1823_CSharp_SimpleUsages_DiagnosticCases()
        {
            VerifyCSharp(@"
public class Class
{
    private string fileName = ""data.txt"";
    private int Used1 = 10;
    private int Used2;
    private int Unused1 = 20;
    private int Unused2;
    public int Unused3;

    public string FileName()
    {
        return fileName;
    }

    private int Value => Used1 + Used2;
}
",
            GetCA1823CSharpResultAt(7, 17, "Unused1"),
            GetCA1823CSharpResultAt(8, 17, "Unused2"));
        }

        [Fact]
        public void CA1823_VisualBasic_DiagnosticCases()
        {
            VerifyBasic(@"
Public Class Class1
    Private fileName As String
    Private Used1 As Integer = 10
    Private Used2 As Integer
    Private Unused1 As Integer = 20
    Private Unused2 As Integer
    Public Unused3 As Integer

    Public Function MyFileName() As String
        Return filename
    End Function

    Public ReadOnly Property MyValue As Integer
        Get
            Return Used1 + Used2
        End Get
    End Property
End Class
",
            GetCA1823BasicResultAt(6, 13, "Unused1"),
            GetCA1823BasicResultAt(7, 13, "Unused2"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_VisualBasic_FieldOffsetAttribute_NoDiagnostic()
        {
            VerifyBasic(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)> _
Public Class [Class]
    <System.Runtime.InteropServices.FieldOffsetAttribute(8)> _
    Private fieldWithFieldOffsetAttribute As Integer
End Class
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_VisualBasic_FieldOffsetAttributeError_NoDiagnostic()
        {
            VerifyBasic(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)> _
Public Class [Class]
    <System.Runtime.InteropServices.FieldOffsetAttribute(8)> _
    Private fieldWithFieldOffsetAttribute As Integer
End Class
", TestValidationMode.AllowCompileErrors);
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_VisualBasic_StructLayoutAttribute_LayoutKindSequential_NoDiagnostic()
        {
            VerifyBasic(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)> _
Public Class Class1
    Private field As Integer
End Class

' System.Runtime.InteropServices.LayoutKind.Sequential has value 0
<System.Runtime.InteropServices.StructLayout(0)> _
Public Class Class2
    Private field As Integer
End Class
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_VisualBasic_StructLayoutAttribute_LayoutKindAuto_Diagnostic()
        {
            VerifyBasic(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)> _
Public Class [Class]
    Private field As Integer
End Class
",
    // Test0.vb(4,13): warning CA1823: Unused field 'field'.
    GetBasicResultAt(4, 13, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_VisualBasic_StructLayoutAttribute_LayoutKindExplicit_Diagnostic()
        {
            VerifyBasic(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)> _
Public Class [Class]
    Private field As Integer
End Class
", TestValidationMode.AllowCompileErrors,
    // Test0.vb(4,13): warning CA1823: Unused field 'field'.
    GetBasicResultAt(4, 13, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public void CA1823_VisualBasic_StructLayoutAttributeError_NoLayoutKind_Diagnostic()
        {
            VerifyBasic(@"
<System.Runtime.InteropServices.StructLayout> _
Public Class Class1
    Private field As Integer
End Class

<System.Runtime.InteropServices.StructLayout(1000)> _
Public Class Class2
    Private field As Integer
End Class
", TestValidationMode.AllowCompileErrors,
    // Test0.vb(4,13): warning CA1823: Unused field 'field'.
    GetBasicResultAt(4, 13, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"),
    // Test0.vb(9,13): warning CA1823: Unused field 'field'.
    GetBasicResultAt(9, 13, AvoidUnusedPrivateFieldsAnalyzer.Rule, "field"));
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public void CA1823_VisualBasic_MEFAttributes_NoDiagnostic()
        {
            VerifyBasic(BasicMEFAttributesDefinition + @"
Public Class [Class]
    <System.Composition.ExportAttribute> _
    Private fieldWithMefV1ExportAttribute As Integer

    <System.ComponentModel.Composition.ExportAttribute> _
    Private fieldWithMefV2ExportAttribute As Integer
End Class
");
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public void CA1823_VisualBasic_MEFAttributesError_NoDiagnostic()
        {
            VerifyBasic(BasicMEFAttributesDefinition + @"
Public Class [Class]
    <System.Composition.ExportAttribute(0)> _
    Private fieldWithMefV1ExportAttribute As Integer

    <System.ComponentModel.Composition.ExportAttribute(0)> _
    Private fieldWithMefV2ExportAttribute As Integer
End Class
", TestValidationMode.AllowCompileErrors);
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public void CA1823_VisualBasic_MEFAttributesUndefined_Diagnostic()
        {
            VerifyBasic(@"
Public Class [Class]
    <System.Composition.ExportAttribute> _
    Private fieldWithMefV1ExportAttribute As Integer

    <System.ComponentModel.Composition.ExportAttribute> _
    Private fieldWithMefV2ExportAttribute As Integer
End Class
", TestValidationMode.AllowCompileErrors,
        // Test0.vb(4,13): warning CA1823: Unused field 'fieldWithMefV1ExportAttribute'.
        GetBasicResultAt(4, 13, AvoidUnusedPrivateFieldsAnalyzer.Rule, "fieldWithMefV1ExportAttribute"),
        // Test0.vb(7,13): warning CA1823: Unused field 'fieldWithMefV2ExportAttribute'.
        GetBasicResultAt(7, 13, AvoidUnusedPrivateFieldsAnalyzer.Rule, "fieldWithMefV2ExportAttribute"));
        }

        private static DiagnosticResult GetCA1823CSharpResultAt(int line, int column, string fieldName)
        {
            return GetCSharpResultAt(line, column, AvoidUnusedPrivateFieldsAnalyzer.RuleId, string.Format(MicrosoftCodeQualityAnalyzersResources.AvoidUnusedPrivateFieldsMessage, fieldName));
        }

        private static DiagnosticResult GetCA1823BasicResultAt(int line, int column, string fieldName)
        {
            return GetBasicResultAt(line, column, AvoidUnusedPrivateFieldsAnalyzer.RuleId, string.Format(MicrosoftCodeQualityAnalyzersResources.AvoidUnusedPrivateFieldsMessage, fieldName));
        }
    }
}