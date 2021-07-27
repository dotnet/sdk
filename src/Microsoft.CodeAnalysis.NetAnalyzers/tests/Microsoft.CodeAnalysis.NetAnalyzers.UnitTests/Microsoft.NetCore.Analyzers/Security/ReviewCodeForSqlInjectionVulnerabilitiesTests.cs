// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<Microsoft.NetCore.Analyzers.Security.ReviewCodeForSqlInjectionVulnerabilities, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ReviewCodeForSqlInjectionVulnerabilitiesTests : TaintedDataAnalyzerTestBase<ReviewCodeForSqlInjectionVulnerabilities, ReviewCodeForSqlInjectionVulnerabilities>
    {
        protected override DiagnosticDescriptor Rule => ReviewCodeForSqlInjectionVulnerabilities.Rule;

        [Fact]
        public async Task EntityFramework_FromSql_Constant_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using Microsoft.EntityFrameworkCore;

    public class MyContext : DbContext
    {
        public DbSet<string> DataSet { get; set; }
    }

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            new MyContext().DataSet.FromSql("""");
        }
    }
}
            ");
        }

        [Fact]
        public async Task HttpRequest_Form_Item_EntityFramework_FromSql_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using Microsoft.EntityFrameworkCore;

    public class MyContext : DbContext
    {
        public DbSet<string> DataSet { get; set; }
    }

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            new MyContext().DataSet.FromSql(Request[""in""]);
        }
    }
}
            ",
                GetCSharpResultAt(20, 13, 20, 45, "IQueryable<string> RelationalQueryableExtensions.FromSql<string>(IQueryable<string> source, RawSqlString sql, params object[] parameters)", "void WebForm.Page_Load(object sender, EventArgs e)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DocSample1_CSharp_Violation_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;

namespace TestNamespace
{
    public partial class WebForm : System.Web.UI.Page
    {
        public static string ConnectionString { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            string name = Request.Form[""product_name""];
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = ""SELECT ProductId FROM Products WHERE ProductName = '"" + name + ""'"",
                    CommandType = CommandType.Text,
                };

                SqlDataReader reader = sqlCommand.ExecuteReader();
            }
        }
    }
}
            ",
                GetCSharpResultAt(19, 21, 14, 27, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task DocSample1_CSharp_ParameterizedSolution_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;

namespace TestNamespace
{
    public partial class WebForm : System.Web.UI.Page
    {
        public static string ConnectionString { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            string name = Request.Form[""product_name""];
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = ""SELECT ProductId FROM Products WHERE ProductName = @productName"",
                    CommandType = CommandType.Text,
                };

                sqlCommand.Parameters.Add(""@productName"", SqlDbType.NVarChar, 128).Value = name;

                SqlDataReader reader = sqlCommand.ExecuteReader();
            }
        }
    }
}
            ");
        }

        [Fact]
        public async Task DocSample1_CSharp_StoredProcedureSolution_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;

namespace TestNamespace
{
    public partial class WebForm : System.Web.UI.Page
    {
        public static string ConnectionString { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            string name = Request.Form[""product_name""];
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = ""sp_GetProductIdFromName"",
                    CommandType = CommandType.StoredProcedure,
                };

                sqlCommand.Parameters.Add(""@productName"", SqlDbType.NVarChar, 128).Value = name;

                SqlDataReader reader = sqlCommand.ExecuteReader();
            }
        }
    }
}
            ");
        }

        [Fact]
        public async Task DocSample1_VB_Violation_Diagnostic()
        {
            await VerifyVisualBasicWithDependenciesAsync(@"
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq

Namespace VulnerableWebApp
    Partial Public Class WebForm
        Inherits System.Web.UI.Page

        Public Property ConnectionString As String

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Dim name As String = Me.Request.Form(""product_name"")
            Using connection As SqlConnection = New SqlConnection(ConnectionString)
                Dim sqlCommand As SqlCommand = New SqlCommand With {.CommandText = ""SELECT ProductId FROM Products WHERE ProductName = '"" + name + ""'"",
                                                                    .CommandType = CommandType.Text}
                Dim reader As SqlDataReader = sqlCommand.ExecuteReader()
            End Using
        End Sub
    End Class
End Namespace
            ",
                GetBasicResultAt(16, 70, 14, 34, "Property SqlCommand.CommandText As String", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"));
        }

        [Fact]
        public async Task DocSample1_VB_ParameterizedSolution_NoDiagnostic()
        {
            await VerifyVisualBasicWithDependenciesAsync(@"
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq

Namespace VulnerableWebApp
    Partial Public Class WebForm
        Inherits System.Web.UI.Page

        Public Property ConnectionString As String

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Dim name As String = Me.Request.Form(""product_name"")
            Using connection As SqlConnection = New SqlConnection(ConnectionString)
                Dim sqlCommand As SqlCommand = New SqlCommand With {.CommandText = ""SELECT ProductId FROM Products WHERE ProductName = @productName"",
                                                                    .CommandType = CommandType.Text}
                sqlCommand.Parameters.Add(""@productName"", SqlDbType.NVarChar, 128).Value = name
                Dim reader As SqlDataReader = sqlCommand.ExecuteReader()
            End Using
        End Sub
    End Class
End Namespace
            ");
        }

        [Fact]
        public async Task DocSample1_VB_StoredProcedureSolution_NoDiagnostic()
        {
            await VerifyVisualBasicWithDependenciesAsync(@"
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq

Namespace VulnerableWebApp
    Partial Public Class WebForm
        Inherits System.Web.UI.Page

        Public Property ConnectionString As String

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Dim name As String = Me.Request.Form(""product_name"")
            Using connection As SqlConnection = New SqlConnection(ConnectionString)
                Dim sqlCommand As SqlCommand = New SqlCommand With {.CommandText = ""sp_GetProductIdFromName"",
                                                                    .CommandType = CommandType.StoredProcedure}
                sqlCommand.Parameters.Add(""@productName"", SqlDbType.NVarChar, 128).Value = name
                Dim reader As SqlDataReader = sqlCommand.ExecuteReader()
            End Using
        End Sub
    End Class
End Namespace
            ");
        }

        [Fact]
        public async Task HttpRequest_Form_LocalString_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string input = Request.Form[""in""];
            if (Request.Form != null && !String.IsNullOrWhiteSpace(input))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = input,
                    CommandType = CommandType.Text,
                };
            }
        }
     }
}
            ",
                GetCSharpResultAt(20, 21, 15, 28, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_LocalString_VB_Diagnostic()
        {
            await VerifyVisualBasicWithDependenciesAsync(@"
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq
Imports System.Web
Imports System.Web.UI

Namespace VulnerableWebApp
    Partial Public Class WebForm
        Inherits System.Web.UI.Page

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Dim input As String = Me.Request.Form(""In"")
            If Me.Request.Form IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(input) Then
                Dim sqlCommand As SqlCommand = New SqlCommand() With {.CommandText = input,
                                                                      .CommandType = CommandType.Text}
            End If
        End Sub
    End Class
End Namespace
            ",
                GetBasicResultAt(16, 72, 14, 35, "Property SqlCommand.CommandText As String", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"));
        }

        [Fact]
        public async Task HttpRequest_Form_DelegateInvocation_OutParam_LocalString_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        public delegate void StringOutputDelegate(string input, out string output);

        public static StringOutputDelegate StringOutput;

        protected void Page_Load(object sender, EventArgs e)
        {
            StringOutput(Request.Form[""in""], out string input);
            if (Request.Form != null && !String.IsNullOrWhiteSpace(input))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = input,
                    CommandType = CommandType.Text,
                };
            }
        }
     }
}
            ",
                GetCSharpResultAt(24, 21, 19, 26, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_InterfaceInvocation_OutParam_LocalString_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        public interface IBlah { void StringOutput(string input, out string output); }

        public static IBlah Blah;

        protected void Page_Load(object sender, EventArgs e)
        {
            Blah.StringOutput(Request.Form[""in""], out string input);
            if (Request.Form != null && !String.IsNullOrWhiteSpace(input))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = input,
                    CommandType = CommandType.Text,
                };
            }
        }
     }
}
            ",
                GetCSharpResultAt(24, 21, 19, 31, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_LocalStringMoreBlocks_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string input;
            if (Request.Form != null)
            {
                input = Request.Form[""in""];
            }
            else
            {
                input = ""SELECT 1"";
            }

            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(27, 17, 18, 25, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_And_QueryString_LocalStringMoreBlocks_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string input;
            if (Request.Form != null)
            {
                input = Request.Form[""in""];
            }
            else
            {
                input = Request.QueryString[""in""];
            }

            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(27, 17, 18, 25, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"),
                GetCSharpResultAt(27, 17, 22, 25, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.QueryString", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_Direct_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = Request.Form[""in""],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(17, 17, 17, 31, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_Substring_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = Request.Form[""in""].Substring(1),
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(17, 17, 17, 31, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task Sanitized_HttpRequest_Form_Direct_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE id < "" + int.Parse(Request.Form[""in""]).ToString(),
                CommandType = CommandType.Text,
            };
        }
     }
}
            ");
        }

        [Fact]
        public async Task Sanitized_HttpRequest_Form_TryParse_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Int16.TryParse(Request.Form[""in""], out short i))
            {
                SqlCommand sqlCommand = new SqlCommand()
                {
                    CommandText = ""SELECT * FROM users WHERE id < "" + i.ToString(),
                    CommandType = CommandType.Text,
                };
            }
        }
     }
}
            ");
        }

        [Fact]
        public async Task HttpRequest_Form_Item_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = Request[""in""],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(17, 17, 17, 31, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_Item_Enters_SqlParameters_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = @username"",
                CommandType = CommandType.Text,
            };

            sqlCommand.Parameters.Add(""@username"", SqlDbType.NVarChar, 16).Value = Request[""in""];

            sqlCommand.ExecuteReader();
        }
     }
}
            ");
        }

        [Fact]
        public async Task HttpRequest_Form_Item_Sql_Constructor_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand(Request[""in""]);
        }
     }
}
            ",
                GetCSharpResultAt(15, 37, 15, 52, "SqlCommand.SqlCommand(string cmdText)", "void WebForm.Page_Load(object sender, EventArgs e)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_Method_GenericsSink_Diagnostic()
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default
                    .AddAssemblies(ImmutableArray.Create("System.Web", "System.Web.Extensions"))
                    .AddPackages(ImmutableArray.Create(new PackageIdentity("EntityFramework", "6.4.4"))),
                TestCode = @"
namespace VulnerableWebApp
{
    using System;
    using System.Data.Entity;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string input = Request.Form.Get(""in"");
            new DbContext(""connectionString"").Set<Object>().SqlQuery(input, null);
        }
     }
}
",
            };

            csharpTest.ExpectedDiagnostics.AddRange(new[] { GetCSharpResultAt(13, 13, 12, 28, "DbSqlQuery<object> DbSet<object>.SqlQuery(string sql, params object[] parameters)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)") });

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task HttpRequest_Form_Method_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string input = Request.Form.Get(""in"");
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(18, 17, 15, 28, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_LocalNameValueCollectionString_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            System.Collections.Specialized.NameValueCollection nvc = Request.Form;
            string input = nvc[""in""];
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(19, 17, 15, 70, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_List_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            List<string> allTheInputs = new List<string>(new string[] { Request.Form[""in""] });
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = allTheInputs[0],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(20, 17, 17, 73, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact(Skip = "Would be nice to distinguish between tainted and non-tainted elements in the List, but for now we taint the entire List from its construction.  FxCop also has a false positive.")]
        public async Task HttpRequest_Form_List_SafeElement_Diagnostic()
        {
            // Would be nice to distinguish between tainted and non-tainted elements in the List, but for now we taint the entire List from its construction.  FxCop also has a false positive.

            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            List<string> list = new List<string>(new string[] { Request.Form[""in""] });
            list.Add(""SELECT * FROM users WHERE userid = 1"");
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = list[1],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ");
        }

        [Fact]
        public async Task HttpRequest_Form_Array_List_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string[] array = new string[] { Request.Form[""in""] };
            List<string> allTheInputs = new List<string>(array);
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = allTheInputs[0],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(21, 17, 17, 45, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_Array_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string[] allTheInputs = new string[] { Request.Form[""in""] };
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = allTheInputs[0],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(20, 17, 17, 52, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_LocalStructNameValueCollectionString_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        public struct MyStruct
        {
            public NameValueCollection nvc;
            public string s;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            MyStruct myStruct = new MyStruct();
            myStruct.nvc = this.Request.Form;
            myStruct.s = myStruct.nvc[""in""];
            string input = myStruct.s;
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(28, 17, 23, 28, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_LocalStructConstructorNameValueCollectionString_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"

namespace VulnerableWebApp
{
    using System;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        public struct MyStruct
        {
            public MyStruct(NameValueCollection v)
            {
                this.nvc = v;
                this.s = null;
            }

            public NameValueCollection nvc;
            public string s;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            MyStruct myStruct = new MyStruct();
            myStruct.nvc = this.Request.Form;
            myStruct.s = myStruct.nvc[""in""];
            string input = myStruct.s;
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(35, 17, 30, 28, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_Form_LocalStructConstructorNameValueCollectionString_VB_Diagnostic()
        {
            await VerifyVisualBasicWithDependenciesAsync(@"
Imports System
Imports System.Collections.Specialized
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq
Imports System.Web
Imports System.Web.UI


Namespace VulnerableWebApp
    Public Structure MyStruct
        Public Sub MyStruct(v As NameValueCollection)
            Me.nvc = v
            Me.s = Nothing
        End Sub


        Public nvc As NameValueCollection
        Public s As String
    End Structure

    Partial Public Class WebForm
        Inherits System.Web.UI.Page

        Protected Sub Page_Load(sender As Object, e As EventArgs)
            Dim myStruct As MyStruct = New MyStruct()
            myStruct.nvc = Me.Request.Form
            myStruct.s = myStruct.nvc(""in"")
            Dim input As String = myStruct.s
            Dim sqlCommand As SqlCommand = New SqlCommand() With {.CommandText = input,
                                                                  .CommandType = CommandType.Text}
            End Sub
    End Class
End Namespace
",
                GetBasicResultAt(31, 68, 28, 28, "Property SqlCommand.CommandText As String", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)", "Property HttpRequest.Form As NameValueCollection", "Sub WebForm.Page_Load(sender As Object, e As EventArgs)"));
        }

        [Fact]
        public async Task HttpRequest_UserLanguages_Direct_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = Request.UserLanguages[0],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(17, 17, 17, 31, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "string[] HttpRequest.UserLanguages", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_UserLanguages_LocalStringArray_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string[] languages = Request.UserLanguages;
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = languages[0],
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(18, 17, 15, 34, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "string[] HttpRequest.UserLanguages", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task HttpRequest_UserLanguages_LocalStringModified_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string language = ""SELECT * FROM languages WHERE language = '"" + Request.UserLanguages[0] + ""'"";
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = language,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ",
                GetCSharpResultAt(18, 17, 15, 78, "string SqlCommand.CommandText", "void WebForm.Page_Load(object sender, EventArgs e)", "string[] HttpRequest.UserLanguages", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task OkayInputLocalStructNameValueCollectionString_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        public struct MyStruct
        {
            public NameValueCollection nvc;
            public string s;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            MyStruct myStruct = new MyStruct();
            myStruct.nvc = this.Request.Form;
            myStruct.s = myStruct.nvc[""in""];
            string input = myStruct.s;
            myStruct.s = ""SELECT 1"";
            input = myStruct.s;
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = input,
                CommandType = CommandType.Text,
            };
        }
     }
}
            ");
        }

        [Fact]
        public async Task OkayInputConst_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = 'aaa'"",
                CommandType = CommandType.Text,
            };
        }
     }
}
            ");
        }

        [Fact]
        public async Task DataBoundLiteralControl_DirectImplementation_Text()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Web.UI;

    public class SomeClass
    {
        public DataBoundLiteralControl Control { get; set; }

        public void Execute()
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = '"" + this.Control.Text + ""'"",
                CommandType = CommandType.Text,
            };
        }
    }
}
            ",
                GetCSharpResultAt(17, 17, 17, 74, "string SqlCommand.CommandText", "void SomeClass.Execute()", "string DataBoundLiteralControl.Text", "void SomeClass.Execute()"));
        }

        [Fact]
        public async Task DataBoundLiteralControl_Interface_Text()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Web.UI;

    public class SomeClass
    {
        public DataBoundLiteralControl Control { get; set; }

        public void Execute()
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = '"" + ((ITextControl) this.Control).Text + ""'"",
                CommandType = CommandType.Text,
            };
        }
    }
}
            ",
                GetCSharpResultAt(17, 17, 17, 74, "string SqlCommand.CommandText", "void SomeClass.Execute()", "string ITextControl.Text", "void SomeClass.Execute()"));
        }

        [Fact]
        public async Task HtmlInputButton_Value()
        {
            // HtmlInputButton derives from HtmlInputControl, and HtmlInputControl.Value is a tainted data source.
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Web.UI.HtmlControls;

    public class SomeClass
    {
        public HtmlInputButton Button { get; set; }

        public void Execute()
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = '"" + this.Button.Value + ""'"",
                CommandType = CommandType.Text,
            };
        }
    }
}
            ",
                GetCSharpResultAt(17, 17, 17, 74, "string SqlCommand.CommandText", "void SomeClass.Execute()", "string HtmlInputControl.Value", "void SomeClass.Execute()"));
        }

        [Fact]
        public async Task SimpleInterprocedural()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];
            MyDatabaseLayer layer = new MyDatabaseLayer();
            layer.MakeSqlInjection(taintedInput);
        }
    }

    public class MyDatabaseLayer
    {
        public void MakeSqlInjection(string sqlInjection)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"",
                CommandType = CommandType.Text,
            };
        }
    }
}",
                GetCSharpResultAt(27, 17, 15, 35, "string SqlCommand.CommandText", "void MyDatabaseLayer.MakeSqlInjection(string sqlInjection)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task SimpleInterproceduralTwice()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];
            MyDatabaseLayer layer = new MyDatabaseLayer();
            layer.MakeSqlInjection(taintedInput);
            layer.MakeSqlInjection(taintedInput);
        }
    }

    public class MyDatabaseLayer
    {
        public void MakeSqlInjection(string sqlInjection)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"",
                CommandType = CommandType.Text,
            };
        }
    }
}",
                GetCSharpResultAt(28, 17, 15, 35, "string SqlCommand.CommandText", "void MyDatabaseLayer.MakeSqlInjection(string sqlInjection)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task SimpleLocalFunction()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            SqlCommand injectSql(string sqlInjection)
            {
                return new SqlCommand()
                {
                    CommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"",
                    CommandType = CommandType.Text,
                };
            };

            injectSql(taintedInput);
        }
    }
}",
                GetCSharpResultAt(21, 21, 15, 35, "string SqlCommand.CommandText", "SqlCommand injectSql(string sqlInjection)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task SimpleLambda()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            Func<string, SqlCommand> injectSql = (sqlInjection) =>
            {
                return new SqlCommand()
                {
                    CommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"",
                    CommandType = CommandType.Text,
                };
            };

            injectSql(taintedInput);
        }
    }
}",
                GetCSharpResultAt(21, 21, 15, 35, "string SqlCommand.CommandText", "lambda expression", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task IntermediateMethodReturnsTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            string sqlCommandText = StillTainted(taintedInput);

            ExecuteSql(sqlCommandText);
        }

        protected string StillTainted(string sqlInjection)
        {
            return ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(31, 17, 15, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task IntermediateMethodReturnsTaintedButOutputUntainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            string sqlCommandText = StillTainted(taintedInput, out string notTaintedSqlCommandText);

            ExecuteSql(notTaintedSqlCommandText);
        }

        protected string StillTainted(string sqlInjection, out string notSqlInjection)
        {
            notSqlInjection = ""SELECT * FROM users WHERE userid = "" + Int32.Parse(sqlInjection);
            return ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task IntermediateMethodReturnsTaintedButRefUntainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            string notTaintedSqlCommandText = taintedInput;
            string sqlCommandText = StillTainted(taintedInput, ref notTaintedSqlCommandText);

            ExecuteSql(notTaintedSqlCommandText);
        }

        protected string StillTainted(string sqlInjection, ref string notSqlInjection)
        {
            notSqlInjection = ""SELECT * FROM users WHERE userid = "" + Int32.Parse(sqlInjection);
            return ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task IntermediateMethodReturnsUntaintedButOutputTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            string sqlCommandText = StillTainted(taintedInput, out string taintedSqlCommandText);

            ExecuteSql(taintedSqlCommandText);
        }

        protected string StillTainted(string input, out string sqlInjection)
        {
            sqlInjection = ""SELECT * FROM users WHERE username = '"" + input + ""'"";
            return ""SELECT * FROM users WHERE userid = "" + Int32.Parse(input);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
                GetCSharpResultAt(32, 17, 15, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task IntermediateMethodReturnsUntaintedButRefTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            string taintedSqlCommandText = null;
            string sqlCommandText = StillTainted(taintedInput, ref taintedSqlCommandText);

            ExecuteSql(taintedSqlCommandText);
        }

        protected string StillTainted(string input, ref string taintedSqlCommandText)
        {
            taintedSqlCommandText = ""SELECT * FROM users WHERE username = '"" + input + ""'"";
            return ""SELECT * FROM users WHERE userid = "" + Int32.Parse(input);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
                GetCSharpResultAt(33, 17, 15, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task IntermediateMethodReturnsNotTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            string sqlCommandText = NotTainted(taintedInput);

            ExecuteSql(sqlCommandText);
        }

        protected string NotTainted(string sqlInjection)
        {
            return ""SELECT * FROM users WHERE username = 'bob'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task IntermediateMethodSanitizesTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""userid""];

            string sqlCommandText = SanitizeTainted(taintedInput);

            ExecuteSql(sqlCommandText);
        }

        protected string SanitizeTainted(string sqlInjection)
        {
            return ""SELECT * FROM users WHERE userid = '"" + Int32.Parse(sqlInjection) + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task IntermediateMethodOutParameterTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            StillTainted(taintedInput, out string sqlCommandText);

            ExecuteSql(sqlCommandText);
        }

        protected void StillTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(31, 17, 15, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task IntermediateMethodOutParameterNotTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            NotTainted(taintedInput, out string sqlCommandText);

            ExecuteSql(sqlCommandText);
        }

        protected void NotTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE username = 'bob'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task IntermediateMethodOutParameterSanitizesTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""userid""];

            SanitizeTainted(taintedInput, out string sqlCommandText);

            ExecuteSql(sqlCommandText);
        }

        protected void SanitizeTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE userid = '"" + Int32.Parse(sqlInjection) + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinaryReturnsDefaultStillTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            StillTainted(taintedInput, out string sqlCommandText);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            sqlCommandText = OtherDllStaticMethods.ReturnsDefault(sqlCommandText);

            ExecuteSql(sqlCommandText);
        }

        protected void StillTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(35, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinaryReturnsInputStillTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            StillTainted(taintedInput, out string sqlCommandText);

            sqlCommandText = OtherDllStaticMethods.ReturnsInput(sqlCommandText);

            ExecuteSql(sqlCommandText);
        }

        protected void StillTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(34, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinarySetsOutputToDefaultStillTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            StillTainted(taintedInput, out string sqlCommandText);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            OtherDllStaticMethods.SetsOutputToDefault(sqlCommandText, out string sqlToExecute);

            ExecuteSql(sqlToExecute);
        }

        protected void StillTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(35, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinarySetsReferenceToDefaultStillTainted()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            StillTainted(taintedInput, out string sqlCommandText);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlToExecute = null;
            OtherDllStaticMethods.SetsReferenceToDefault(sqlCommandText, ref sqlToExecute);

            ExecuteSql(sqlToExecute);
        }

        protected void StillTainted(string sqlInjection, out string sqlCommandText)
        {
            sqlCommandText = ""SELECT * FROM users WHERE username = '"" + sqlInjection + ""'"";
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(36, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Property_ConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ConstructedInput + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(29, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Property_Default()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.Default + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(30, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Method_ReturnsConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsConstructedInput() + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(29, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Method_SetsOutputToConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            otherDllObj.SetsOutputToConstructedInput(out string outputParameter);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + outputParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(31, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Method_SetsReferenceToConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            string referenceParameter = ""not tainted"";
            otherDllObj.SetsReferenceToConstructedInput(ref referenceParameter);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + referenceParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(32, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Method_ReturnsDefault()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsDefault() + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(30, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Method_SetsReferenceToDefault()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string referenceParameter = ""not tainted"";
            otherDllObj.SetsReferenceToDefault(ref referenceParameter);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + referenceParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(33, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_TaintedObject_Method_ReturnsDefault_UntaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(taintedInput);

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsDefault(""not tainted"") + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(30, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Property_ConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ConstructedInput + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Property_Default()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.Default + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_ReturnsConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsConstructedInput() + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_SetsOutputToConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            otherDllObj.SetsOutputToConstructedInput(out string outputParameter);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + outputParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_SetsReferenceToConstructedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            string referenceParameter = ""also not tainted"";
            otherDllObj.SetsReferenceToConstructedInput(ref referenceParameter);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + referenceParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_ReturnsDefault()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsDefault() + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_SetsReferenceToDefault()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainteed"");

            string referenceParameter = ""also not tainted"";
            otherDllObj.SetsReferenceToDefault(ref referenceParameter);

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + referenceParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_ReturnsDefault_UntaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsDefault(""also not tainted"") + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}");
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_ReturnsDefault_TaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsDefault(taintedInput) + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(30, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_ReturnsInput_TaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsInput(taintedInput) + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(29, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_ReturnsRandom_TaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + otherDllObj.ReturnsRandom(taintedInput) + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(30, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_SetsOutputToDefault_TaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            otherDllObj.SetsOutputToDefault(taintedInput, out string outputParameter);
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + outputParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(31, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_SetsOutputToInput_TaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            otherDllObj.SetsOutputToInput(taintedInput, out string outputParameter);
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + outputParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(30, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task CrossBinary_UntaintedObject_Method_SetsOutputToRandom_TaintedInput()
        {
            await VerifyCSharpWithDependenciesAsync(@"
namespace VulnerableWebApp
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using OtherDll;

    public partial class WebForm : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string taintedInput = this.Request[""input""];

            OtherDllClass<string> otherDllObj = new OtherDllClass<string>(""not tainted"");

            // Still tainted, cuz not doing cross-binary interprocedural DFA.
            otherDllObj.SetsOutputToRandom(taintedInput, out string outputParameter);
            string sqlCommandText = ""SELECT * FROM users WHERE username = '"" + outputParameter + ""'"";

            ExecuteSql(sqlCommandText);
        }

        protected void ExecuteSql(string sqlCommandText)
        {
            SqlCommand sqlCommand = new SqlCommand()
            {
                CommandText = sqlCommandText,
                CommandType = CommandType.Text,
            };
        }
    }
}",
            GetCSharpResultAt(31, 17, 16, 35, "string SqlCommand.CommandText", "void WebForm.ExecuteSql(string sqlCommandText)", "string HttpRequest.this[string key]", "void WebForm.Page_Load(object sender, EventArgs e)"));
        }

        [Fact]
        public async Task NonMonotonicMergeAssert()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

public class SettingData1
{
    public string Name { get; set; }
    public string CartType { get; set; }
}

public class SettingData2
{
    public string CartType { get; set; }
    public string Index { get; set; }
}

public class Settings
{
    public string DefaultIndex { get; set; }
    public List<SettingData1> Datas1 { get; set; }
    public List<SettingData2> Datas2 { get; set; }
}

public class Class1
{
    public Settings MySettings { get; set; }
    public string SiteCartType { get; set; }
    private SettingData1 GetDefaultData1(string contentType, string taintedInput)
    {
        var settings = MySettings;
        var defaultData = settings.Datas2.FirstOrDefault(x => x.CartType == taintedInput);
        var defaultIndex = defaultData != null ? defaultData.Index : ""0"";

        if (String.IsNullOrWhiteSpace(defaultIndex))
            defaultIndex = ""0"";

        if (!settings.Datas2.Any(x => String.Equals(x.CartType, taintedInput, StringComparison.OrdinalIgnoreCase)))
        {
            var patternIndex = String.IsNullOrWhiteSpace(settings.DefaultIndex) ? ""0"" : settings.DefaultIndex;
            if (String.Equals(taintedInput, SiteCartType, StringComparison.OrdinalIgnoreCase))
            {
                settings.Datas2.Add(new SettingData2 { Index = patternIndex, CartType = taintedInput });
                return settings.Datas1.Where(x => x.CartType == null).ElementAt(Convert.ToInt32(defaultIndex));
            }
            else
            {
                settings.Datas2.Add(new SettingData2 { Index = ""0"", CartType = taintedInput });
                return new SettingData1 { Name = ""Name"", CartType = taintedInput };
            }
        }

        var cartTypeSearch = settings.Datas1.Any(x => String.Equals(x.CartType, taintedInput, StringComparison.OrdinalIgnoreCase)) ? taintedInput : null;

        if (settings.Datas1.Any())
        {
            if (settings.Datas1.Where(x => x.CartType == cartTypeSearch).ElementAt(Convert.ToInt32(defaultIndex)) != null)
            {
                return settings.Datas1.Where(x => x.CartType == cartTypeSearch).ElementAt(Convert.ToInt32(defaultIndex));
            };
        }

        return new SettingData1 { Name = ""Name"", CartType = taintedInput };
    }

    public void ProcessRequest()
    {
        string tainted = HttpContext.Current.Request.Form[""taintedinput""];
        GetDefaultData1(HttpContext.Current.Request.ContentType, tainted);
    }
}
");
        }

        [Fact]
        public async Task PointsToAnalysisAssertsLocationSetsComparison()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.IO;
using System.Threading;
using System.Web;

public interface IContext
{
    HttpContext HttpContext { get; }
}

public class CaptureStream : Stream
{
    public CaptureStream(Stream innerStream)
    {
        _innerStream = innerStream;
        _captureStream = new MemoryStream();
    }

    private readonly Stream _innerStream;
    private readonly MemoryStream _captureStream;

    public override bool CanRead
    {
        get { return _innerStream.CanRead; }
    }

    public override bool CanSeek
    {
        get { return _innerStream.CanSeek; }
    }

    public override bool CanWrite
    {
        get { return _innerStream.CanWrite; }
    }

    public override long Length
    {
        get { return _innerStream.Length; }
    }

    public override long Position
    {
        get { return _innerStream.Position; }
        set { _innerStream.Position = value; }
    }

    public override long Seek(long offset, SeekOrigin direction)
    {
        return _innerStream.Seek(offset, direction);
    }

    public override void SetLength(long length)
    {
        _innerStream.SetLength(length);
    }

    public override void Close()
    {
        _innerStream.Close();
    }

    public override void Flush()
    {
        if (_captureStream.Length > 0)
        {
            OnCaptured();
            _captureStream.SetLength(0);
        }

        _innerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _innerStream.Read(buffer, offset, count);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _captureStream.Write(buffer, offset, count);
        _innerStream.Write(buffer, offset, count);
    }

    public event Action<byte[]> Captured;

    protected virtual void OnCaptured()
    {
        Captured(_captureStream.ToArray());
    }
}

public class Class1
{
    private string AField;
    private bool ASwitch;

    public void Method(IContext aContext)
    {
        var captureHandlerIsAttached = false;

        try
        {
            if (!ASwitch)
                return;

            Console.WriteLine(AField);

            if (!HasUrl(aContext))
            {
                return;
            }

            var response = aContext.HttpContext.Response;
            var captureStream = new CaptureStream(null);
            response.Filter = captureStream;

            captureStream.Captured += (output) => {
                try
                {
                    if (response.StatusCode != 200)
                    {
                        Console.WriteLine(AField);
                        return;
                    }

                    Console.WriteLine(aContext.HttpContext.Request.Url.AbsolutePath);
                }
                finally
                {
                    ReleaseTheLock();
                }
            };

            captureHandlerIsAttached = true;
        }
        finally
        {
            if (!captureHandlerIsAttached)
                ReleaseTheLock();
        }
    }

    private void ReleaseTheLock()
    {
        if (AField != null && Monitor.IsEntered(AField))
        {
            Monitor.Exit(AField);
            AField = null;
        }
    }

    protected virtual bool HasUrl(IContext filterContext)
    {
        if (filterContext.HttpContext.Request.Url == null)
        {
            return false;
        }
        return true;
    }
}
"
                    },
                    ExpectedDiagnostics =
                    {
                    },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task LotsOfAnalysisEntities_1()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace TestNamespace
{
    public class DataStructure
    {
        public string StringProperty { get; set; }
    }

    public class ExampleClass
    {
        public SqlCommand Something(HttpRequest request)
        {
            string name = request.Form[""product_name""];

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            if ((new Random()).Next(6) == 4)
            {
                return null;
            }

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            for (int i = 0; i < 3; i++)
            {
                new DataStructure()
                {
                    StringProperty = ""This is tainted: "" + name,
                };
            }

            return null;
        }
    }
}");
        }

        [Fact]
        public async Task LotsOfAnalysisEntities_2()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace TestNamespace
{
    public class DataStructure
    {
        public string StringProperty { get; set; }
    }

    public class ExampleClass
    {
        public SqlCommand Something(HttpRequest request)
        {
            string name = request.Form[""product_name""];

            var d1 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d2 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d3 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d4 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d5 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d6 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            if ((new Random()).Next(6) == 4)
            {
                return null;
            }

            var d7 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d8 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d9 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d10 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            var d11 = new DataStructure()
            {
                StringProperty = ""This is tainted: "" + name,
            };

            for (int i = 0; i < 3; i++)
            {
                var d12 = new DataStructure()
                {
                    StringProperty = ""This is tainted: "" + name,
                };
            }

            return null;
        }
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments_MvcFromServices_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public interface ISomething
{
    string ToString();
}

public class MyController : Controller
{
    public void DoSomething([FromServices]ISomething input)
    {
        new SqlCommand(input.ToString());
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments_MvcNoFromServices_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public interface ISomething
{
    string ToString();
}

public class MyController : Controller
{
    public void DoSomething(ISomething input)
    {
        new SqlCommand(input.ToString());
    }
}",
                GetCSharpResultAt(14, 9, 12, 29, "SqlCommand.SqlCommand(string cmdText)", "void MyController.DoSomething(ISomething input)", "ISomething input", "void MyController.DoSomething(ISomething input)"));
        }

        [Fact]
        public async Task TaintFunctionArguments_MultipleActions_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class MyController : Controller
{
    public void DoSomething1(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething2(string input)
    {
        new SqlCommand(input.ToString());
    }
}",
                GetCSharpResultAt(9, 9, 7, 30, "SqlCommand.SqlCommand(string cmdText)", "void MyController.DoSomething1(string input)", "string input", "void MyController.DoSomething1(string input)"),
                GetCSharpResultAt(14, 9, 12, 30, "SqlCommand.SqlCommand(string cmdText)", "void MyController.DoSomething2(string input)", "string input", "void MyController.DoSomething2(string input)"));
        }

        [Fact]
        public async Task TaintFunctionArguments_MultipleActions_NotController_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

[NonController]
public class MyController : Controller
{
    // more actions to hit the cache even with the lock-free implementation
    public void DoSomething1(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething2(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething3(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething4(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething5(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething6(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething7(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething8(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething9(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething10(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething11(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething12(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething13(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething14(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething15(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething16(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething17(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething18(string input)
    {
        new SqlCommand(input.ToString());
    }

    public void DoSomething19(string input)
    {
        new SqlCommand(input.ToString());
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments_InheritedNonController_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

[NonController]
public class TotallyNonController : Controller
{
}

public class MyController : TotallyNonController
{
    public void DoSomethingNonAction(string input)
    {
        new SqlCommand(input);
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments_InheritedNonAction_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class MyControllerBase : Controller
{
    [NonAction]
    public virtual void DoSomethingNonAction(string input)
    {
        new SqlCommand(input);
    }
}

public class MyController : MyControllerBase
{
    public override void DoSomethingNonAction(string input)
    {
        new SqlCommand(input);
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments_ControllerAttribute()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

[Controller]
public class My
{
    public void DoSomething(string input)
    {
        new SqlCommand(input);
    }
}",
                GetCSharpResultAt(10, 9, 8, 29, "SqlCommand.SqlCommand(string cmdText)", "void My.DoSomething(string input)", "string input", "void My.DoSomething(string input)"));
        }

        [Fact]
        public async Task TaintFunctionArguments_ControllerSuffix_Inherited()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class Controller
{
}

public class My : Controller
{
    public void DoSomething(string input)
    {
        new SqlCommand(input);
    }
}",
                GetCSharpResultAt(13, 9, 11, 29, "SqlCommand.SqlCommand(string cmdText)", "void My.DoSomething(string input)", "string input", "void My.DoSomething(string input)"));
        }

        [Fact]
        public async Task TaintFunctionArguments_ControllerSuffix()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class MyController
{
    public void DoSomething(string input)
    {
        new SqlCommand(input);
    }
}",
                GetCSharpResultAt(9, 9, 7, 29, "SqlCommand.SqlCommand(string cmdText)", "void MyController.DoSomething(string input)", "string input", "void MyController.DoSomething(string input)"));
        }

        [Fact]
        public async Task TaintFunctionArguments_Reassignment_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class MyController
{
    public void DoSomething(string input)
    {
        input = """";
        new SqlCommand(input);
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments_NoMvcReferenced_NoDiagnostic()
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources = { @"
using System.Data.SqlClient;

public class MyController
{
    public void DoSomething(string input)
    {
        new SqlCommand(input);
    }
}"
                    }
                },
            };

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task AspNetMvcController_HasPropertySetter()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;

