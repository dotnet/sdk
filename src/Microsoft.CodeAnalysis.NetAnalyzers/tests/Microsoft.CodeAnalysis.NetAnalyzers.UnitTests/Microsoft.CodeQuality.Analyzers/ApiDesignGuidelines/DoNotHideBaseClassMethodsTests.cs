// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotHideBaseClassMethodsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpDoNotHideBaseClassMethodsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotHideBaseClassMethodsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicDoNotHideBaseClassMethodsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotHideBaseClassMethodsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotHideBaseClassMethodsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotHideBaseClassMethodsAnalyzer();
        }

        [Fact]
        public void CA1061_DerivedMethodMatchesBaseMethod_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(string input)
    {
    }
}

class Derived : Base
{
    public void Method(string input)
    {
    }
}");

            VerifyBasic(@"
Class Base
    Public Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base
    
    Public Sub Method(input As String)
    End Sub
End Class");
        }

        [Fact]
        public void CA1061_DerivedMethodHasMoreDerivedParameter_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(object input)
    {
    }
}

class Derived : Base
{
    public void Method(string input)
    {
    }
}");

            VerifyBasic(@"
Class Base
    Public Sub Method(input As Object)
    End Sub
End Class

Class Derived
    Inherits Base
    
    Public Sub Method(input As String)
    End Sub
End Class");
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_Diagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(string input)
    {
    }
}

