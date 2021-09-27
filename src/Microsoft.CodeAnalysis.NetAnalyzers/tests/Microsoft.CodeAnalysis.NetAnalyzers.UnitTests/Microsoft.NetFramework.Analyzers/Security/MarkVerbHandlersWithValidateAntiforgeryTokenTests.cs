// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class MarkVerbHandlersWithValidateAntiforgeryTokenTests
    {
        #region Boilerplate

        private static DiagnosticResult GetCA3147CSharpNoVerbs(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.NoVerbsRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147CSharpNoVerbsNoToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.NoVerbsNoTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147CSharpGetAndToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.GetAndTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147CSharpGetAndOtherToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.GetAndOtherAndTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147CSharpVerbsAndNoToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.VerbsAndNoTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147BasicNoVerbs(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.NoVerbsRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147BasicNoVerbsNoToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.NoVerbsNoTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147BasicGetAndToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.GetAndTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147BasicGetAndOtherToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.GetAndOtherAndTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3147BasicVerbsAndNoToken(int line, int column, string controllerAction)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer.VerbsAndNoTokenRule).WithLocation(line, column).WithArguments(controllerAction);
#pragma warning restore RS0030 // Do not used banned APIs

        #endregion

        /// <summary>
        /// Types in the System.Web.Mvc namespace that the analyzer looks for, so we don't have to reference ASP.NET MVC bits.
        /// </summary>
        private const string SystemWebMvcNamespaceCSharp = @"
namespace System.Web.Mvc
{
    using System;

    public class Controller { }

    public class ControllerBase { }

    public class ActionResult { }

    public class ContentResult : ActionResult { }

    public class ValidateAntiForgeryTokenAttribute : Attribute { }

    public class HttpGetAttribute : Attribute { }

    public class HttpPostAttribute : Attribute { }

    public class HttpPutAttribute : Attribute { }

    public class HttpDeleteAttribute : Attribute { }

    public class HttpPatchAttribute : Attribute { }

    public class AcceptVerbsAttribute : Attribute 
    {
        public AcceptVerbsAttribute(params string[] verbs)
        {
        }

        public AcceptVerbsAttribute(HttpVerbs verbs)
        {
        }
    }

    public class NonActionAttribute : Attribute { }

    public class ChildActionOnlyAttribute : Attribute { }

    /// <summary>Enumerates the HTTP verbs.</summary>
    [Flags]
    public enum HttpVerbs
    {
	    /// <summary>Retrieves the information or entity that is identified by the URI of the request.</summary>
	    Get = 1,
	    /// <summary>Posts a new entity as an addition to a URI.</summary>
	    Post = 2,
	    /// <summary>Replaces an entity that is identified by a URI.</summary>
	    Put = 4,
	    /// <summary>Requests that a specified URI be deleted.</summary>
	    Delete = 8,
	    /// <summary>Retrieves the message headers for the information or entity that is identified by the URI of the request.</summary>
	    Head = 0x10,
	    /// <summary>Requests that a set of changes described in the request entity be applied to the resource identified by the Request-URI.</summary>
	    Patch = 0x20,
	    /// <summary>Represents a request for information about the communication options available on the request/response chain identified by the Request-URI.</summary>
	    Options = 0x40
    }
}
";

        /// <summary>
        /// If the tested source code starts with SystemWebMvcNamespaceCSharp, we can add offsets when specifying location line numbers,
        /// and not break everything when adding to SystemWebMvcNamespaceCSharp.
        /// </summary>
        private static readonly int SystemWebMvcNamespaceCSharpLineCount = SystemWebMvcNamespaceCSharp.Where(ch => ch == '\n').Count();

        /// <summary>
        /// Types in the System.Web.Mvc namespace that the analyzer looks for, so we don't have to reference ASP.NET MVC bits.
        /// </summary>
        private const string SystemWebMvcNamespaceBasic = @"
Imports System

Namespace System.Web.Mvc
    Public Class Controller
    End Class

    Public Class ControllerBase
    End Class

    Public Class ActionResult
    End Class

    Public Class ContentResult
        Inherits ActionResult
    End Class

    Public Class ValidateAntiForgeryTokenAttribute
        Inherits Attribute
    End Class

    Public Class HttpGetAttribute
        Inherits Attribute
    End Class

    Public Class HttpPostAttribute
        Inherits Attribute
    End Class

    Public Class HttpPutAttribute
        Inherits Attribute
    End Class

    Public Class HttpDeleteAttribute
        Inherits Attribute
    End Class

    Public Class HttpPatchAttribute
        Inherits Attribute
    End Class

    Public Class AcceptVerbsAttribute
        Inherits Attribute

        Public Sub New(ByVal ParamArray verbs() As String)
        End Sub

        Public Sub New(ByVal verbs As HttpVerbs)
        End Sub
    End Class

    Public Class NonActionAttribute
        Inherits Attribute
    End Class

    Public Class ChildActionOnlyAttribute
        Inherits Attribute
    End Class

    <Flags>
    Public Enum HttpVerbs
        [Get] = 1
        Post = 2
        Put = 4
        Delete = 8
        Head = 16
        Patch = 32
        Options = 64
    End Enum
End Namespace
";

        /// <summary>
        /// If the tested source code starts with SystemWebMvcNamespaceBasic, we can add offsets when specifying location line numbers,
        /// and not break everything when adding to SystemWebMvcNamespaceBasic.
        /// </summary>
        private static readonly int SystemWebMvcNamespaceBasicLineCount = SystemWebMvcNamespaceBasic.Where(ch => ch == '\n').Count();

        [Fact]
        public async Task HaveAcceptStringPutAndToken_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [AcceptVerbs(""Put"")]
        [ValidateAntiForgeryToken]
        public ActionResult DoSomething(string input)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task HaveAcceptStringPutAndToken_Basic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.AcceptVerbs(""Put"")>
        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByRef input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ReturnsNonActionResult_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        public string DoSomething(string input)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task HttpGet_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [HttpGet]
        public ActionResult DoSomething(string input)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task ReturnsNonActionResult_Basic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController 
        Inherits System.Web.Mvc.Controller

        Public Function DoSomething(ByVal input As String) As String
            Return Nothing
        End Function
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task HaveAcceptEnumPutAndToken_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateAntiForgeryToken]
        public ActionResult DoSomething(string input)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task HaveAcceptEnumPutAndToken_Basic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.AcceptVerbs(System.Web.Mvc.HttpVerbs.Post)>
        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByRef input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task NotAController_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class NotAController
    {
        public ContentResult DoSomethingElse()
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task NotAController_Basic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class NotAController
        Public Function DoSomething() As System.Web.Mvc.ContentResult
            Return Nothing
        End Function
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task NotAnAction_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class AController : Controller
    {
        [NonAction]
        public ContentResult DoSomethingElse()
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task NotAnAction_Basic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.NonAction>
        Public Function DoSomething() As System.Web.Mvc.ContentResult
            Return Nothing
        End Function
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task HaveHttpPostAndToken_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoSomething(string input)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task HaveHttpPostAndToken_Basic_NoDiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.HttpPost>
        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByVal input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task MissingVerbsAndToken_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        public ActionResult DoSomething(string input)
        {
            return null;
        }
    }
}
",
                GetCA3147CSharpNoVerbsNoToken(SystemWebMvcNamespaceCSharpLineCount + 8, 29, "DoSomething"));
        }

        [Fact]
        public async Task AutoGeneratedCode_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
