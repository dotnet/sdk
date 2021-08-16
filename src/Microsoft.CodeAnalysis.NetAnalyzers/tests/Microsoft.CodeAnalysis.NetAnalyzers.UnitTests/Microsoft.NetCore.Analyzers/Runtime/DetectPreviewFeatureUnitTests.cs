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

        [Fact]
        public async Task TestPreviewMethodUnaryOperator()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{" +
@"
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
        public async Task TestCatchPreviewException()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{" +
@"

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
        public async Task TestArrayOrPreviewTypes()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"
            public class Program
            {
                static void Main(string[] args)
                {
                    Lib[] array = {|#0:new Lib[] { }|};
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
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewMethodBinaryOperator()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"
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

        [Fact]
        public async Task TestUnmarkedPreviewPropertyCallingPreviewProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"
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
        public async Task TestUnmarkedPreviewMethodCallingPreviewMethod()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"
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
        {" +
@"
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
        public async Task TestDerivedClassExtendsUnmarkedClass()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("UnmarkedVirtualMethodInPreviewClass"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMethodInvocation_Simple()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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
        {" +
@"

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("PreviewMethod"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewMethod"));
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
        {" +
@"

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Program"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("FooBar"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("BarImplemented"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(3).WithArguments("Bar"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestUnmarkedPreviewProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

            class Program : IProgram
            {
                static void Main(string[] args)
                {
                    new Program();
                }

                public bool {|#0:MarkedPropertyInInterface|} { get => throw new NotImplementedException(); set => throw new NotImplementedException(); } // [] if not opted in yet
            }

            public interface IProgram
            {
                [RequiresPreviewFeatures]
                bool MarkedPropertyInInterface { get; set; }
            }
        }

            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("MarkedPropertyInInterface"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestUnmarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("MarkedMethodInInterface"));
            await test.RunAsync();

        }

        [Fact]
        public async Task TestPreviewLanguageFeatures()
        {
            var csInput = @" 
                using System.Runtime.Versioning; using System;
                namespace Preview_Feature_Scratch
                {" +
@"

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

        [Fact]
        public async Task TestMarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

            class Program : IProgram
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("UnmarkedMethodInMarkedInterface"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMarkedEmptyPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsEmptyPreviewInterfaceRule).WithLocation(0).WithArguments("Program"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestInterfaceMethodInvocation()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(4).WithArguments("FooDelegate"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestField()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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

        [Fact]
        public async Task TestProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("AProperty"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDelegate()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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

        [Fact]
        public async Task TestEnumValue()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

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
        {" +
@"

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
        {" +
@"

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

        [Fact]
        public async Task TestEvent()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {" +
@"

            class Program
            {
                public Program()
                {
                }

                public delegate void SampleEventHandler(object sender, bool e);

                [RequiresPreviewFeatures]
                public static event SampleEventHandler SampleEvent;

                static void Main(string[] args)
                {
                    {|#0:SampleEvent|}?.Invoke(new Program(), new bool());
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("SampleEvent"));
            await test.RunAsync();
        }
    }
}