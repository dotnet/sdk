// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.UseAutoValidateAntiforgeryToken,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class UseAutoValidateAntiforgeryTokenTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(rule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        protected async Task VerifyCSharpWithDependenciesAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAspNetCoreMvc,
                TestState =
                {
                    Sources = { source, "public class MyValidateAntiForgeryAttribute : System.Attribute {}" }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAsyncAuthorizationFilter_NotCallMethodsOf_DescedantOfIAntiForgery_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        return null;
    }
}

class TestClass : Controller
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}",
            GetCSharpResultAt(26, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpDelete"));
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAuthorizationFilter_NotCallMethodsOf_DescedantOfIAntiForgery_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class FilterClass : IAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public void OnAuthorization (AuthorizationFilterContext context)
    {
    }
}

class TestClass : Controller
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}",
            GetCSharpResultAt(25, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpDelete"));
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_DerivedIAuthorizationFilter_NotCallMethodsOf_DescedantOfIAntiForgery_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

abstract class AbstractClass : IAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public void OnAuthorization (AuthorizationFilterContext context)
    {
        defaultAntiforgery.ValidateRequestAsync(null).Wait();
    }
}

class FilterClass : AbstractClass 
{
    public void OnAuthorization (AuthorizationFilterContext context)
    {
    }
}

class TestClass : Controller
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}",
            GetCSharpResultAt(33, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpDelete"));
        }

        [Fact]
        public async Task Test_ChildrenOfController_ActionMethodWithHttpPostAndHttpGetAttributes_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpGet]
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(13, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpPost"));
        }

        [Fact]
        public async Task Test_ChildrenOfController_ActionMethodWithHttpPatchAttribute_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpPatch]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(13, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpPatch"));
        }

        [Fact]
        public async Task Test_ChildrenOfController_ActionMethodWithHttpPostAttribute_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(12, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpPost"));
        }

        [Fact]
        public async Task Test_ChildrenOfController_ActionMethodWithHttpPutAttribute_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpPut]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(12, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpPut"));
        }

        [Fact]
        public async Task Test_ChildrenOfController_ActionMethodWithHttpDeleteAttribute_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(12, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpDelete"));
        }

        [Fact]
        public async Task Test_WithoutValidateAntiForgeryAttribute_ActionMethodWithTwoHttpVervAttributes_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpDelete]
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(17, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpDelete"));
        }

        [Fact]
        public async Task Test_NoValidateAntiForgeryTokenAttribute_ActionMethodMissingHttpVerbAttribute_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(15, 35, UseAutoValidateAntiforgeryToken.MissHttpVerbAttributeRule, "CustomizedActionMethod"));
        }

        [Fact, WorkItem(2844, "https://github.com/dotnet/roslyn-analyzers/issues/2844")]
        public async Task Test_ConcurrencyIssue_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

