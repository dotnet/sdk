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
        public async Task TestPreviewPropertyGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class {|#2:AFoo|}<T> where T : Foo, new() // Highlight Foo
    {
        [RequiresPreviewFeatures]
        private Foo _value;

        public Foo Value // Highlight Foo. Nice to have to collapse 0 and 1 into Value itself
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
        public async Task TestPreviewPropertySetter()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class AFoo
    {
        private int _value;

        public int Value
        {
            get
            {
                return _value;
            }
            [RequiresPreviewFeatures]
            set
            {
                _value = value;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            AFoo prog = new AFoo();
            {|#0:prog.Value|} = 1;
        }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("set_Value"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewPropertyGetter()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class AFoo
    {
        private int _value;

        public int Value
        {
            [RequiresPreviewFeatures]
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            AFoo prog = new AFoo();
            int value = {|#0:prog.Value|};
        }
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("get_Value"));
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

        public Foo Value // Highlight Foo. Nice to have: Collapse 0 and 1 into Value itself, though this might lead to 2 diagnostics on Value.
        {
            {|#2:get|}
            {
                return _value;
            }
            {|#3:set|}
            {
                _value = value;
            }
        }

        public Foo AnotherGetter => {|#4:_value|};
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
        public decimal Value => 1.1m;
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
        decimal IFoo.Value => 1.1m;
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
                return 1.1m;
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
    }
}