class Derived : Base
{
    public void Method(object input)
    {
    }
}",
                GetCA1061CSharpResultAt(11, 17, "Derived.Method(object)", "Base.Method(string)"));

            VerifyBasic(@"
Class Base
    Public Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base
    
    Public Sub Method(input As Object)
    End Sub
End Class",
                GetCA1061BasicResultAt(10, 16, "Public Sub Method(input As Object)", "Public Sub Method(input As String)"));
        }

        [Fact]
        public void CA1061_ConstructorCallsBaseConstructorWithDifferentParameterType_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public Base(string input)
    {
    }
}
class Derived : Base
{
    public Derived(object input)
        :base(null)
    {
    }
}
");
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_MultipleMethodsHidden_Diagnostics()
        {
            VerifyCSharp(@"
class Parent
{
    public void Method(string input)
    {
    }
}

class Child : Parent
{
    public void Method(string input)
    {
    }
}

class Grandchild : Child
{
    public void Method(object input)
    {
    }
}",
                GetCA1061CSharpResultAt(18, 17, "Grandchild.Method(object)", "Child.Method(string)"),
                GetCA1061CSharpResultAt(18, 17, "Grandchild.Method(object)", "Parent.Method(string)"));

            VerifyBasic(@"
Class Parent
    Public Sub Method(input As String)
    End Sub
End Class

Class Child
    Inherits Parent

    Public Sub Method(input as String)
    End Sub
End Class

Class Grandchild
    Inherits Child
    
    Public Sub Method(input As Object)
    End Sub
End Class",
                GetCA1061BasicResultAt(17, 16, "Public Sub Method(input As Object)", "Public Sub Method(input As String)"),
                GetCA1061BasicResultAt(17, 16, "Public Sub Method(input As Object)", "Public Sub Method(input As String)"));
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_ImplementsInterface_CompileError()
        {
            VerifyCSharp(@"
interface IFace
{
    void Method(string input);
}

class Derived : IFace
{
    public void Method(object input)
    {
    }
}",
                TestValidationMode.AllowCompileErrors);

            VerifyBasic(@"
Interface IFace
    Sub Method(input As String)
End Interface

Class Derived
    Implements IFace

    Public Sub Method(input As Object) Implements IFace.Method
    End Sub
End Class",
                TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_OverridesVirtualBaseMethod_CompileError()
        {
            VerifyCSharp(@"
class Base
{
    public virtual void Method(string input);
}

class Derived : Base
{
    public override void Method(object input)
    {
    }
}",
                TestValidationMode.AllowCompileErrors);

            VerifyBasic(@"
Class Base
    Public Overridable Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base
    
    Public Overrides Sub Method(input As Object)
    End Sub
End Class",
                TestValidationMode.AllowCompileErrors);
        }


        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_OverridesAbstractBaseMethod_CompileError()
        {
            VerifyCSharp(@"
abstract class Base
{
    public abstract void Method(string input);
}

class Derived : Base
{
    public override void Method(object input)
    {
    }
}",
                TestValidationMode.AllowCompileErrors);

            VerifyBasic(@"
MustInherit Class Base
    Public MustOverride Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base
    
    Public Overrides Sub Method(input As Object)
    End Sub
End Class",
                TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_DerivedMethodPrivate_Diagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(string input)
    {
    }
}

class Derived : Base
{
    private void Method(object input)
    {
    }
}",
                GetCA1061CSharpResultAt(11, 18, "Derived.Method(object)", "Base.Method(string)"));

            VerifyBasic(@"
Class Base
    Public Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base

    Public Sub Method(input As Object)
    End Sub
End Class
",
                GetCA1061BasicResultAt(10, 16, "Public Sub Method(input As Object)", "Public Sub Method(input As String)"));
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_BaseMethodPrivate_NoDiagnostic()
        {
            // Note: This behavior differs from FxCop's CA1061
            VerifyCSharp(@"
class Base
{
    private void Method(string input)
    {
    }
}

class Derived : Base
{
    public void Method(object input)
    {
    }
}");

            VerifyBasic(@"
Class Base
    Private Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base

    Public Sub Method(input As Object)
    End Sub
End Class
");
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_ArityMismatch_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(string input, string input2)
    {
    }
}

class Derived : Base
{
    public void Method(object input)
    {
    }
}");

            VerifyBasic(@"
Class Base
    Private Sub Method(input As String, input2 As String)
    End Sub
End Class

Class Derived
    Inherits Base

    Public Sub Method(input As Object)
    End Sub
End Class
");
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_ReturnTypeMismatch_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(string input)
    {
    }
}

class Derived : Base
{
    public int Method(object input)
    {
        return 0;
    }
}");

            VerifyBasic(@"
Class Base
    Private Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base

    Public Function Method(input As Object) As Integer
        Method = 0
    End Function
End Class
");
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_ParameterTypeMismatchAtStart_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(int input, string input2)
    {
    }
}

class Derived : Base
{
    public void Method(char input, object input2)
    {
    }
}");

            VerifyBasic(@"
Class Base
    Private Sub Method(input As Integer, input2 As String)
    End Sub
End Class

Class Derived
    Inherits Base

    Public Sub Method(input As Char, input2 As Object)
    End Sub
End Class
");
        }

        [Fact]
        public void CA1061_DerivedMethodHasLessDerivedParameter_ParameterTypeMismatchAtEnd_NoDiagnostic()
        {
            VerifyCSharp(@"
class Base
{
    public void Method(string input, int input2)
    {
    }
}

class Derived : Base
{
    public void Method(object input, char input2)
    {
    }
}");

            VerifyBasic(@"
Class Base
    Private Sub Method(input As String, input2 As Integer)
    End Sub
End Class

Class Derived
    Inherits Base

    Public Sub Method(input As Object, input2 As Char)
    End Sub
End Class
");
        }

        private DiagnosticResult GetCA1061CSharpResultAt(int line, int column, string derivedMethod, string baseMethod)
        {
            return GetCSharpResultAt(line, column, DoNotHideBaseClassMethodsAnalyzer.Rule, derivedMethod, baseMethod);
        }

        private DiagnosticResult GetCA1061BasicResultAt(int line, int column, string derivedMethod, string baseMethod)
        {
            return GetBasicResultAt(line, column, DoNotHideBaseClassMethodsAnalyzer.Rule, derivedMethod, baseMethod);
        }
    }
}
