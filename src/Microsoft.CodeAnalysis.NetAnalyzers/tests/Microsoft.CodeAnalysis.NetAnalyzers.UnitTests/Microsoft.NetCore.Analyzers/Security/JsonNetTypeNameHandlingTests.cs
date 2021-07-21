// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.JsonNetTypeNameHandling,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.JsonNetTypeNameHandling,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class JsonNetTypeNameHandlingTests
    {
        private static readonly DiagnosticDescriptor Rule = JsonNetTypeNameHandling.Rule;

        [Theory]
        [CombinatorialData]
        public async Task DocSample1_CSharp_Violation_Diagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using Newtonsoft.Json;

public class ExampleClass
{
    public JsonSerializerSettings Settings { get; }

    public ExampleClass()
    {
        Settings = new JsonSerializerSettings();
        Settings.TypeNameHandling = TypeNameHandling.All;    // CA2326 violation.
    }
}",
                GetCSharpResultAt(11, 37, Rule));
        }

        [Theory]
        [CombinatorialData]
        public async Task DocSample1_VB_Violation_Diagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyBasicWithJsonNetAsync(version, @"
Imports Newtonsoft.Json

Public Class ExampleClass
    Public ReadOnly Property Settings() As JsonSerializerSettings

    Public Sub New()
        Settings = New JsonSerializerSettings()
        Settings.TypeNameHandling = TypeNameHandling.All    ' CA2326 violation.
    End Sub
End Class",
                GetBasicResultAt(9, 37, Rule));
        }

        [Theory]
        [CombinatorialData]
        public async Task DocSample1_CSharp_Solution_NoDiagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using Newtonsoft.Json;

public class ExampleClass
{
    public JsonSerializerSettings Settings { get; }

    public ExampleClass()
    {
        Settings = new JsonSerializerSettings();
        
        // The default value of Settings.TypeNameHandling is TypeNameHandling.None.
    }
}");
        }

        [Theory]
        [CombinatorialData]
        public async Task DocSample1_VB_Solution_NoDiagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyBasicWithJsonNetAsync(version, @"
Imports Newtonsoft.Json

Public Class ExampleClass
    Public ReadOnly Property Settings() As JsonSerializerSettings

    Public Sub New()
        Settings = New JsonSerializerSettings()

        ' The default value of Settings.TypeNameHandling is TypeNameHandling.None.
    End Sub
End Class");
        }

        [Theory]
        [CombinatorialData]
        public async Task Reference_TypeNameHandling_None_NoDiagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        Console.WriteLine(TypeNameHandling.None);
    }
}");
        }

        [Theory]
        [CombinatorialData]
        public async Task Reference_TypeNameHandling_All_Diagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        Console.WriteLine(TypeNameHandling.All);
    }
}",
                GetCSharpResultAt(9, 27, Rule));
        }

        [Theory]
        [CombinatorialData]
        public async Task Reference_AttributeTargets_All_NoDiagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        Console.WriteLine(AttributeTargets.All);
    }
}");
        }

        [Theory]
        [CombinatorialData]
        public async Task Assign_TypeNameHandling_Objects_Diagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        TypeNameHandling tnh = TypeNameHandling.Objects;
    }
}",
                GetCSharpResultAt(9, 32, Rule));
        }

        [Theory]
        [CombinatorialData]
        public async Task Assign_TypeNameHandling_1_Or_Arrays_Diagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        TypeNameHandling tnh = (TypeNameHandling) 1 | TypeNameHandling.Arrays;
    }
}",
                GetCSharpResultAt(9, 55, Rule));
        }

        [Theory]
        [CombinatorialData]
        public async Task Assign_TypeNameHandling_0_NoDiagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        TypeNameHandling tnh = (TypeNameHandling) 0;
    }
}");
        }

        [Theory]
        [CombinatorialData]
        public async Task Assign_TypeNameHandling_None_NoDiagnostic(NewtonsoftJsonVersion version)
        {
            await VerifyCSharpWithJsonNetAsync(version, @"
using System;
using Newtonsoft.Json;

class Blah
{
    public static void Main(string[] args)
    {
        TypeNameHandling tnh = TypeNameHandling.None;
    }
}");
        }

        private async Task VerifyCSharpWithJsonNetAsync(NewtonsoftJsonVersion version, string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = version switch
                {
                    NewtonsoftJsonVersion.Version10 => AdditionalMetadataReferences.DefaultWithNewtonsoftJson10,
                    NewtonsoftJsonVersion.Version12 => AdditionalMetadataReferences.DefaultWithNewtonsoftJson12,
                    _ => throw new NotSupportedException(),
                },
                TestState =
                {
                    Sources = { source },
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private async Task VerifyBasicWithJsonNetAsync(NewtonsoftJsonVersion version, string source, params DiagnosticResult[] expected)
        {
            var vbTest = new VerifyVB.Test
            {
                ReferenceAssemblies = version switch
                {
                    NewtonsoftJsonVersion.Version10 => AdditionalMetadataReferences.DefaultWithNewtonsoftJson10,
                    NewtonsoftJsonVersion.Version12 => AdditionalMetadataReferences.DefaultWithNewtonsoftJson12,
                    _ => throw new NotSupportedException(),
                },
                TestState =
                {
                    Sources = { source },
                },
            };

            vbTest.ExpectedDiagnostics.AddRange(expected);

            await vbTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(rule)
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyVB.Diagnostic(rule)
               .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