// <auto-generated/>
" + SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        public ActionResult DoSomething(string input)
        {
            return null;
        }
    }
}
",
                GetCA3147CSharpNoVerbsNoToken(SystemWebMvcNamespaceCSharpLineCount + 10, 29, "DoSomething"));
        }

        [Fact]
        public async Task MissingVerbsAndToken_Basic_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        Public Function DoSomething(ByVal input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace",
            GetCA3147BasicNoVerbsNoToken(SystemWebMvcNamespaceBasicLineCount + 6, 25, "DoSomething"));
        }

        [Fact]
        public async Task HttpGetAndToken_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : ControllerBase
    {
        [HttpGet]
        [ValidateAntiForgeryToken]
        public ActionResult DoSomething()
        {
            return new ContentResult();
        }
    }
}
",
                GetCA3147CSharpGetAndToken(SystemWebMvcNamespaceCSharpLineCount + 10, 29, "DoSomething"));
        }

        [Fact]
        public async Task HttpGetAndToken_Basic_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.HttpGet>
        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByVal input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace",
            GetCA3147BasicGetAndToken(SystemWebMvcNamespaceBasicLineCount + 8, 25, "DoSomething"));
        }

        [Fact]
        public async Task AcceptGetPostAndToken_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        [ValidateAntiForgeryToken]
        public ContentResult DoSomething()
        {
            return new ContentResult();
        }
    }
}",
                GetCA3147CSharpGetAndToken(SystemWebMvcNamespaceCSharpLineCount + 10, 30, "DoSomething"),
                GetCA3147CSharpGetAndOtherToken(SystemWebMvcNamespaceCSharpLineCount + 10, 30, "DoSomething"));
        }

        [Fact]
        public async Task AcceptGetPostAndToken_Basic_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.AcceptVerbs(System.Web.Mvc.HttpVerbs.Post Or System.Web.Mvc.HttpVerbs.Get)>
        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByVal input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace",
                GetCA3147BasicGetAndToken(SystemWebMvcNamespaceBasicLineCount + 8, 25, "DoSomething"),
                GetCA3147BasicGetAndOtherToken(SystemWebMvcNamespaceBasicLineCount + 8, 25, "DoSomething"));
        }

        [Fact]
        public async Task HttpGetHttpPutAndToken_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class ApiController : ControllerBase
    {
        [HttpGet]
        [HttpPut]
        [ValidateAntiForgeryToken]
        public ActionResult DoSomething()
        {
            return new ContentResult();
        }
    }
}
",
                GetCA3147CSharpGetAndToken(SystemWebMvcNamespaceCSharpLineCount + 11, 29, "DoSomething"),
                GetCA3147CSharpGetAndOtherToken(SystemWebMvcNamespaceCSharpLineCount + 11, 29, "DoSomething"));
        }

        [Fact]
        public async Task HttpGetHttpPutAndToken_Basic_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.HttpGet>
        <System.Web.Mvc.HttpPost>
        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByVal input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace",
                GetCA3147BasicGetAndToken(SystemWebMvcNamespaceBasicLineCount + 9, 25, "DoSomething"),
                GetCA3147BasicGetAndOtherToken(SystemWebMvcNamespaceBasicLineCount + 9, 25, "DoSomething"));
        }

        [Fact]
        public async Task TokenWithoutVerbs_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class TokenWithVerbsController : Controller
    {
        [ValidateAntiForgeryToken]
        public ActionResult DoSomething()
        {
            return null;
        }
    }
}
",
            GetCA3147CSharpNoVerbs(SystemWebMvcNamespaceCSharpLineCount + 9, 29, "DoSomething"));
        }

        [Fact]
        public async Task TokenWithoutVerbs_Basic_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        <System.Web.Mvc.ValidateAntiForgeryToken>
        Public Function DoSomething(ByVal input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace",
                GetCA3147BasicNoVerbs(SystemWebMvcNamespaceBasicLineCount + 7, 25, "DoSomething"));
        }

        [Fact]
        public async Task AcceptVerbsNoToken_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Web.Mvc;

    public class AcceptVerbsNoTokenController : Controller
    {
        private const HttpVerbs AllowedVerbs = HttpVerbs.Post | HttpVerbs.Put;

        [AcceptVerbs(AllowedVerbs)]
        public ActionResult DoSomething()
        {
            return null;
        }
    }
}
",
            GetCA3147CSharpVerbsAndNoToken(SystemWebMvcNamespaceCSharpLineCount + 11, 29, "DoSomething"));
        }

        [Fact]
        public async Task AcceptVerbsNoToken_Basic_DiagnosticAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(SystemWebMvcNamespaceBasic + @"
Namespace Blah
    Public Class ApiController
        Inherits System.Web.Mvc.Controller

        Private Const AllowedVerbs As System.Web.Mvc.HttpVerbs = System.Web.Mvc.HttpVerbs.Post Or System.Web.Mvc.HttpVerbs.Put

        <System.Web.Mvc.AcceptVerbs(AllowedVerbs)>
        Public Function DoSomething(ByRef input As String) As System.Web.Mvc.ActionResult
            Return Nothing
        End Function
    End Class
End Namespace",
                GetCA3147BasicVerbsAndNoToken(SystemWebMvcNamespaceBasicLineCount + 9, 25, "DoSomething"));
        }

        [Fact]
        public async Task DocumentationViolationPostExample_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace TestNamespace
{
    using System.Web.Mvc;

    public class TestController : Controller
    {
        public ActionResult TransferMoney(string toAccount, string amount)
        {
            // You don't want an attacker specify to who and how much money to transfer.

            return null;
        }
    }
}",
                GetCA3147CSharpNoVerbsNoToken(SystemWebMvcNamespaceCSharpLineCount + 8, 29, "TransferMoney"));
        }

        [Fact]
        public async Task DocumentationFixPostExample_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace TestNamespace
{
    using System.Web.Mvc;

    public class TestController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TransferMoney(string toAccount, string amount)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task DocumentationViolationGetExample_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace TestNamespace
{
    using System.Web.Mvc;

    public class TestController : Controller
    {
        public ActionResult Help(int topicId)
        {
            // This Help method is an example of a read-only operation with no harmful side effects.
            return null;
        }
    }
}",
                GetCA3147CSharpNoVerbsNoToken(SystemWebMvcNamespaceCSharpLineCount + 8, 29, "Help"));
        }

        [Fact]
        public async Task DocumentationFixGetExample_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace TestNamespace
{
    using System.Web.Mvc;

    public class TestController : Controller
    {
        [HttpGet]
        public ActionResult Help(int topicId)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task MissingVerbsAndTokenAsync_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Threading.Tasks;
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        public async Task<ActionResult> DoSomethingAsync(string input)
        {
            return null;
        }
    }
}
",
                GetCA3147CSharpNoVerbsNoToken(SystemWebMvcNamespaceCSharpLineCount + 9, 41, "DoSomethingAsync"));
        }

        [Fact]
        public async Task AcceptVerbsNoTokenAsync_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Threading.Tasks;
    using System.Web.Mvc;

    public class AcceptVerbsNoTokenController : Controller
    {
        private const HttpVerbs AllowedVerbs = HttpVerbs.Post | HttpVerbs.Put;

        [AcceptVerbs(AllowedVerbs)]
        public async Task<ActionResult> DoSomethingAsync()
        {
            return null;
        }
    }
}
",
            GetCA3147CSharpVerbsAndNoToken(SystemWebMvcNamespaceCSharpLineCount + 12, 41, "DoSomethingAsync"));
        }

        [Fact]
        public async Task HaveAcceptStringPutAndTokenAsync_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Threading.Tasks;
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [AcceptVerbs(""Put"")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DoSomethingAsync(string input)
        {
            return null;
        }
    }
}"
            );
        }

        [Fact]
        public async Task MissingVerbsAndTokenTaskButNotAsync_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Threading.Tasks;
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        public Task<ActionResult> DoSomethingAsync(string input)
        {
            return null;
        }
    }
}
",
                GetCA3147CSharpNoVerbsNoToken(SystemWebMvcNamespaceCSharpLineCount + 9, 35, "DoSomethingAsync"));
        }

        [Fact]
        public async Task AcceptVerbsNoTokenTaskButNotAsync_CSharp_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Threading.Tasks;
    using System.Web.Mvc;

    public class AcceptVerbsNoTokenController : Controller
    {
        private const HttpVerbs AllowedVerbs = HttpVerbs.Post | HttpVerbs.Put;

        [AcceptVerbs(AllowedVerbs)]
        public Task<ActionResult> DoSomethingAsync()
        {
            return null;
        }
    }
}
",
            GetCA3147CSharpVerbsAndNoToken(SystemWebMvcNamespaceCSharpLineCount + 12, 35, "DoSomethingAsync"));
        }

        [Fact]
        public async Task HaveAcceptStringPutAndTokenTaskButNotAsync_CSharp_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(SystemWebMvcNamespaceCSharp + @"
namespace Blah
{
    using System.Threading.Tasks;
    using System.Web.Mvc;

    public class ApiController : Controller
    {
        [AcceptVerbs(""Put"")]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> DoSomethingAsync(string input)
        {
            return null;
        }
    }
}"
            );
        }
    }
}