class TestClass : Controller
{
    [HttpPut]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class TestClass2 : Controller
{
    [HttpPut]
    public AcceptedAtActionResult CustomizedActionMethod2 (string actionName)
    {
        return null;
    }
}

class TestClass3 : Controller
{
    [HttpPut]
    public AcceptedAtActionResult CustomizedActionMethod3 (string actionName)
    {
        return null;
    }
}

class TestClass4 : Controller
{
    [HttpPut]
    public AcceptedAtActionResult CustomizedActionMethod4 (string actionName)
    {
        return null;
    }
}

class TestClass5 : Controller
{
    [HttpPut]
    public AcceptedAtActionResult CustomizedActionMethod5 (string actionName)
    {
        return null;
    }
}",
            GetCSharpResultAt(12, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpPut"),
            GetCSharpResultAt(21, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod2", "HttpPut"),
            GetCSharpResultAt(30, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod3", "HttpPut"),
            GetCSharpResultAt(39, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod4", "HttpPut"),
            GetCSharpResultAt(48, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod5", "HttpPut"));
        }

        [Fact]
        public async Task EditorConfigConfiguration_OnlyLookAtDerivedClassesOfController_DefaultValue_Diagnostic()
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAspNetCoreMvc,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using Microsoft.AspNetCore.Mvc;

public class MyValidateAntiForgeryAttribute : Attribute {}

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA5391.exclude_aspnet_core_mvc_controllerbase = false") }
                },
            };

            csharpTest.ExpectedDiagnostics.Add(
                GetCSharpResultAt(15, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpDelete")
            );

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAsyncAuthorizationFilter_DirectlyCallMethodsOf_ChildrenOfIAntiForgery_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_DerivedFilter_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

abstract class AbstractAuthorizationFilter : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class FilterClass : AbstractAuthorizationFilter
{
}

class TestClass : Controller
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_DerivedFilterOverrides_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : Controller
{
}

abstract class AbstractAuthorizationFilter : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public virtual Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class FilterClass : AbstractAuthorizationFilter
{
    public override Task OnAuthorizationAsync (AuthorizationFilterContext content)
    {
        return Task.CompletedTask;
    }
}

class TestClass : Controller
{
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection();
        filterCollection.Add(typeof(FilterClass));
    }
}",
                GetCSharpResultAt(36, 35, UseAutoValidateAntiforgeryToken.UseAutoValidateAntiforgeryTokenRule, "CustomizedActionMethod", "HttpPost"));
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_MethodReferItSelft_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public void BlahMethod(AuthorizationFilterContext context)
    {
        OnAuthorizationAsync(context);
    }

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        if (count > 0)
        {
            count--;
            BlahMethod(context);
        }
        
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }

    private int count;
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_DelegateField_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

public delegate Task TestDelegate();

public class ClassContainsDelegateField
{
    public TestDelegate delegateField;
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        ClassContainsDelegateField classContainsDelegateField = new ClassContainsDelegateField();
        classContainsDelegateField.delegateField = () =>
        {
            HttpContext httpContext = null;
            return defaultAntiforgery.ValidateRequestAsync(httpContext);
        };

        return classContainsDelegateField.delegateField();
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_StaticDelegateField_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

public delegate Task TestDelegate(DefaultAntiforgery defaultAntiforgery);

public class ClassContainsDelegateField
{
    public static TestDelegate staticDelegateField = (DefaultAntiforgery defaultAntiforgery) =>
    {
        HttpContext httpContext = null;
        
        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    };
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        return ClassContainsDelegateField.staticDelegateField(defaultAntiforgery);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Interface_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

interface BlahInterface
{
    void BlahMethod();
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public BlahInterface blahInterface;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        blahInterface.BlahMethod();
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAsyncAuthorizationFilter_DirectlyCallMethodsOf_ChildrenOfIAntiForgeryImplicitly_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public IAntiforgery antiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return antiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAsyncAuthorizationFilter_DirectlyCallMethodsOf_DescedantOfIAntiForgery_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public MyAntiforgery myAntiforgery;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;
        return myAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class MyAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => null;
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => null;
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => null;
    public void SetCookieTokenAndHeader(HttpContext httpContext) {}
    public Task ValidateRequestAsync(HttpContext httpContext) => null;
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod(string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAsyncAuthorizationFilter_IndirectlyCallMethodsOf_ChildrenOfIAntiForgery_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return ThirdMethod(httpContext);
    }

    public Task ThirdMethod(HttpContext httpContext)
    {
        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIAsyncAuthorizationFilter_IndirectlyCallMethodsOf_DescedantOfIAntiForgery_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public MyAntiforgery myAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return ThirdMethod(httpContext);
    }

