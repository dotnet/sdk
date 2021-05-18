// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidUnusedPrivateFieldsFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class AvoidUnusedPrivateFieldsTests
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

        [Fact]
        public async Task CA1823_CSharp_AttributeUsage_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[System.Obsolete(Message)]
public class Class
{
    private const string Message = ""Test"";
}
");
        }

        [Fact]
        public async Task CA1823_CSharp_InterpolatedStringUsage_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class
{
    private const string Message = ""Test"";
    public string PublicMessage = $""Test: {Message}"";
}
");
        }

        [Fact]
        public async Task CA1823_CSharp_CollectionInitializerUsage_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class Class
{
    private const string Message = ""Test"";
    public List<string> PublicMessage = new List<string> { Message };
}
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_CSharp_FieldOffsetAttribute_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
public class Class
{
    [System.Runtime.InteropServices.FieldOffsetAttribute(8)]
    private int fieldWithFieldOffsetAttribute;
}
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_CSharp_FieldOffsetAttributeError_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
public class Class
{
    [{|CS7036:System.Runtime.InteropServices.FieldOffsetAttribute|}]
    private int {|CS0625:fieldWithFieldOffsetAttribute|};
}
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_CSharp_StructLayoutAttribute_LayoutKindSequential_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CA1823_CSharp_StructLayoutAttribute_LayoutKindAuto_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
class Class
{
    private int field;
}
",
                GetCA1823CSharpResultAt(5, 17, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_CSharp_StructLayoutAttribute_LayoutKindExplicit_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
class Class
{
    private int {|CS0625:field|};
}
",
                GetCA1823CSharpResultAt(5, 17, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_CSharp_StructLayoutAttributeError_NoLayoutKind_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
[{|CS1729:System.Runtime.InteropServices.StructLayout|}]
class Class1
{
    private int field;
}

[System.Runtime.InteropServices.StructLayout(1000)]
class Class2
{
    private int field;
}
",
                GetCA1823CSharpResultAt(5, 17, "field"),
                GetCA1823CSharpResultAt(11, 17, "field"));
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public async Task CA1823_CSharp_MEFAttributes_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSharpMEFAttributesDefinition + @"
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
        public async Task CA1823_CSharp_MEFAttributesError_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(CSharpMEFAttributesDefinition + @"
public class Class
{
    [{|CS1729:System.Composition.ExportAttribute(0)|}]
    private int fieldWithMefV1ExportAttribute;

    [{|CS1729:System.ComponentModel.Composition.ExportAttribute(0)|}]
    private int fieldWithMefV2ExportAttribute;
}
");
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public async Task CA1823_CSharp_MEFAttributesUndefined_Diagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Default,
                TestCode = @"
public class Class
{
    [System.{|CS0234:Composition|}.ExportAttribute]
    private int fieldWithMefV1ExportAttribute;

    [System.ComponentModel.{|CS0234:Composition|}.ExportAttribute]
    private int fieldWithMefV2ExportAttribute;
}
",
                ExpectedDiagnostics =
                {
                    GetCA1823CSharpResultAt(5, 17, "fieldWithMefV1ExportAttribute"),
                    GetCA1823CSharpResultAt(8, 17, "fieldWithMefV2ExportAttribute"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1823_CSharp_SimpleUsages_DiagnosticCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CA1823_VisualBasic_DiagnosticCases()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1823_VisualBasic_FieldOffsetAttribute_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)> _
Public Class [Class]
    <System.Runtime.InteropServices.FieldOffsetAttribute(8)> _
    Private fieldWithFieldOffsetAttribute As Integer
End Class
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_VisualBasic_FieldOffsetAttributeError_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)>
Public Class [Class]
    <System.Runtime.InteropServices.{|BC30455:FieldOffsetAttribute|}>
    Private fieldWithFieldOffsetAttribute As Integer
End Class
");
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_VisualBasic_StructLayoutAttribute_LayoutKindSequential_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1823_VisualBasic_StructLayoutAttribute_LayoutKindAuto_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)> _
Public Class [Class]
    Private field As Integer
End Class
",
                GetCA1823BasicResultAt(4, 13, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_VisualBasic_StructLayoutAttribute_LayoutKindExplicit_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)> _
Public Class [Class]
    Private field As Integer
End Class
",
                GetCA1823BasicResultAt(4, 13, "field"));
        }

        [Fact, WorkItem(1219, "https://github.com/dotnet/roslyn-analyzers/issues/1219")]
        public async Task CA1823_VisualBasic_StructLayoutAttributeError_NoLayoutKind_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
<System.Runtime.InteropServices.{|BC30516:StructLayout|}> _
Public Class Class1
    Private field As Integer
End Class

<System.Runtime.InteropServices.{|BC30519:StructLayout|}(1000)> _
Public Class Class2
    Private field As Integer
End Class
",
                GetCA1823BasicResultAt(4, 13, "field"),
                GetCA1823BasicResultAt(9, 13, "field"));
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public async Task CA1823_VisualBasic_MEFAttributes_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(BasicMEFAttributesDefinition + @"
Public Class [Class]
    <System.Composition.ExportAttribute> _
    Private fieldWithMefV1ExportAttribute As Integer

    <System.ComponentModel.Composition.ExportAttribute> _
    Private fieldWithMefV2ExportAttribute As Integer
End Class
");
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public async Task CA1823_VisualBasic_MEFAttributesError_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(BasicMEFAttributesDefinition + @"
Public Class [Class]
    <System.Composition.ExportAttribute({|BC30057:0|})> _
    Private fieldWithMefV1ExportAttribute As Integer

    <System.ComponentModel.Composition.ExportAttribute({|BC30057:0|})> _
    Private fieldWithMefV2ExportAttribute As Integer
End Class
");
        }

        [Fact, WorkItem(1217, "https://github.com/dotnet/roslyn-analyzers/issues/1217")]
        public async Task CA1823_VisualBasic_MEFAttributesUndefined_Diagnostic()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Default,
                TestCode = @"
Public Class [Class]
    <{|BC30002:System.Composition.ExportAttribute|}> _
    Private fieldWithMefV1ExportAttribute As Integer

    <{|BC30002:System.ComponentModel.Composition.ExportAttribute|}> _
    Private fieldWithMefV2ExportAttribute As Integer
End Class
",
                ExpectedDiagnostics =
                {
                    GetCA1823BasicResultAt(4, 13, "fieldWithMefV1ExportAttribute"),
                    GetCA1823BasicResultAt(7, 13, "fieldWithMefV2ExportAttribute"),
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCA1823CSharpResultAt(int line, int column, string fieldName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(fieldName);

        private static DiagnosticResult GetCA1823BasicResultAt(int line, int column, string fieldName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(fieldName);
    }
}