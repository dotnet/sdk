// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
        public async Task TestPropertyGetterSetInConstructor()
        {
            var csInput = @"
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    class Program
    {
        public int Value { get; }
        public Program()
        {
            Value = 1;
        }

        static void Main(string[] args)
        {
        }
    }
}";
            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewPropertyGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class AFoo<T> where T : {|#2:Foo|}, new()
    {
        [RequiresPreviewFeatures]
        private Foo _value;

        public {|#0:Foo|} Value
        {
            get
            {
                return {|#4:_value|};
            }
            {|#1:set|}
            {
                {|#5:_value|} = value;
            }
        }

        public {|#3:Foo|} AnotherGetter => {|#6:_value|};
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(0).WithArguments("get_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(1).WithArguments("set_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(2).WithArguments("AFoo", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(3).WithArguments("get_AnotherGetter", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(4).WithArguments("_value", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(5).WithArguments("_value", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(6).WithArguments("_value", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
Imports System
Imports System.Runtime.Versioning
Imports System.Collections.Generic
Module Preview_Feature_Scratch
    Public Class AFoo(Of T As {{|#2:Foo|}, New})
        <RequiresPreviewFeatures>
        Private _value As Foo

        Public Property Value() As {|#0:Foo|}
            Get
                Return {|#4:_value|}
            End Get
            Set(ByVal value As {|#1:Foo|})
                {|#5:_value|} = value
            End Set
        End Property
    End Class

    <RequiresPreviewFeatures>
    Public Class Foo

    End Class
End Module
            ";

            var testVb = TestVB(vbInput);
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(0).WithArguments("get_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(1).WithArguments("set_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(2).WithArguments("AFoo", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(4).WithArguments("_value", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(5).WithArguments("_value", DetectPreviewFeatureAnalyzer.DefaultURL));
            await testVb.RunAsync();
        }

        [Fact]
        public async Task TestGenericPreviewPropertyGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
using System.Collections.Generic;
namespace Preview_Feature_Scratch
{

    class AFoo<T> where T : {|#2:Foo|}, new()
    {
        private List<{|#3:Foo|}> _value;

        public List<{|#0:Foo|}> Value
        {
            get
            {
                return _value;
            }
            {|#1:set|}
            {
                _value = value;
            }
        }
#nullable enable
        private List<{|#6:Foo|}?>? _valueNullable;

        public List<{|#4:Foo|}?>? ValueNullable
        {
            get
            {
                return _valueNullable;
            }
            {|#5:set|}
            {
                _valueNullable = value;
            }
        }
#nullable disable
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(0).WithArguments("get_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(1).WithArguments("set_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(2).WithArguments("AFoo", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(3).WithArguments("_value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(6).WithArguments("_valueNullable", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(4).WithArguments("get_ValueNullable", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(5).WithArguments("set_ValueNullable", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("set_Value", DetectPreviewFeatureAnalyzer.DefaultURL));
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("get_Value", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestNullablePropertyReturnTypePreviewGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    class AFoo
    {
#nullable enable
        private {|#5:Foo|}[]? _valueNullable;
        private {|#8:Foo?|}[]? _valueNullableArray;
        private {|#9:Foo?|}[] _valueNullableArrayInitialized;
        public {|#6:Foo|}[]? ValueNullable
        {
            get
            {
                return _valueNullable;
            }
            {|#7:set|}
            {
                _valueNullable = value;
            }
        }
        public AFoo()
        {
            _valueNullableArrayInitialized = {|#10:new Foo?[0]|};
        }
#nullable disable
    }
    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(5).WithArguments("_valueNullable", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(6).WithArguments("get_ValueNullable", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(7).WithArguments("set_ValueNullable", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(8).WithArguments("_valueNullableArray", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(9).WithArguments("_valueNullableArrayInitialized", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(10).WithArguments("Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPropertyReturnTypePreviewGetterAndSetters()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    class AFoo<T> where T : {|#0:Foo|}, new()
    {
        private {|#1:Foo|} _value;

        public {|#2:Foo|} Value
        {
            get
            {
                return _value;
            }
            {|#3:set|}
            {
                _value = value;
            }
        }

        public {|#4:Foo|} AnotherGetter => _value;
    }

    [RequiresPreviewFeatures]
    public class Foo
    {
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(0).WithArguments("AFoo", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(1).WithArguments("_value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(2).WithArguments("get_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodUsesPreviewTypeAsParameterRule).WithLocation(3).WithArguments("set_Value", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.MethodReturnsPreviewTypeRule).WithLocation(4).WithArguments("get_AnotherGetter", "Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPropertyGetterFromInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class Foo : {|#0:IFoo|}
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestExplicitPropertyGetterFromInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class Foo : {|#0:IFoo|}
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
Imports System.Runtime.Versioning
Imports System

Namespace Preview_Feature_Scratch
    Public Class Foo
        Implements {|#0:IFoo|}

        <RequiresPreviewFeatures>
        Public ReadOnly Property Value As Decimal Implements IFoo.Value
            Get
                Return 1.1D
            End Get
        End Property
    End Class

    <RequiresPreviewFeatures>
    Interface IFoo
        ReadOnly Property Value As Decimal
    End Interface
End Namespace
";
            var vbTest = TestVB(vbInput);
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await vbTest.RunAsync();
        }

        [Fact]
        public async Task TestExplicitPropertyGetMethodFromInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class Foo : {|#0:IFoo|}
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Foo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("get_AGetter", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("set_AGetter", DetectPreviewFeatureAnalyzer.DefaultURL));
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedPropertyInInterface", "IProgram.MarkedPropertyInInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(1).WithArguments("AProperty", "Program.AProperty", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestRefProperty()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                private bool _foo;
                private bool _aProperty;

                [RequiresPreviewFeatures]
                private ref bool Foo => ref _foo;

                [RequiresPreviewFeatures]
                public virtual ref bool AProperty => ref _aProperty;

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
                private bool _aProperty;

                public override ref bool {|#1:AProperty|} => ref _aProperty;
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(1).WithArguments("AProperty", "Program.AProperty", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestIndexer()
        {
            var csInput = @"using System;
        using System.Runtime.Versioning;

        namespace Preview_Feature_Scratch
        {
            class Program
            {
                private bool[] _value;

                [RequiresPreviewFeatures]
                public bool this[int index] => _value[index];

                static void Main(string[] args)
                {
                    Program prog = new Program();
                    bool x = {|#0:prog[0]|};

                    Base @base = new Base();
                    bool y = {|#1:@base[0]|};

                    Derived derived = new Derived();
                    bool z = derived[0];
                }
            }

            class Base
            {
                private bool[] _value;

                [RequiresPreviewFeatures]
                public virtual bool this[int index] => _value[index];
            }

            class Derived : Base
            {
                private bool[] _value;

                public override bool {|#2:this|}[int index] => _value[index];
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("this[]", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("this[]", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.OverridesPreviewMethodRule).WithLocation(2).WithArguments("this[]", "Base.this[]", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }
    }
}
