// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerJavaScriptSerializerWithSimpleTypeResolver,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PropertySetAnalysis)]
    public class DoNotUseInsecureDeserializerJavascriptSerializerWithSimpleTypeResolverTests
    {
        private static readonly DiagnosticDescriptor DefinitelyRule = DoNotUseInsecureDeserializerJavaScriptSerializerWithSimpleTypeResolver.DefinitelyWithSimpleTypeResolver;
        private static readonly DiagnosticDescriptor MaybeRule = DoNotUseInsecureDeserializerJavaScriptSerializerWithSimpleTypeResolver.MaybeWithSimpleTypeResolver;

        [Fact]
        public async Task Deserialize_Generic_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(string str)
        {
            JavaScriptSerializer s = new JavaScriptSerializer(new SimpleTypeResolver());
            return s.Deserialize<T>(str);
        }
    }
}",
                GetCSharpResultAt(12, 20, DefinitelyRule, "T JavaScriptSerializer.Deserialize<T>(string input)"));
        }

        [Fact]
        public async Task Deserialize_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(string str)
        {
            JavaScriptSerializer s = new JavaScriptSerializer(new SimpleTypeResolver());
            return (T) s.Deserialize(str, typeof(T));
        }
    }
}",
                GetCSharpResultAt(12, 24, DefinitelyRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task DeserializeObject_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            JavaScriptSerializer s = new JavaScriptSerializer(new SimpleTypeResolver());
            return s.DeserializeObject(str);
        }
    }
}",
                GetCSharpResultAt(12, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_AnyPath_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str, bool flag)
        {
            JavaScriptSerializer s;
            if (flag)
                s = new JavaScriptSerializer(new SimpleTypeResolver());
            else
                s = new JavaScriptSerializer();
            return s.DeserializeObject(str);
        }
    }
}",
                GetCSharpResultAt(16, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task Deserialize_FromArgument_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(JavaScriptSerializer s, string str)
        {
            return (T) s.Deserialize(str, typeof(T));
        }
    }
}",
                GetCSharpResultAt(11, 24, MaybeRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task Deserialize_TypeResolver_Unknown_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(Func<JavaScriptTypeResolver> trFactory, string str)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer(trFactory());
            return (T) jss.Deserialize(str, typeof(T));
        }
    }
}",
                GetCSharpResultAt(13, 24, MaybeRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task Deserialize_TypeResolver_UnknownNotNull_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(Func<JavaScriptTypeResolver> trFactory, string str)
        {
            JavaScriptTypeResolver tr = trFactory();
            JavaScriptSerializer jss = tr != null ? new JavaScriptSerializer(tr) : new JavaScriptSerializer();
            return (T) jss.Deserialize(str, typeof(T));
        }
    }
}",
                GetCSharpResultAt(14, 24, MaybeRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task Deserialize_TypeResolver_UnknownNull_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(Func<JavaScriptTypeResolver> trFactory, string str)
        {
            JavaScriptTypeResolver tr = trFactory();
            JavaScriptSerializer jss = tr == null ? new JavaScriptSerializer(tr) : new JavaScriptSerializer();
            return (T) jss.Deserialize(str, typeof(T));
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_FromField_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public JavaScriptSerializer Serializer;

        public T D<T>(string str)
        {
            return (T) this.Serializer.Deserialize(str, typeof(T));
        }
    }
}",
                GetCSharpResultAt(13, 24, MaybeRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task Deserialize_FromStaticField_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public static JavaScriptSerializer Serializer;

        public T D<T>(string str)
        {
            return (T) Program.Serializer.Deserialize(str, typeof(T));
        }
    }
}",
                GetCSharpResultAt(13, 24, MaybeRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task Deserialize_NoTypeResolver_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T D<T>(string str)
        {
            return (T) new JavaScriptSerializer().Deserialize(str, typeof(T));
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_CustomTypeResolver_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class MyTypeResolver : JavaScriptTypeResolver
    {
        public override Type ResolveType(string id)
        {
            throw new NotImplementedException();
        }

        public override string ResolveTypeId(Type type)
        {
            throw new NotImplementedException();
        }
    }

    public class Program
    {
        public T D<T>(string str)
        {
            return (T) new JavaScriptSerializer(new MyTypeResolver()).Deserialize(str, typeof(T));
        }
    }
}");
        }

        [Fact]
        public async Task DeserializeObject_FromLocalFunction_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            return GetSerializer().DeserializeObject(str);

            JavaScriptSerializer GetSerializer() => new JavaScriptSerializer(new SimpleTypeResolver());
        }
    }
}",
            GetCSharpResultAt(11, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_SimpleTypeResolverFromParameter_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(SimpleTypeResolver str1, string str2)
        {
            return GetSerializer().DeserializeObject(str2);

            JavaScriptSerializer GetSerializer() => new JavaScriptSerializer(str1);
        }
    }
}",
                GetCSharpResultAt(11, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_JavaScriptTypeResolverFromParameter_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(JavaScriptTypeResolver jstr, string str)
        {
            return GetSerializer().DeserializeObject(str);

            JavaScriptSerializer GetSerializer() => new JavaScriptSerializer(jstr);
        }
    }
}",
               GetCSharpResultAt(11, 20, MaybeRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_SimpleTypeResolverFromLocalFunction_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            return new JavaScriptSerializer(GetTypeResolver()).DeserializeObject(str);

            JavaScriptTypeResolver GetTypeResolver() => new SimpleTypeResolver();
        }
    }
}",
               GetCSharpResultAt(11, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task Deserialize_InLocalFunction_SimpleTypeResolverFromLocalFunction_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str, Type t)
        {
            return Deserialize();

            JavaScriptTypeResolver GetTypeResolver() => new SimpleTypeResolver();

            object Deserialize() => new JavaScriptSerializer(GetTypeResolver()).Deserialize(str, t);
        }
    }
}",
               GetCSharpResultAt(16, 37, DefinitelyRule, "object JavaScriptSerializer.Deserialize(string input, Type targetType)"));
        }

        [Fact]
        public async Task DeserializeObject_InLambda_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            Func<string, object> f = (s) => new JavaScriptSerializer(GetTypeResolver()).DeserializeObject(s);
            return f(str);

            JavaScriptTypeResolver GetTypeResolver() => new SimpleTypeResolver();
        }
    }
}",
                  GetCSharpResultAt(12, 45, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_InLambda_CustomTypeResolver_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            Func<string, object> f = (s) => new JavaScriptSerializer(GetTypeResolver()).DeserializeObject(s);
            return f(str);

            JavaScriptTypeResolver GetTypeResolver() => new MyTypeResolver();
        }
    }

    public class MyTypeResolver : JavaScriptTypeResolver
    {
        public override Type ResolveType(string id)
        {
            throw new NotImplementedException();
        }

        public override string ResolveTypeId(Type type)
        {
            throw new NotImplementedException();
        }
    }
}");
        }

        [Fact]
        public async Task DeserializeObject_InOtherMethod_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            return D(GetTypeResolver(), str);

            JavaScriptTypeResolver GetTypeResolver() => new SimpleTypeResolver();
        }

        public object D(JavaScriptTypeResolver tr, string s)
        {
            return new JavaScriptSerializer(tr).DeserializeObject(s);
        }
    }
}",
                  GetCSharpResultAt(19, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_InOtherMethodThrice_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            JavaScriptTypeResolver tr = GetTypeResolver();
            D(tr, str);
            D(tr, str);
            return D(tr, str);

            JavaScriptTypeResolver GetTypeResolver() => new SimpleTypeResolver();
        }

        public object D(JavaScriptTypeResolver tr, string s)
        {
            return new JavaScriptSerializer(tr).DeserializeObject(s);
        }
    }
}",
                  GetCSharpResultAt(22, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_InOtherMethod_OnceDefinitely_OnceMaybe_DefinitelyDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public JavaScriptTypeResolver OtherTypeResolver { get; set; }

        public object D(string str)
        {
            JavaScriptTypeResolver tr = GetTypeResolver();
            D(tr, str);
            return D(OtherTypeResolver, str);

            JavaScriptTypeResolver GetTypeResolver() => new SimpleTypeResolver();
        }

        public object D(JavaScriptTypeResolver tr, string s)
        {
            return new JavaScriptSerializer(tr).DeserializeObject(s);
        }
    }
}",
                  GetCSharpResultAt(23, 20, DefinitelyRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Fact]
        public async Task DeserializeObject_InOtherMethod_CustomTypeResolver_MaybeDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public object D(string str)
        {
            return D(GetTypeResolver(), str);

            JavaScriptTypeResolver GetTypeResolver() => new MyTypeResolver();
        }

        public object D(JavaScriptTypeResolver tr, string s)
        {
            return new JavaScriptSerializer(tr).DeserializeObject(s);
        }
    }

    public class MyTypeResolver : JavaScriptTypeResolver
    {
        public override Type ResolveType(string id)
        {
            throw new NotImplementedException();
        }

        public override string ResolveTypeId(Type type)
        {
            throw new NotImplementedException();
        }
    }
}",
                  GetCSharpResultAt(19, 20, MaybeRule, "object JavaScriptSerializer.DeserializeObject(string input)"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = Des")]
        [InlineData(@"dotnet_code_quality.CA2321.excluded_symbol_names = Des
                      dotnet_code_quality.CA2322.excluded_symbol_names = Des")]
        [InlineData(@"dotnet_code_quality.CA2321.excluded_symbol_names = D*
                      dotnet_code_quality.CA2322.excluded_symbol_names = D*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = Des")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var test = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.IO;
using System.Web.Script.Serialization;

namespace Blah
{
    public class Program
    {
        public T Des<T>(string str)
        {
            JavaScriptSerializer s = new JavaScriptSerializer(new SimpleTypeResolver());
            return s.Deserialize<T>(str);
        }
    }
}"

                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            };

            if (editorConfigText.Length == 0)
            {
                test.ExpectedDiagnostics.Add(GetCSharpResultAt(12, 20, DefinitelyRule, "T JavaScriptSerializer.Deserialize<T>(string input)"));
            }

            await test.RunAsync();
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources = { source },
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
