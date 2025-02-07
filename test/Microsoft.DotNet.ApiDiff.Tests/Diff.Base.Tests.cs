// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI.Tests;

namespace Microsoft.DotNet.ApiDiff.Tests;

public abstract class DiffBaseTests
{
    private readonly ConsoleLog _log = new(MessageImportance.Normal);
    protected const string AssemblyName = "MyAssembly";

    protected void RunTest(string beforeCode,
                           string afterCode,
                           string expectedCode,
                           string[]? attributesToExclude = null,
                           bool addPartialModifier = false,
                           bool hideImplicitDefaultConstructors = false)
        => RunTest(before: [($"{AssemblyName}.dll", beforeCode)],
                   after: [($"{AssemblyName}.dll", afterCode)],
                   expected: new() { { AssemblyName, expectedCode } },
                   attributesToExclude,
                   addPartialModifier,
                   hideImplicitDefaultConstructors);

    protected void RunTest((string, string)[] before,
                           (string, string)[] after,
                           Dictionary<string, string> expected,
                           string[]? attributesToExclude = null,
                           bool addPartialModifier = false,
                           bool hideImplicitDefaultConstructors = false)
    {
        // CreateFromTexts will assert on any loader diagnostics via SyntaxFactory.
        (IAssemblySymbolLoader beforeLoader, Dictionary<string, IAssemblySymbol> beforeAssemblySymbols)
            = TestAssemblyLoaderFactory.CreateFromTexts(_log, assemblyTexts: before, diagnosticOptions: DiffGeneratorFactory.DefaultDiagnosticOptions);
        (IAssemblySymbolLoader afterLoader, Dictionary<string, IAssemblySymbol> afterAssemblySymbols)
            = TestAssemblyLoaderFactory.CreateFromTexts(_log, assemblyTexts: after, diagnosticOptions: DiffGeneratorFactory.DefaultDiagnosticOptions);

        using MemoryStream outputStream = new();

        IDiffGenerator generator = DiffGeneratorFactory.Create(
            _log,
            attributesToExclude,
            beforeLoader,
            afterLoader,
            beforeAssemblySymbols,
            afterAssemblySymbols,
            addPartialModifier,
            hideImplicitDefaultConstructors,
            DiffGeneratorFactory.DefaultDiagnosticOptions);

        generator.Run();

        foreach ((string expectedAssemblyName, string expectedCode) in expected)
        {
            Assert.True(generator.Results.TryGetValue(expectedAssemblyName, out string? actualCode), $"Expected assembly entry not found among actual results: {expectedAssemblyName}");
            string fullExpectedCode = GetExpected(expectedCode, expectedAssemblyName);
            Assert.True(fullExpectedCode.Equals(actualCode), $"\nExpected:\n{fullExpectedCode}\nActual:\n{actualCode}");
        }
    }

    private static string GetExpected(string expectedCode, string expectedAssemblyName)
    {
        return $"""
                # {Path.GetFileNameWithoutExtension(expectedAssemblyName)}

                ```diff
                {expectedCode}
                ```

                """;
    }
}
