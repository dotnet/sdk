// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AvoidUnsealedAttributeTests
    {
        #region Diagnostic Tests

        [Fact]
        public async Task CA1813CSharpDiagnosticProviderTestFired()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public class AttributeClass: Attribute
    {
    }

    private class AttributeClass2: Attribute
    {
    }
}
",
            GetCSharpResultAt(6, 18),
            GetCSharpResultAt(10, 19));
        }

        [Fact]
        public async Task CA1813CSharpDiagnosticProviderTestFiredWithScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class [|AttributeClass|]: Attribute
{
}

public class Outer
{
    private class [|AttributeClass2|]: Attribute
    {
    }
}
");
        }

        [Fact]
        public async Task CA1813CSharpDiagnosticProviderTestNotFired()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public sealed class AttributeClass: Attribute
{
    private abstract class AttributeClass2: Attribute
    {
        public abstract void F();
    }
}");
        }

        [Fact]
        public async Task CA1813VisualBasicDiagnosticProviderTestFired()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class AttributeClass
    Inherits Attribute
End Class

Public Class Outer
    Private Class AttributeClass2
        Inherits Attribute
    End Class
End Class
",
            GetBasicResultAt(4, 14),
            GetBasicResultAt(9, 19));
        }

        [Fact]
        public async Task CA1813VisualBasicDiagnosticProviderTestFiredwithScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class [|AttributeClass|]
    Inherits Attribute
End Class

Public Class Outer
    Private Class [|AttributeClass2|]
        Inherits Attribute
    End Class
End Class
");
        }

        [Fact]
        public async Task CA1813VisualBasicDiagnosticProviderTestNotFired()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public NotInheritable Class AttributeClass
    Inherits Attribute

    Private MustInherit Class AttributeClass2
        Inherits Attribute
        MustOverride Sub F()
    End Class
End Class
");
        }

        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic()
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