    public Task ThirdMethod(HttpContext httpContext)
    {
        return myAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class MyAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => null;
    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => null;
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => null;
    public void SetCookieTokenAndHeader(HttpContext httpContext) {}
    public Task ValidateRequestAsync(HttpContext httpContext) => null;
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_DescedantOfIAsyncAuthorizationFilter_DirectlyCallMethodsOf_ChildrenOfIAntiForgery_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

public interface IMyAsyncAuthorizationFilter : IAsyncAuthorizationFilter
{
}

class FilterClass : IMyAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_AddIsAGenericMethod_ChildrenOfIAsyncAuthorizationFilter_DirectlyCallMethodsOf_ChildrenOfIAntiForgery_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_ChildrenOfIFilterMetadata_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IFilterMetadata
{
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_Add_DescedantOfIFilterMetadata_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

public interface IMyFilterMetadata : IFilterMetadata
{
}

class MyValidateAntiForgeryClass : IMyFilterMetadata
{
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(MyValidateAntiForgeryClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_AddIsAGenericMethod_ChildrenOfIFilterMetadata_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class FilterClass : IFilterMetadata
{
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_AddIsAGenericMethod_DescedantOfIFilterMetadata_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

public interface IMyFilterMetadata : IFilterMetadata
{
}

class MyValidateAntiForgeryClass : IMyFilterMetadata
{
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(MyValidateAntiForgeryClass));
    }
}");
        }

        [Fact]
        public async Task Test_ActionMethodIsNotPublic_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class TestClass : ControllerBase
{
    [HttpPost]
    private AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ActionMethodIsStatic_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class TestClass : ControllerBase
{
    [HttpPost]
    public static AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ActionMethodWithNonActionAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass
{
}

class TestClass : ControllerBase
{
    [NonAction]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_OverridenMethodWithNonActionAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class TestClass : Controller
{
    [HttpDelete]
    public override AcceptedAtActionResult AcceptedAtAction (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ActionMethodWitoutAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

class TestClass : ControllerBase
{
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ChildrenOfControllerBase_ActionMethodWithBothValidateAntiForgeryAndHttpPostAttributes_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.AspNetCore.Mvc;

class TestClass : ControllerBase
{
    [MyValidateAntiForgeryAttribute]
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ChildrenOfControllerBase_ActionMethodWithHttpPostAttributeWhileTypeWithValidateAntiForgeryAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class TestClass : ControllerBase
{
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_NotUsingValidateAntiForgeryAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

class TestClass : ControllerBase
{
    [HttpPost]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ActionMethodWithHttpGetAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using Microsoft.AspNetCore.Mvc;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class TestClass : ControllerBase
{
    [HttpGet]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ValidateAntiForgeryTokenAttributeOnActionMethod_ActionMethodMissingHttpVerbAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

class TestClass : ControllerBase
{
    [MyValidateAntiForgeryAttribute]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_ValidateAntiForgeryTokenAttributeOnController_ActionMethodMissingHttpVerbAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class TestClass : ControllerBase
{
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_NoValidateAntiForgeryTokenAttribute_ActionMethodMissingHttpVerbAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

class TestClass : ControllerBase
{
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}");
        }

        [Fact]
        public async Task Test_GlobalAntiForgeryFilter_ActionMethodMissingHttpVerbAttribute_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class FilterClass : IAsyncAuthorizationFilter 
{
    public DefaultAntiforgery defaultAntiforgery;

    public Task OnAuthorizationAsync (AuthorizationFilterContext context)
    {
        HttpContext httpContext = null;

        return defaultAntiforgery.ValidateRequestAsync(httpContext);
    }
}

class TestClass : ControllerBase
{
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}

class BlahClass
{
    public void TestMethod ()
    {
        var filterCollection = new FilterCollection ();
        filterCollection.Add(typeof(FilterClass));
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.CA5391.exclude_aspnet_core_mvc_controllerbase = true")]
        public async Task EditorConfigConfiguration_OnlyLookAtDerivedClassesOfController_NonDefaultValue_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithAspNetCoreMvc,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using Microsoft.AspNetCore.Mvc;

public class MyValidateAntiForgeryAttribute : Attribute {}

[MyValidateAntiForgeryAttribute]
class MakeSureValidateAntiForgeryAttributeIsUsedSomeWhereClass : ControllerBase
{
}

class TestClass : ControllerBase
{
    [HttpDelete]
    public AcceptedAtActionResult CustomizedActionMethod (string actionName)
    {
        return null;
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();
        }
    }
}
