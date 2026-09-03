// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class DetectPreviewFeatureUnitTests
    {
        [Fact]
        public async Task TestClassImplementingAbstractClassThatImplementsAnInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{" +
    @"

    class Program
    {
        static void Main(string[] args)
        {
            BImplementation b = new BImplementation();
            ((IZoo)b).Bar();
        }
    }

    public class BImplementation : BAbstract
    {

    }

    public abstract class BAbstract : IZoo
    {
    }

    interface IZoo : {|#0:IFoo|}
    { 
        bool Bar() { return true; }
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        bool Bar() { return true; }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("IZoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDerivedClassExtendsUnmarkedClass()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            public partial class UnmarkedPreviewClass
            {
                [RequiresPreviewFeatures]
                public virtual void UnmarkedVirtualMethodInPreviewClass() { }
            }

            public partial class Derived : UnmarkedPreviewClass
            {
                public override void {|#0:UnmarkedVirtualMethodInPreviewClass|}()
                {
                    throw new NotImplementedException();
                }
            }
        }
        ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(0).WithArguments("UnmarkedVirtualMethodInPreviewClass", "UnmarkedPreviewClass.UnmarkedVirtualMethodInPreviewClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Theory]
        [InlineData("class")]
        [InlineData("struct")]
        public async Task TestClassOrStruct(string classOrStruct)
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@$"

            [RequiresPreviewFeatures]
            {classOrStruct} Program
            {{
                static void Main(string[] args)
                {{
                    new Program();
                }}
            }}
        }}";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Theory]
        [InlineData("class")]
        [InlineData("record")]
        public async Task TestAbstractType(string type)
        {
            var csInput = $@" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {{

            {type} Program : {{|#0:AbClass|}}
            {{
                static void Main(string[] args)
                {{
                    Program prog = new Program();
                    prog.Bar();
                    {{|#1:prog.FooBar()|}};
                    {{|#2:prog.BarImplemented()|}};
                }}

                public override void {{|#3:Bar|}}()
                {{
                    throw new NotImplementedException();
                }}

                [RequiresPreviewFeatures]
                public override void FooBar()
                {{
                    throw new NotImplementedException();
                }}
            }}

            [RequiresPreviewFeatures]
            public abstract {type} AbClass
            {{
                [RequiresPreviewFeatures]
                public abstract void Bar();

                [RequiresPreviewFeatures]
                public abstract void FooBar();

                [RequiresPreviewFeatures]
                public void BarImplemented() => throw new NotImplementedException();
            }}
        }}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.DerivesFromPreviewClassRule).WithLocation(0).WithArguments("Program", "AbClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("FooBar", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("BarImplemented", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(3).WithArguments("Bar", "AbClass.Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact, WorkItem(5802, "https://github.com/dotnet/roslyn-analyzers/issues/5802")]
        public async Task TestPartialClassWithFirstDeclarationNotHavingBaseTypes()
        {
            var test = TestCS(@"
using System;
using System.Runtime.Versioning;

partial class Program { }

partial class Program : {|#0:AbClass|} { }

[RequiresPreviewFeatures]
public class AbClass
{
}
");
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.DerivesFromPreviewClassRule).WithLocation(0).WithArguments("Program", "AbClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Theory]
        [InlineData("class")]
        [InlineData("struct")]
        public async Task TestPartialClassOrStructDeclaration(string classOrStruct)
        {
            var csInput = @$" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{{
    public partial {classOrStruct} Zoo"; // Zoo: IFoo

            csInput += @": {|#0:IFoo|}
    {
        public void {|#1:Foo|}() { }
    }
";
            csInput += $@"
    public partial {classOrStruct} Zoo : NonPreviewInterface";

            csInput += @"
    {
        public void Bar() { }
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Foo();
    }

    interface NonPreviewInterface
    {
        void Bar();
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Zoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(1).WithArguments("Foo", "IFoo.Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Theory]
        [InlineData("class")]
        [InlineData("struct")]
        public async Task TestPartialClassOrStructDeclarationEndOfList(string classOrStruct)
        {
            var csInput = @$" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{{
    public partial {classOrStruct} Zoo";

            csInput += @": NonPreviewInterface, {|#0:IFoo|}
    {
        public void {|#1:Foo|}() { }
        public void Bar() { }
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Foo();
    }

    interface NonPreviewInterface
    {
        void Bar();
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Zoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(1).WithArguments("Foo", "IFoo.Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Theory]
        [InlineData("class")]
        [InlineData("struct")]
        public async Task TestPartialClassOrStructImplementsMultiplePreviewInterfaces(string classOrStruct)
        {
            var csInput = @$" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{{
    public partial {classOrStruct} Zoo";

            csInput += @": {|#3:PreviewInterface|}, NonPreviewInterface, {|#0:IFoo|}
    {
        public void {|#1:Foo|}() { }
        public void {|#2:Bar|}() { }
        public void NonPreviewInterfaceMethod() { }
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Foo();
    }

    [RequiresPreviewFeatures]
    interface PreviewInterface
    {
        void Bar();
    }

    interface NonPreviewInterface
    {
        void NonPreviewInterfaceMethod();
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Zoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(3).WithArguments("Zoo", "PreviewInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(1).WithArguments("Foo", "IFoo.Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(2).WithArguments("Bar", "PreviewInterface.Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPartialClassDeclarationInterfacesAndAbstractClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public partial class Zoo : NonPreviewInterface, {|#0:IFoo|}
    {
        public void {|#1:Foo|}() { }
        public void Bar() { }
    }

    public partial class Zoo : {|#2:AbClass|}
    {
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Foo();
    }

    interface NonPreviewInterface
    {
        void Bar();
    }

    [RequiresPreviewFeatures]
    public abstract class AbClass
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Zoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(1).WithArguments("Foo", "IFoo.Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.DerivesFromPreviewClassRule).WithLocation(2).WithArguments("Zoo", "AbClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
Imports System.Runtime.Versioning
Imports System
Imports System.Collections.Generic

Namespace Preview_Feature_Scratch
    Public Partial Class Zoo
        Implements NonPreviewInterface, {|#0:PreviewInterface|}

        Private _field As List(Of {|#1:PreviewType|})
    End Class

    Public Partial Class Zoo
        Inherits {|#2:PreviewType|}
    End Class

    <RequiresPreviewFeatures>
    Interface PreviewInterface
    End Interface

    Interface NonPreviewInterface
    End Interface

    <RequiresPreviewFeatures>
    Public MustInherit Class PreviewType
    End Class
End Namespace
";
            var vbTest = TestVB(vbInput);
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Zoo", "PreviewInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(1).WithArguments("_field", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.DerivesFromPreviewClassRule).WithLocation(2).WithArguments("Zoo", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            await vbTest.RunAsync();
        }
    }
}
