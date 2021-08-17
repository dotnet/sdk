// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class DetectPreviewFeatureUnitTests
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

        [Fact]
        public async Task TestArrayOfArraysOfPreviewTypes()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            public class Program
            {
                static void Main(string[] args)
                {
                    Lib[][] array = {|#0:new Lib[][] {}|};
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

[{|#0:My|}]
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

[RequiresPreviewFeatures]
[My(Feature = ""This is a feature"")]
class classUsingFeatureAndGuarded
{
}

[My(true, Feature = ""This is a feature"")]
class classUsingFeature
{
}

[My(true, {|#1:PreviewFeature|} = ""This is a feature"")]
class classUsingPreviewFeature
{
}

[RequiresPreviewFeatures]
[My(true, PreviewFeature = ""This is a feature"")]
class classUsingBoolFeatureAndGuarded
{
}

[AttributeUsage(AttributeTargets.All)]
class MyAttribute : Attribute
{
    [RequiresPreviewFeatures]
    public MyAttribute() {}

    public MyAttribute(bool foo) {}

    public string Feature { get; set; }

    [RequiresPreviewFeatures]
    public string PreviewFeature { get; set; }
}
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("MyAttribute"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewFeature"));
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

        public void CallBackMethod(Action<Foo> {|#5:action|})
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