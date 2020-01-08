// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class AvoidUnsealedAttributeTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new AvoidUnsealedAttributesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AvoidUnsealedAttributesAnalyzer();
        }

        #region Diagnostic Tests

        [Fact]
        public void CA1813CSharpDiagnosticProviderTestFired()
        {
            VerifyCSharp(@"
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
            GetCA1813CSharpResultAt(6, 18),
            GetCA1813CSharpResultAt(10, 19));
        }

        [Fact]
        public void CA1813CSharpDiagnosticProviderTestFiredWithScope()
        {
            VerifyCSharp(@"
using System;

[|public class AttributeClass: Attribute
{
}|]

public class Outer
{
    private class AttributeClass2: Attribute
    {
    }
}
",
            GetCA1813CSharpResultAt(4, 14));
        }

        [Fact]
        public void CA1813CSharpDiagnosticProviderTestNotFired()
        {
            VerifyCSharp(@"
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
        public void CA1813VisualBasicDiagnosticProviderTestFired()
        {
            VerifyBasic(@"
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
            GetCA1813BasicResultAt(4, 14),
            GetCA1813BasicResultAt(9, 19));
        }

        [Fact]
        public void CA1813VisualBasicDiagnosticProviderTestFiredwithScope()
        {
            VerifyBasic(@"
Imports System

Public Class AttributeClass
    Inherits Attribute
End Class

Public Class Outer
    [|Private Class AttributeClass2
        Inherits Attribute
    End Class|]
End Class
",
            GetCA1813BasicResultAt(9, 19));
        }

        [Fact]
        public void CA1813VisualBasicDiagnosticProviderTestNotFired()
        {
            VerifyBasic(@"
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

        private static DiagnosticResult GetCA1813CSharpResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, AvoidUnsealedAttributesAnalyzer.RuleId, MicrosoftNetCoreAnalyzersResources.AvoidUnsealedAttributesMessage);
        }

        private static DiagnosticResult GetCA1813BasicResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, AvoidUnsealedAttributesAnalyzer.RuleId, MicrosoftNetCoreAnalyzersResources.AvoidUnsealedAttributesMessage);
        }
    }
}
