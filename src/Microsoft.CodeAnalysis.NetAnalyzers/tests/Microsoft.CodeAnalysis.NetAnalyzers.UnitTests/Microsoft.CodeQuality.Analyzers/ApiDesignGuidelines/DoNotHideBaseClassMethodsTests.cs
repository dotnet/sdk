// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotHideBaseClassMethodsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpDoNotHideBaseClassMethodsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotHideBaseClassMethodsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicDoNotHideBaseClassMethodsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotHideBaseClassMethodsTests
    {
        [Fact]
        public async Task CA1061_DerivedMethodMatchesBaseMethod_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasMoreDerivedParameter_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_ConstructorCallsBaseConstructorWithDifferentParameterType_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_MultipleMethodsHidden_Diagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_ImplementsInterface_CompileError()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
interface IFace
{
    void Method(string input);
}

class Derived : {|CS0535:IFace|}
{
    public void Method(object input)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Interface IFace
    Sub Method(input As String)
End Interface

Class Derived
    Implements {|BC30149:IFace|}

    Public Sub Method(input As Object) Implements {|BC30401:IFace.Method|}
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_OverridesVirtualBaseMethod_CompileError()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class Base
{
    public virtual void {|CS0501:Method|}(string input);
}

class Derived : Base
{
    public override void {|CS0115:Method|}(object input)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Class Base
    Public Overridable Sub Method(input As String)
    End Sub
End Class

Class Derived
    Inherits Base
    
    Public Overrides Sub {|BC30284:Method|}(input As Object)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_OverridesAbstractBaseMethod_CompileError()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
abstract class Base
{
    public abstract void Method(string input);
}

class {|CS0534:Derived|} : Base
{
    public override void {|CS0115:Method|}(object input)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
MustInherit Class Base
    Public MustOverride Sub Method(input As String)
    {|BC30429:End Sub|}
End Class

Class {|BC30610:Derived|}
    Inherits Base
    
    Public Overrides Sub {|BC30284:Method|}(input As Object)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_DerivedMethodPrivate_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_BaseMethodPrivate_NoDiagnostic()
        {
            // Note: This behavior differs from FxCop's CA1061
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_ArityMismatch_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_ReturnTypeMismatch_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_ParameterTypeMismatchAtStart_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA1061_DerivedMethodHasLessDerivedParameter_ParameterTypeMismatchAtEnd_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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

        private static DiagnosticResult GetCA1061CSharpResultAt(int line, int column, string derivedMethod, string baseMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(derivedMethod, baseMethod);

        private static DiagnosticResult GetCA1061BasicResultAt(int line, int column, string derivedMethod, string baseMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(derivedMethod, baseMethod);
    }
}
