// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DetectPreviewFeatureUnitTests
    {
        private static async Task TestCS(string csInput)
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
                TestState =
                {
                    Sources =
                    {
                        csInput
                    }
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                ReferenceAssemblies = AdditionalMetadataReferences.Net60,
            }.RunAsync();
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
            var b = [|+a|];
        }
    }

    public readonly struct Fraction
    {
        [RequiresPreviewFeatures]
        public static Fraction operator +(Fraction a) => a;
    }
}
";

            await TestCS(csInput);
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
            catch [|(DerivedException ex)|]
            {
                throw;
            }
        }
    }
}
";

            await TestCS(csInput);
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
            Lib[] array = [|new Lib[] { }|];
        }
    }

    [RequiresPreviewFeatures]
    public class Lib
    {
    }
}
";

            await TestCS(csInput);
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
            b = [|b + a|];
        }
    }

    public readonly struct Fraction
    {
        [RequiresPreviewFeatures]
        public static Fraction operator +(Fraction a, Fraction b) => a;
    }
}
";
            await TestCS(csInput);
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

            await TestCS(csInput);
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

            await TestCS(csInput);
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

            await TestCS(csInput);
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
        public override void [|UnmarkedVirtualMethodInPreviewClass|]()
        {
            throw new NotImplementedException();
        }
    }
}
";

            await TestCS(csInput);
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
            [|prog.PreviewMethod()|];
        }
    }
}";

            await TestCS(csInput);
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

        public override void [|PreviewMethod|]()
        {
            [|base.PreviewMethod()|];
        }
    }
}";

            await TestCS(csInput);
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

            await TestCS(csInput);
        }

        [Fact]
        public async Task TestAbstractClass()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{" +
@"

    class [|Program|] : AbClass
    {
        static void Main(string[] args)
        {
            Program prog = new Program();
            prog.Bar();
            [|prog.FooBar()|];
            [|prog.BarImplemented()|];
        }

        public override void [|Bar|]()
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

            await TestCS(csInput);
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

        public bool [|MarkedPropertyInInterface|] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); } // [] if not opted in yet
    }

    public interface IProgram
    {
        [RequiresPreviewFeatures]
        bool MarkedPropertyInInterface { get; set; }
    }
}

    ";

            await TestCS(csInput);
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

        public void [|MarkedMethodInInterface|]()
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

            await TestCS(csInput);

        }

        [Fact(Skip = "The following test cannot be activated yet because it requires preview roslyn bits")]
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
            }

            public interface IProgram
            {
                public static abstract bool [|StaticMethod|]();
                public static abstract bool [|AProperty|] { [|get|]; }
            }
        }

            ";

            await TestCS(csInput);
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

        public void [|UnmarkedMethodInMarkedInterface|]() { }

    }

    [RequiresPreviewFeatures]
    public interface IProgram
    {
        public void UnmarkedMethodInMarkedInterface() { }
    }
}

    ";

            await TestCS(csInput);
        }

        [Fact]
        public async Task TestMarkedEmptyPreviewInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{" +
@"

    class [|Program|] : IProgram
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

            await TestCS(csInput);
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
            [|prog.Foo()|];
            [|prog.FooDelegate()|];
            bool prop = [|prog.AProperty|];
            bool anotherProp = [|progObject.AnotherInterfaceProperty|];
            Console.WriteLine(""prop.ToString() + anotherProp.ToString()"");
        }

        public IProgram.IProgramDelegate [|FooDelegate|]()
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

            await TestCS(csInput);
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
            [|_field|] = true;
        } 

        static void Main(string[] args)
        {
        }
    }
}";

            await TestCS(csInput);
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
            bool foo = [|prog.Foo|];

            Derived derived = new Derived();
            bool prop = derived.AProperty;
        }
    }

    class Derived: Program
    {
        public override bool [|AProperty|] => true;
    }
}";

            await TestCS(csInput);
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
            Del del = [|new(() => { })|];
        }
    }
}";

            await TestCS(csInput);

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
            AnEnum fooEnum = [|AnEnum.Foo|];
        }
    }
}";

            await TestCS(csInput);
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

            await TestCS(csInput);
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
            AnEnum fooEnum = [|AnEnum.Foo|];
        }
    }
}";

            await TestCS(csInput);
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
            [|SampleEvent|]?.Invoke(new Program(), new bool());
        }
    }
}";

            await TestCS(csInput);
        }
    }
}