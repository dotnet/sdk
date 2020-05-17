// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Usage.ProvideCorrectArgumentToEnumHasFlag,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Usage.ProvideCorrectArgumentToEnumHasFlag,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    public class ProvideCorrectArgumentToEnumHasFlagTests
    {
        [Fact]
        public async Task CA2248_EnumTypesAreDifferent_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    [Flags]
    public enum MyEnum { A, B, }

    [Flags]
    public enum OtherEnum { A, }

    public void Method(MyEnum m)
    {
        {|#0:m.HasFlag(OtherEnum.A)|};
    }
}",
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("OtherEnum", "MyEnum"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    <Flags>
    Public Enum MyEnum
        A
        B
    End Enum

    <Flags>
    Public Enum OtherEnum
        A
    End Enum

    Public Sub Method(ByVal m As MyEnum)
        {|#0:m.HasFlag(OtherEnum.A)|}
    End Sub
End Class
",
                VerifyVB.Diagnostic().WithLocation(0).WithArguments("OtherEnum", "MyEnum"));
        }

        [Fact]
        public async Task CA2248_EnumTypesAreSame_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    [Flags]
    public enum MyEnum { A, B, }

    public void Method(MyEnum m)
    {
        m.HasFlag(MyEnum.A);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    <Flags>
    Public Enum MyEnum
        A
        B
    End Enum

    Public Sub Method(ByVal m As MyEnum)
        m.HasFlag(MyEnum.A)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2248_EnumTypesAreSameButNotFlag_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public enum MyEnum { A, B, }

    public void Method(MyEnum m)
    {
        m.HasFlag(MyEnum.A);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public Enum MyEnum
        A
        B
    End Enum

    Public Sub Method(ByVal m As MyEnum)
        m.HasFlag(MyEnum.A)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2248_EnumTypesAreDifferentAndNotFlags_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public enum MyEnum { A, B, }

    public enum OtherEnum { A, }

    public void Method(MyEnum m)
    {
        [|m.HasFlag(OtherEnum.A)|];
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public Enum MyEnum
        A
        B
    End Enum

    Public Enum OtherEnum
        A
    End Enum

    Public Sub Method(ByVal m As MyEnum)
        [|m.HasFlag(OtherEnum.A)|]
    End Sub
End Class
");
        }
    }
}
