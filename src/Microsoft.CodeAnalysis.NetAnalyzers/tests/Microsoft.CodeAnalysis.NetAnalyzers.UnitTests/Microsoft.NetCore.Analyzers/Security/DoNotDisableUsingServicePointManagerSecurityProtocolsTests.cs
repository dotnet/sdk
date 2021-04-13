// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotSetSwitch,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotSetSwitch,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotDisableUsingServicePointManagerSecurityProtocolsTests
    {
        [Fact]
        public async Task DocSample1_CSharp_Violation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5378 violation
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", true);
    }
}",
            GetCSharpResultAt(9, 9, "SetSwitch"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class ExampleClass
    Public Sub ExampleMethod()
        ' CA5378 violation
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", true)
    End Sub
End Class",
            GetBasicResultAt(7, 9, "SetSwitch"));
        }

        [Fact]
        public async Task DocSample1_CSharp_Solution()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class ExampleClass
{
    public void ExampleMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", false);
    }
}");
        }

        [Fact]
        public async Task DocSample1_VB_Solution()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class ExampleClass
    Public Sub ExampleMethod()
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", false)
    End Sub
End Class");
        }

        [Fact]
        public async Task TestBoolDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", true);
    }
}",
            GetCSharpResultAt(8, 9, "SetSwitch"));
        }

        [Fact]
        public async Task TestEquationDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", 1 + 2 == 3);
    }
}",
            GetCSharpResultAt(8, 9, "SetSwitch"));
        }

        [Fact]
        public async Task TestConditionalOperatorDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", 1 == 1 ? true : false);
    }
}",
            GetCSharpResultAt(8, 9, "SetSwitch"));
        }

        [Fact]
        public async Task TestWithConstantSwitchNameDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        const string constSwitchName = ""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"";
        AppContext.SetSwitch(constSwitchName, true);
    }
}",
            GetCSharpResultAt(9, 9, "SetSwitch"));
        }

        [Fact]
        public async Task TestBoolNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", false);
    }
}");
        }

        [Fact]
        public async Task TestEquationNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", 1 + 2 != 3);
    }
}");
        }

        [Fact]
        public async Task TestConditionalOperatorNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", 1 == 1 ? false : true);
    }
}");
        }

        [Fact]
        public async Task TestSwitchNameNullNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(null, true);
    }
}");
        }

        [Fact]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        public async Task TestSwitchNameVariableNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        string switchName = ""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"";
        AppContext.SetSwitch(switchName, true);
    }
}",
                GetCSharpResultAt(9, 9, "SetSwitch"));
        }

        //Ideally, we would generate a diagnostic in this case.
        [Fact]
        public async Task TestBoolParseNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class TestClass
{
    public void TestMethod()
    {
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", bool.Parse(""true""));
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = ExampleMethod")]
        [InlineData("dotnet_code_quality.CA5378.excluded_symbol_names = ExampleMethod")]
        [InlineData("dotnet_code_quality.CA5378.excluded_symbol_names = ExampleMet*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = ExampleMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5378 violation
        AppContext.SetSwitch(""Switch.System.ServiceModel.DisableUsingServicePointManagerSecurityProtocols"", true);
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };

            if (editorConfigText.Length == 0)
            {
                test.ExpectedDiagnostics.Add(GetCSharpResultAt(9, 9, "SetSwitch"));
            }

            await test.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotSetSwitch.DoNotDisableSpmSecurityProtocolsRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotSetSwitch.DoNotDisableSpmSecurityProtocolsRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
