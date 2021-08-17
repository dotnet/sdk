// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DetectPreviewFeatureAnalyzer,
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

    interface {|#0:IZoo|} : IFoo
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("IZoo", "IFoo"));
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(0).WithArguments("UnmarkedVirtualMethodInPreviewClass", "UnmarkedPreviewClass.UnmarkedVirtualMethodInPreviewClass"));
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

        [Fact]
        public async Task TestAbstractClass()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class {|#0:Program|} : AbClass
            {
                static void Main(string[] args)
                {
                    Program prog = new Program();
                    prog.Bar();
                    {|#1:prog.FooBar()|};
                    {|#2:prog.BarImplemented()|};
                }

                public override void {|#3:Bar|}()
                {
                    throw new NotImplementedException();
                }

                [RequiresPreviewFeatures]
                public override void FooBar()
                {
                    throw new NotImplementedException();
                }
            }

            [RequiresPreviewFeatures]
            public abstract class AbClass
            {
                [RequiresPreviewFeatures]
                public abstract void Bar();

                [RequiresPreviewFeatures]
                public abstract void FooBar();

                [RequiresPreviewFeatures]
                public void BarImplemented() => throw new NotImplementedException();
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.DerivesFromPreviewClassRule).WithLocation(0).WithArguments("Program", "AbClass"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("FooBar"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("BarImplemented"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(3).WithArguments("Bar", "AbClass.Bar"));
            await test.RunAsync();
        }
    }
}
