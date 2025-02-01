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
    protected const string AssemblyName = "MyAssembly.dll";

    protected void RunTest(string beforeCode,
                           string afterCode,
                           string expectedCode,
                           bool addPartialModifier = false,
                           bool hideImplicitDefaultConstructors = false)
        => RunTest(before: [(AssemblyName, beforeCode)],
                   after: [(AssemblyName, afterCode)],
                   expected: new() { { AssemblyName, expectedCode } },
                   addPartialModifier, hideImplicitDefaultConstructors);

    protected void RunTest((string, string)[] before,
                           (string, string)[] after,
                           Dictionary<string, string> expected,
                           bool addPartialModifier = false,
                           bool hideImplicitDefaultConstructors = false)
    {
        string[] attributesToExclude = Array.Empty<string>(); // TODO: Add tests for this

        // CreateFromTexts will assert on any loader diagnostics via SyntaxFactory.
        (IAssemblySymbolLoader beforeLoader, Dictionary<string, IAssemblySymbol> beforeAssemblySymbols)
            = TestAssemblyLoaderFactory.CreateFromTexts(_log, assemblyTexts: before, diagnosticOptions: DiffGenerator.DefaultDiagnosticOptions);
        (IAssemblySymbolLoader afterLoader, Dictionary<string, IAssemblySymbol> afterAssemblySymbols)
            = TestAssemblyLoaderFactory.CreateFromTexts(_log, assemblyTexts: after, diagnosticOptions: DiffGenerator.DefaultDiagnosticOptions);

        using MemoryStream outputStream = new();

        Dictionary<string, string> actualResults = DiffGenerator.Run(
            _log,
            attributesToExclude,
            beforeLoader,
            afterLoader,
            beforeAssemblySymbols,
            afterAssemblySymbols,
            addPartialModifier,
            hideImplicitDefaultConstructors,
            DiffGenerator.DefaultDiagnosticOptions);

        foreach ((string expectedAssemblyName, string expectedCode) in expected)
        {
            Assert.True(actualResults.TryGetValue(expectedAssemblyName, out string? actualCode), $"Expected assembly entry not found among actual results: {expectedAssemblyName}");
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