public class MyController
{
    public void DoSomething(string input)
    {
    }

    public string AString 
    {
        set { _ = value; }
    }
}");
        }

        [Fact]
        public async Task TaintFunctionArguments()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class MyController : Controller
{
    public void DoSomething(string input)
    {
        new SqlCommand(input);
    }

    [NonAction]
    public void DoSomethingNonAction(string input)
    {
        new SqlCommand(input);
    }

    public static void DoSomethingStatic(string input)
    {
        new SqlCommand(input);
    }

    private void DoSomethingPrivate(string input)
    {
        new SqlCommand(input);
    }

    protected void DoSomethingProtected(string input)
    {
        new SqlCommand(input);
    }

    public MyController(string x)
    {
        new SqlCommand(x);
    }
}",
                GetCSharpResultAt(9, 9, 7, 29, "SqlCommand.SqlCommand(string cmdText)", "void MyController.DoSomething(string input)", "string input", "void MyController.DoSomething(string input)"));
        }

        [Theory]
        [InlineData("st.Append(stringParam);", true, "string stringParam")]
        [InlineData("st.Append(intParam);", false)]
        [InlineData("st.Append(charArrayParam);", true, "char[] charArrayParam", 70)]
        [InlineData("st.Append(charPointerParam, 10);", true, "char* charPointerParam", 93)]
        [InlineData("st.Append(charParam);", true, "char charParam", 117)]

        [InlineData("st.AppendFormat(stringParam, \"1\");", true, "string stringParam")]
        [InlineData("st.AppendFormat(\"{0}\", stringParam);", true, "string stringParam")]
        [InlineData("st.AppendFormat(\"{0}{1}\", \"\", stringParam);", true, "string stringParam")]
        [InlineData("st.AppendFormat(\"{0}{1}{2}\", \"\", \"\", stringParam);", true, "string stringParam")]
        [InlineData("st.AppendFormat(\"{0}{1}{2}{3}\", \"\", \"\", \"\", stringParam);", true, "string stringParam")]
        [InlineData("st.AppendFormat(\"{0}{1}{2}{3}{4}\", \"\", \"\", \"\", \"\", stringParam);", true, "string stringParam")]

        [InlineData("st.AppendLine(stringParam);", true, "string stringParam")]

        [InlineData("st.Insert(0, stringParam);", true, "string stringParam")]
        [InlineData("st.Insert(0, intParam);", false)]
        [InlineData("st.Insert(0, charArrayParam);", true, "char[] charArrayParam", 70)]
        [InlineData("st.Insert(0, charParam);", true, "char charParam", 117)]

        [InlineData("st.Replace(\"\", stringParam);", true, "string stringParam")]

        [InlineData("st[0] = charParam;", true, "char charParam", 117)]

        [InlineData("st.Append(stringParam); st.CopyTo(0, arr, 0, 10)", true, "string stringParam", 36, "new string(arr)")]
        [InlineData("st.Append(stringParam); st.Clear()", false)]

        [InlineData("StaticString = stringParam", false)]
        public async Task TaintThis_StringBuilder(string payload, bool warn, string source = "", int sourceColumn = 36, string sinkArg = "st.ToString()")
        {
            string code = $@"
using System.Data.SqlClient;
using System.Text;
using Microsoft.AspNetCore.Mvc;

public class MyController
{{
    public unsafe void DoSomething(string stringParam, int intParam, char[] charArrayParam, char* charPointerParam, char charParam)
    {{
        var st = new StringBuilder();
        var arr = new char[100];
        {payload};
        new SqlCommand({sinkArg});
    }}

    public static string StaticString {{ get; set; }}
}}";
            if (warn)
            {
                await VerifyCSharpWithDependenciesAsync(
                     code,
                     GetCSharpResultAt(
                         13, 9, 8, sourceColumn,
                         "SqlCommand.SqlCommand(string cmdText)",
                         "void MyController.DoSomething(string stringParam, int intParam, char[] charArrayParam, char* charPointerParam, char charParam)",
                         source,
                         "void MyController.DoSomething(string stringParam, int intParam, char[] charArrayParam, char* charPointerParam, char charParam)"));
            }
            else
            {
                await VerifyCSharpWithDependenciesAsync(code);
            }
        }

        [Fact]
        public async Task HttpServerUtility_HtmlEncode_StringWriterOverload_WrongSanitizer()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultForTaintedDataAnalysis,
                TestState =
                {
                    Sources =
                    {
                        SharedCode.WrongSanitizer,
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(16, 33, 11, 24, "SqlCommand.SqlCommand(string cmdText)", "void WebForm.Page_Load(object sender, EventArgs e)", "NameValueCollection HttpRequest.Form", "void WebForm.Page_Load(object sender, EventArgs e)")
                    },
                },
            }.RunAsync();
        }

        [Fact, WorkItem(4491, "https://github.com/dotnet/roslyn-analyzers/issues/4491")]
        public async Task AssemblyAttributeRegressionTest()
        {
            await VerifyVisualBasicWithDependenciesAsync(@"<Assembly: System.Reflection.AssemblyTitle(""Title"")>");
        }

        [Fact]
        public async Task AspNetCoreHttpRequest_Form_Direct_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.Data;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        string input = Request.Form[""in""];
        var sqlCommand = new SqlCommand()
        {
            CommandText = input,
            CommandType = CommandType.Text,
        };

        return View();
    }
}",
                GetCSharpResultAt(13, 13, 10, 24, "string SqlCommand.CommandText", "IActionResult HomeController.Index()", "IFormCollection HttpRequest.Form", "IActionResult HomeController.Index()"));
        }
    }
}
