// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DetectPreviewFeatureUnitTests
    {
        private static VerifyCS.Test TestCS(string csInput)
        {
            return new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
                TestState =
                {
                    Sources =
                    {
                        csInput
                    }
                },
                ReferenceAssemblies = AdditionalMetadataReferences.Net60,
            };
        }

        private static VerifyCS.Test TestCSPreview(string csInput)
        {
            return new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                TestState =
                {
                    Sources =
                    {
                        csInput
                    }
                },
                ReferenceAssemblies = AdditionalMetadataReferences.Net60,
            };
        }

        // TODO: Unit tests for events of preview types

        #region Operators
        [Fact]
        public async Task TestPreviewMethodUnaryOperator()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class Program
    {
        static void Main(string[] args)
        {
            var a = new Fraction();
            var b = {|#0:+a|};
        }
    }

    public readonly struct Fraction
    {
        [RequiresPreviewFeatures]
        public static Fraction operator +(Fraction a) => a;
    }
}
";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("op_UnaryPlus"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewMethodBinaryOperator()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            public class Program
            {
                static void Main(string[] args)
                {
                    var a = new Fraction();
                    var b = new Fraction();
                    b = {|#0:b + a|};
                }
            }

            public readonly struct Fraction
            {
                [RequiresPreviewFeatures]
                public static Fraction operator +(Fraction a, Fraction b) => a;
            }
        }
        ";
            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("op_Addition"));
            await test.RunAsync();
        }
        #endregion

        [Fact]
        public async Task TestCatchPreviewException()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    [RequiresPreviewFeatures]
    public class DerivedException : Exception
    {

    }

    public class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(""Foo"");
            }
            catch {|#0:(DerivedException ex)|}
            {
                throw;
            }
        }
    }
}
";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("DerivedException"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestArrayOfPreviewTypes()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            public class Program
            {
                static void Main(string[] args)
                {
                    Lib[] array = {|#0:new Lib[] { }|};
                    Lib anObject = {|#1:new()|};
                }
            }

            [RequiresPreviewFeatures]
            public class Lib
            {
            }
        }
        ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Lib"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("Lib"));
            await test.RunAsync();
        }

        #region Properties

        [Fact]
        public async Task TestPreviewPropertyGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class {|#2:AFoo|}<T> where T : Foo, new()
    {
        [RequiresPreviewFeatures]
        private Foo _value;

        public Foo Value
        {
            {|#0:get|}
            {
                return {|#4:_value|};
            }
            {|#1:set|}
            {
                {|#5:_value|} = value;
            }
        }

        public Foo AnotherGetter => {|#6:{|#3:_value|}|};
    }

    class Program
    {
        static void Main(string[] args)
        {
            Program prog = new Program();
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(0).WithArguments("get_Value", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(1).WithArguments("set_Value", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(2).WithArguments("AFoo", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(3).WithArguments("get_AnotherGetter", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(4).WithArguments("_value"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(5).WithArguments("_value"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(6).WithArguments("_value"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPropertyReturnTypePreviewGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class {|#0:AFoo|}<T> where T : Foo, new()
    {
        private Foo {|#1:_value|};

        public Foo Value
        {
            {|#2:get|}
            {
                return _value; // No diagnostic here for field reference to reduce noise. We just look to see if _value has the attribute on it
            }
            {|#3:set|}
            {
                _value = value;
            }
        }

        public Foo AnotherGetter => {|#4:_value|};
    }

    class Program
    {
        static void Main(string[] args)
        {
            Program prog = new Program();
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(0).WithArguments("AFoo", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(1).WithArguments("_value", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(2).WithArguments("get_Value", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(3).WithArguments("set_Value", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(4).WithArguments("get_AnotherGetter", "Foo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPropertyGetterFromInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class {|#0:Foo|} : IFoo
    {
        [RequiresPreviewFeatures]
        public decimal Value => 1.1m; // No diagnostic
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        decimal Value { get; }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestExplicitPropertyGetterFromInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class {|#0:Foo|} : IFoo
    {
        [RequiresPreviewFeatures]
        decimal IFoo.Value => 1.1m; // No diagnostic
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        decimal Value { get; }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestExplicitPropertyGetMethodFromInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class {|#0:Foo|} : IFoo
    {
        [RequiresPreviewFeatures]
        decimal IFoo.Value
        {
            get
            {
                return 1.1m; // No diagnostic
            }
        }
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        decimal Value { get; }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestUnmarkedPreviewPropertyCallingPreviewProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
        [RequiresPreviewFeatures]
        public class Program
        {
            public bool CallSite => UnmarkedPreviewClass.SomeStaticProperty;
        }

        public class UnmarkedPreviewClass
        {
                [RequiresPreviewFeatures]
                public static bool SomeStaticProperty => false;
        }
        }
        ";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewGetterAndSetter()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            class Program
            {
                static void Main(string[] args)
                {
                    Program program = new Program();
                    bool getter = {|#0:program.AGetter|};
                    {|#1:program.AGetter|} = true;
                }
                private bool _field;
                public bool AGetter
                {
                    [RequiresPreviewFeatures]
                    get
                    {
                        return true;
                    }
                    [RequiresPreviewFeatures]
                    set
                    {
                        _field = value;
                    }
                }
            }
        }
            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("get_AGetter"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("set_AGetter"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestUnmarkedPreviewProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program : IProgram
            {
                static void Main(string[] args)
                {
                    new Program();
                }

                public bool {|#0:MarkedPropertyInInterface|} { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            }

            public interface IProgram
            {
                [RequiresPreviewFeatures]
                bool MarkedPropertyInInterface { get; set; }
            }
        }

            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedPropertyInInterface", "IProgram.MarkedPropertyInInterface"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                [RequiresPreviewFeatures]
                private bool Foo => true;

                [RequiresPreviewFeatures]
                public virtual bool AProperty => true;

                static void Main(string[] args)
                {
                    Program prog = new Program();
                    bool foo = {|#0:prog.Foo|};

                    Derived derived = new Derived();
                    bool prop = derived.AProperty;
                }
            }

            class Derived: Program
            {
                public override bool {|#1:AProperty|} => true;
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(1).WithArguments("AProperty", "Program.AProperty"));
            await test.RunAsync();
        }

        #endregion

        #region Methods

        [Fact]
        public async Task TestPreviewParametersToPreviewMethod()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    [RequiresPreviewFeatures]
    class Program
    {
        public Foo Getter(Foo foo)
        {
            return foo;
        }

        static void Main(string[] args)
        {
            Program prog = new Program();
            prog.Getter(new Foo());
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewParametersToMethods()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        public Foo {|#2:Getter|}(Foo {|#0:foo|})
        {
            return foo;
        }

        static void Main(string[] args)
        {
            Program prog = new Program();
            prog.Getter({|#1:new Foo()|});
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(0).WithArguments("Getter", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(2).WithArguments("Getter", "Foo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestUnmarkedPreviewMethodCallingPreviewMethod()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
        [RequiresPreviewFeatures]
        public class Program
        {
            public bool CallSite()
            {
                return UnmarkedPreviewClass.SomeStaticMethod();
            }
        }

        public class UnmarkedPreviewClass
        {
                [RequiresPreviewFeatures]
                public static bool SomeStaticMethod()
                {
                    return false;
                }
        }
        }
        ";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewMethodCallingPreviewMethod()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
        public class Program
        {
            [RequiresPreviewFeatures]
            public virtual void PreviewMethod()  { }

            [RequiresPreviewFeatures]
            void CallSite()
            {
                PreviewMethod();
            }
        }
        }
        ";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMethodInvocation_Simple()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            public class Program
            {
                [RequiresPreviewFeatures]
                public virtual void PreviewMethod()
                {

                }

                static void Main(string[] args)
                {
                    var prog = new Program();
                    {|#0:prog.PreviewMethod()|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("PreviewMethod"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMethodInvocation_DeclareDerivedMethod()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            public class Program
            {
                [RequiresPreviewFeatures]
                public virtual void PreviewMethod()
                {

                }

                static void Main(string[] args)
                {
                }
            }

            public class Derived : Program
            {
                public Derived() : base()
                {
                }

                public override void {|#0:PreviewMethod|}()
                {
                    {|#1:base.PreviewMethod()|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(0).WithArguments("PreviewMethod", "Program.PreviewMethod"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewMethod"));
            await test.RunAsync();
        }

        #endregion

        #region Class

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

        #endregion

        #region Interfaces
        [Fact]
        public async Task TestUnmarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program : IProgram
            {
                static void Main(string[] args)
                {
                    new Program();
                }

                public void {|#0:MarkedMethodInInterface|}()
                {
                    throw new NotImplementedException();
                }        
            }

            public interface IProgram
            {
                [RequiresPreviewFeatures]
                void MarkedMethodInInterface();
            }
        }

            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedMethodInInterface", "IProgram.MarkedMethodInInterface"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class {|#1:Program|} : IProgram
            {
                static void Main(string[] args)
                {
                    new Program();
                }

                public void {|#0:UnmarkedMethodInMarkedInterface|}() { }

            }

            [RequiresPreviewFeatures]
            public interface IProgram
            {
                public void UnmarkedMethodInMarkedInterface() { }
            }
        }

            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("UnmarkedMethodInMarkedInterface", "IProgram.UnmarkedMethodInMarkedInterface"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(1).WithArguments("Program", "IProgram"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMarkedEmptyPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class {|#0:Program|} : IProgram
            {
                static void Main(string[] args)
                {
                    new Program();
                }
            }

            [RequiresPreviewFeatures]
            public interface IProgram
            {
            }
        }

            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Program", "IProgram"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDerivedInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    interface {|#0:IZoo|} : IFoo
    {
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Bar();
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("IZoo", "IFoo"));
            await test.RunAsync();
        }

        #endregion

        #region StaticAbstract
        [Fact]
        public async Task TestPreviewLanguageFeaturesHeirarchy()
        {
            var csInput = @" 
                using System.Runtime.Versioning; using System;
                namespace Preview_Feature_Scratch
                {

                    [RequiresPreviewFeatures]
                    class Program : IProgram
                    {
                        static void Main(string[] args)
                        {
                            new Program();
                        }

                        public static bool StaticMethod() => throw null;
                        public static bool AProperty => throw null;
                    }

                    [RequiresPreviewFeatures]
                    public interface IProgram
                    {
                        public static abstract bool StaticMethod();
                        public static abstract bool AProperty { get; }
                    }
                }

                    ";

            var test = TestCSPreview(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewLanguageFeatures()
        {
            var csInput = @" 
                using System.Runtime.Versioning; using System;
                namespace Preview_Feature_Scratch
                {

                    class Program : IProgram
                    {
                        static void Main(string[] args)
                        {
                            new Program();
                        }

                        public static bool StaticMethod() => throw null;
                        public static bool AProperty => throw null;
                    }

                    public interface IProgram
                    {
                        public static abstract bool {|#0:StaticMethod|}();
                        public static abstract bool {|#1:AProperty|} { {|#2:get|}; }
                    }
                }

                    ";

            var test = TestCSPreview(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.StaticAbstractIsPreviewFeatureRule).WithLocation(0).WithArguments("StaticMethod"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.StaticAbstractIsPreviewFeatureRule).WithLocation(1).WithArguments("AProperty"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.StaticAbstractIsPreviewFeatureRule).WithLocation(2).WithArguments("get"));
            await test.RunAsync();
        }

        #endregion

        [Fact]
        public async Task TestInterfaceMethodInvocation()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program : IProgram
            {
                static void Main(string[] args)
                {
                    Program progObject = new Program();
                    IProgram prog = progObject;
                    {|#0:prog.Foo()|};
                    {|#1:prog.FooDelegate()|};
                    bool prop = {|#2:prog.AProperty|};
                    bool anotherProp = {|#3:progObject.AnotherInterfaceProperty|};
                    Console.WriteLine(""prop.ToString() + anotherProp.ToString()"");
                }

                public IProgram.IProgramDelegate {|#4:FooDelegate|}()
                {
                    throw new NotImplementedException();
                }

                [RequiresPreviewFeatures]
                public bool AnotherInterfaceProperty { get; set; }
            }

            public interface IProgram
            {
                [RequiresPreviewFeatures]
                public bool AProperty => true;

                public bool AnotherInterfaceProperty { get; set; }

                public delegate void IProgramDelegate();

                [RequiresPreviewFeatures]
                public void Foo()
                {
                    throw new NotImplementedException();
                }

                [RequiresPreviewFeatures]
                public IProgramDelegate FooDelegate();

            }
        }

            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("FooDelegate"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("AProperty"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(3).WithArguments("AnotherInterfaceProperty"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(4).WithArguments("FooDelegate", "IProgram.FooDelegate"));
            await test.RunAsync();
        }

        #region Fields

        [Fact]
        public async Task TestPreviewTypeField()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                private PreviewType {|#0:_field|};
                private AGenericClass<PreviewType> {|#2:_genericPreviewField|};
                private AGenericClass<bool> _noDiagnosticField;

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            class PreviewType
            {

            }

            class AGenericClass<T>
            {

            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestArrayOrGenericPreviewFields()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            public class Program
            {
                private PreviewType {|#0:_field|};
                private AGenericClass<PreviewType>[] {|#2:_genericPreviewField|};

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            public class PreviewType
            {

            }

            public class {|#3:AGenericClass|}<T>
                where T : PreviewType
            {

            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(3).WithArguments("AGenericClass", "PreviewType"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewTypeArrayOfGenericPreviewFields()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            public class Program
            {
                private PreviewType {|#0:_field|};
                private AGenericClass<PreviewType>[] {|#3:{|#2:_genericPreviewField|}|};

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            public class PreviewType
            {

            }

            [RequiresPreviewFeatures]
            public class AGenericClass<T>
                where T : PreviewType
            {

            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(3).WithArguments("_genericPreviewField", "AGenericClass"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestField()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                [RequiresPreviewFeatures]
                private bool _field;

                public Program()
                {
                    {|#0:_field|} = true;
                } 

                static void Main(string[] args)
                {
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("_field"));
            await test.RunAsync();
        }

        #endregion

        [Fact]
        public async Task TestDelegate()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                [RequiresPreviewFeatures]
                public delegate void Del();

                static void Main(string[] args)
                {
                    Del del = {|#0:new(() => { })|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Del"));
            await test.RunAsync();
        }

        #region Enums

        [Fact]
        public async Task TestEnumValue()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            enum AnEnum
            {
                [RequiresPreviewFeatures]
                Foo,
                Bar
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                    AnEnum fooEnum = {|#0:AnEnum.Foo|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEnumValue_NoDiagnostic()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            enum AnEnum
            {
                Foo,
                [RequiresPreviewFeatures]
                Bar
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                    AnEnum fooEnum = AnEnum.Foo;
                }
            }
        }";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEnum()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            [RequiresPreviewFeatures]
            enum AnEnum
            {
                Foo,
                Bar
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                    AnEnum fooEnum = {|#0:AnEnum.Foo|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            await test.RunAsync();
        }

        #endregion

        #region Events

        [Fact]
        public async Task TestEventWithPreviewAddAndRemove()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
           class Publisher
           {
               public static event EventHandler<EventArgs> RaiseCustomEvent
               {
                   [RequiresPreviewFeatures]
                   add { }
                   [RequiresPreviewFeatures]
                   remove { }
               }
           }
        
           class Subscriber
           {
               private readonly string _id;
        
               public Subscriber(string id, Publisher pub)
               {
                   _id = id;
        
                   {|#0:Publisher.RaiseCustomEvent += HandleCustomEvent|};
                   {|#1:Publisher.RaiseCustomEvent -= HandleCustomEvent|};
               }
        
               void HandleCustomEvent(object sender, EventArgs e)
               {
               }
           }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("add_RaiseCustomEvent"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("remove_RaiseCustomEvent"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEventWithCustomAddAndRemove()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
           class Publisher
           {
               [RequiresPreviewFeatures]
               public static event EventHandler<EventArgs> RaiseCustomEvent
               {
                   add { }
                   remove { }
               }
        
               public static void EventHandler(object sender, EventArgs e) { }
           }
        
           class Subscriber
           {
               private readonly string _id;
        
               public Subscriber(string id, Publisher pub)
               {
                   _id = id;
        
                   {|#0:Publisher.RaiseCustomEvent|} += HandleCustomEvent;
                   {|#1:Publisher.RaiseCustomEvent|} -= HandleCustomEvent;
               }
        
               void HandleCustomEvent(object sender, EventArgs e)
               {
               }
           }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("RaiseCustomEvent"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("RaiseCustomEvent"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEvent()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                public Program()
                {
                }

                public delegate void SampleEventHandler(object sender, bool e);

                [RequiresPreviewFeatures]
                public static event SampleEventHandler StaticSampleEvent;

                [RequiresPreviewFeatures]
                public event SampleEventHandler SampleEvent;

                public static void HandleEvent(object sender, bool e)
                {

                }
                static void Main(string[] args)
                {
                    {|#0:StaticSampleEvent|}?.Invoke(new Program(), new bool());

                    Program program = new Program();
                    {|#1:program.SampleEvent|} += HandleEvent;
                    {|#2:program.SampleEvent|} -= HandleEvent;
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("StaticSampleEvent"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("SampleEvent"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("SampleEvent"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEventWithPreviewEventHandler()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            class Publisher
            {
                public event EventHandler<PreviewEventArgs> {|#0:RaiseCustomEvent|};
         
                public void DoSomething()
                {
                    OnRaiseCustomEvent({|#1:new PreviewEventArgs()|});
                }
         
                protected virtual void OnRaiseCustomEvent(EventArgs e)
                {
                    EventHandler<PreviewEventArgs> raiseEvent = RaiseCustomEvent;
         
                    if (raiseEvent != null)
                    {
                    }
                }
            }
         
            [RequiresPreviewFeatures]
            public class PreviewEventArgs : EventArgs
            {
         
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                }
            }

            class Subscriber
            {
                private readonly string _id;
         
                public Subscriber(string id, Publisher pub)
                {
                    _id = id;
         
                    pub.RaiseCustomEvent += {|#2:HandleCustomEvent|};
                    pub.RaiseCustomEvent -= {|#3:HandleCustomEvent|};
                }
         
                // Define what actions to take when the event is raised.
                void HandleCustomEvent(object sender, EventArgs e)
                {
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("RaiseCustomEvent", "PreviewEventArgs"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewEventArgs"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("PreviewEventArgs"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(3).WithArguments("PreviewEventArgs"));
            await test.RunAsync();
        }

        #endregion

        [Fact]
        public async Task TestTypeOf()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine({|#0:typeof(IFoo)|});
        }
    }
    
    [RequiresPreviewFeatures]
    interface IFoo { }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("IFoo"));
            await test.RunAsync();
        }

        #region Generics

        [Fact]
        public async Task TestNonPreviewMethodWithGenericPreviewParameter()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        public bool GenericMethod<T>()
        {
            return true;
        }

        static void Main(string[] args)
        {
            Program program = new Program();
            {|#0:program.GenericMethod<Foo>()|};
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            await test.RunAsync();
        }
        [Fact]
        public async Task TestGenericMethodWithPreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        public bool {|#0:GenericMethod|}<T>()
            where T : Foo
        {
            return true;
        }

        static void Main(string[] args)
        {
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(0).WithArguments("GenericMethod", "Foo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericMethodInsidePreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    [RequiresPreviewFeatures]
    class Program
    {
        public bool GenericMethod<T>()
            where T : Foo
        {
            return true;
        }

        static void Main(string[] args)
        {
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestTwoLevelGenericMethodInsidePreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    [RequiresPreviewFeatures]
    class Program
    {
        class NestedClass
        {
            public bool GenericMethod<T>()
                where T : Foo
            {
                return true;
            }
        }

        static void Main(string[] args)
        {
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }

}";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClassWithoutPreviewInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A<Foo> aFooInstance = {|#0:new A<Foo>()|};
        }
    }

class A<T> where T : IFoo, new()
{
    public A()
    {
        new T().Bar();
    }
}

[RequiresPreviewFeatures]
class Foo : IFoo
{
    public void Bar() { }
}

interface IFoo
{
    void Bar();
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestGenericClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A<Foo> aFooInstance = {|#4:new A<Foo>()|};
        }
    }

class {|#1:A|}<T> where T : IFoo, new()
{
    public A()
    {
        IFoo foo = new T();
        {|#0:foo.Bar()|};
    }
}

class {|#2:Foo|} : IFoo
{
    public void {|#3:Bar|}() { }
}

[RequiresPreviewFeatures]
interface IFoo
{
    void Bar();
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Bar"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(1).WithArguments("A", "IFoo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(2).WithArguments("Foo", "IFoo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(3).WithArguments("Bar", "IFoo.Bar"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(4).WithArguments("IFoo"));
            await test.RunAsync();
        }

        #endregion

        [Fact]
        public async Task TestSimpleCustomAttributeOnPreviewClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A aObject = {|#0:new()|};
        }
    }

[RequiresPreviewFeatures]
[My]
class A
{
}

[RequiresPreviewFeatures]
[AttributeUsage(AttributeTargets.All)]
class MyAttribute : Attribute
{
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("A"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestSimpleCustomAttribute()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A aObject = new A();
        }
    }

[{|#1:My|}]
class A
{
}

[RequiresPreviewFeatures]
[AttributeUsage(AttributeTargets.All)]
class MyAttribute : Attribute
{
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("MyAttribute"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestCustomAttribute()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            A aObject = new A();
        }
    }

[{|#1:My|}]
class A
{
}

[My(true)]
class B
{
}

[RequiresPreviewFeatures]
[My]
class C
{
}


[AttributeUsage(AttributeTargets.All)]
class MyAttribute : Attribute
{
    [RequiresPreviewFeatures]
    public MyAttribute() {}

    public MyAttribute(bool foo) {}
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("MyAttribute"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDeepNesting()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            NestedClass0.NestedClass1.NestedClass2.NestedClass3 nestedClass3 = {|#0:new()|};
            {|#1:nestedClass3.AMethod()|};
            bool prop = {|#2:nestedClass3.AProperty|};
            prop = {|#3:nestedClass3.AField|};
        }
    }

    [RequiresPreviewFeatures]
    public class NestedClass0
    {
        public class NestedClass1
        {
            public class NestedClass2
            {
                public class NestedClass3
                {
                    public bool AMethod() => false;
                    public bool AProperty => false;
                    public bool AField = true;
                }
            }
        }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("NestedClass3"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("AMethod"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("AProperty"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(3).WithArguments("AField"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestNestedInvocation()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine({|#0:A.B()|});
        }
    }

class A
{
    [RequiresPreviewFeatures]
    public static bool B() => true;
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("B"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestNestedClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class Program
    {
        [RequiresPreviewFeatures]
        class NestedClass
        {

        }

        static void Main(string[] args)
        {
            NestedClass nestedClass = {|#0:new NestedClass()|};
        }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("NestedClass"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestCallback()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{" +
    @"

    class {|#2:AFoo|}<T> where T : Foo, new()
    {
        public Foo[] {|#1:_fooArray|};

        public void {|#5:CallBackMethod|}(Action<Foo> action)
        {
            foreach (var foo in _fooArray)
            {
                action(foo);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            AFoo<Foo> anObject = {|#4:new AFoo<Foo>()|};
            anObject.CallBackMethod({|#0:(Foo foo) => { }|});
        }
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(1).WithArguments("_fooArray", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(2).WithArguments("AFoo", "Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(4).WithArguments("Foo"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(5).WithArguments("CallBackMethod", "Foo"));
            await test.RunAsync();
        }
    }
}