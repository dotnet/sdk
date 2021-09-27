// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseDeprecatedSecurityProtocols,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseDeprecatedSecurityProtocols,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotUseDeprecatedSecurityProtocolsTests
    {
        [Fact]
        public async Task DocSample1_CSharp_ViolationAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5364 violation for using Tls11
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
    }
}",
            GetCSharpResultAt(10, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls11"),
            GetCSharpResultAt(10, 77, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample1_VB_ViolationAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Net

Public Class TestClass
    Public Sub ExampleMethod()
        ' CA5364 violation for using Tls11
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
    End Sub
End Class
",
            GetBasicResultAt(8, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls11"),
            GetBasicResultAt(8, 78, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample2_CSharp_ViolationAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5364 violation
        ServicePointManager.SecurityProtocol = (SecurityProtocolType) 768;    // TLS 1.1
    }
}",
            GetCSharpResultAt(10, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "768"));
        }

        [Fact]
        public async Task DocSample2_VB_ViolationAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Net

Public Class TestClass
    Public Sub ExampleMethod()
        ' CA5364 violation
        ServicePointManager.SecurityProtocol = CType(768, SecurityProtocolType)   ' TLS 1.1
    End Sub
End Class
",
            GetBasicResultAt(8, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "768"));
        }

        [Fact]
        public async Task DocSample1_CSharp_SolutionAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

public class TestClass
{
    public void TestMethod()
    {
        // Let the operating system decide what TLS protocol version to use.
        // See https://docs.microsoft.com/dotnet/framework/network-programming/tls
    }
}");
        }

        [Fact]
        public async Task DocSample1_VB_SolutionAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Net

Public Class TestClass
    Public Sub ExampleMethod()
        ' Let the operating system decide what TLS protocol version to use.
        ' See https://docs.microsoft.com/dotnet/framework/network-programming/tls
    End Sub
End Class
");
        }

        [Fact]
        public async Task DocSample3_CSharp_ViolationAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5386 violation
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }
}",
            GetCSharpResultAt(10, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample3_VB_ViolationAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Net

Public Class TestClass
    Public Sub ExampleMethod()
        ' CA5386 violation
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
    End Sub
End Class
",
            GetBasicResultAt(8, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample4_CSharp_ViolationAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5386 violation
        ServicePointManager.SecurityProtocol = (SecurityProtocolType) 3072;    // TLS 1.2
    }
}",
            GetCSharpResultAt(10, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "3072"));
        }

        [Fact]
        public async Task DocSample4_VB_ViolationAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Net

Public Class TestClass
    Public Sub ExampleMethod()
        ' CA5386 violation
        ServicePointManager.SecurityProtocol = CType(3072, SecurityProtocolType)   ' TLS 1.2
    End Sub
End Class
",
            GetBasicResultAt(8, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "3072"));
        }

        [Fact]
        public async Task TestUseSsl3DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        var a = SecurityProtocolType.Ssl3;
    }
}",
            GetCSharpResultAt(9, 17, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Ssl3"));
        }

        [Fact]
        public async Task TestUseTlsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        var a = SecurityProtocolType.Tls;
    }
}",
            GetCSharpResultAt(9, 17, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls"));
        }

        [Fact]
        public async Task TestUseTls11DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11;
    }
}",
            GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls11"));
        }

        [Fact]
        public async Task TestUseSystemDefaultNoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net47.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        var a = SecurityProtocolType.SystemDefault;
    }
}",
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task TestUseTls12DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }
}",
                GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"));
        }

        [Fact]
        public async Task TestUseTls13DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net48.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls13"),
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task TestUseTls12OrdTls11DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
    }
}",
                GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"),
                GetCSharpResultAt(9, 77, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls11"));
        }

        [Fact]
        public async Task TestUse192CompoundAssignmentDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)192;
    }
}",
                GetCSharpResultAt(9, 49, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "192"));
        }

        [Fact]
        public async Task TestUse384SimpleAssignmentDiagnosticAsync()
        {
            // 384 = SchProtocols.Tls11Server | SchProtocols.Tls10Client
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)384;
    }
}",
                GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "384"));
        }

        [Fact]
        public async Task TestUse768SimpleAssignmentOrExpressionDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)768;
    }
}",
                GetCSharpResultAt(9, 87, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "768"));
        }

        [Fact]
        public async Task TestUse12288SimpleAssignmentOrExpressionDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)12288;
    }
}",
                GetCSharpResultAt(9, 87, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "12288"));
        }

        [Fact]
        public async Task TestUseTls12OrTls11Or192DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | (SecurityProtocolType)192;
    }
}",
                GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "Tls12"),
                GetCSharpResultAt(9, 77, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls11"));
        }

        [Fact]
        public async Task TestUseTls12Or192DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)192;
    }
}",
                VerifyCS.Diagnostic(DoNotUseDeprecatedSecurityProtocols.DeprecatedRule).WithSpan(9, 48, 9, 102).WithArguments("3264"),
                VerifyCS.Diagnostic(DoNotUseDeprecatedSecurityProtocols.HardCodedRule).WithSpan(9, 48, 9, 74).WithArguments("Tls12"));
        }

        [Fact]
        public async Task TestUse768DeconstructionAssignmentNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        int i;
        (ServicePointManager.SecurityProtocol, i) = ((SecurityProtocolType)384, 384);
    }
}");
            // Ideally we'd handle the IDeconstructionAssignment, but this code pattern seems unlikely.
        }

        [Fact]
        public async Task TestUse24Plus24SimpleAssignmentDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)(24 + 24);
    }
}",
                GetCSharpResultAt(9, 48, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "48"));
        }

        [Fact]
        public async Task TestUse768NotSecurityProtocolTypeNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        int i = 384 | 768;
    }
}");
        }

        [Fact]
        public async Task TestMaskOutUnsafeOnServicePointManagerNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.SecurityProtocol &= ~(SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11);
    }
}");
        }

        [Fact]
        public async Task TestMaskOutUnsafeOnVariableDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        SecurityProtocolType t = default(SecurityProtocolType);
        t &= ~(SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11);
    }
}",
                GetCSharpResultAt(10, 14, DoNotUseDeprecatedSecurityProtocols.HardCodedRule, "-1009"),
                GetCSharpResultAt(10, 16, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Ssl3"),
                GetCSharpResultAt(10, 44, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls"),
                GetCSharpResultAt(10, 71, DoNotUseDeprecatedSecurityProtocols.DeprecatedRule, "Tls11"));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(rule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyVB.Diagnostic(rule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);
    }
}
