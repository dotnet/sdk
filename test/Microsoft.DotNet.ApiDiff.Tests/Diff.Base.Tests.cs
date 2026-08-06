// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI.Tests;
using Moq;
using VerifyTests;

namespace Microsoft.DotNet.ApiDiff.Tests;

public abstract class DiffBaseTests
{
    private readonly Mock<ILog> _log = new();
    protected const string AssemblyName = "MyAssembly";
    private readonly string[] _separator = [Environment.NewLine];

    protected Task RunTestAsync(
                           string beforeCode,
                           string afterCode,
                           string expectedCode,
                           string[]? attributesToExclude = null,
                           string[]? apisToExclude = null,
                           bool addPartialModifier = false)
        => RunTestAsync(
                   before: [($"{AssemblyName}.dll", beforeCode)],
                   after: [($"{AssemblyName}.dll", afterCode)],
                   expected: new() { { AssemblyName, expectedCode } },
                   attributesToExclude,
                   apisToExclude,
                   addPartialModifier);

    protected async Task RunTestAsync(
                           (string, string)[] before,
                           (string, string)[] after,
                           Dictionary<string, string> expected,
                           string[]? attributesToExclude = null,
                           string[]? apisToExclude = null,
                           bool addPartialModifier = false)
    {
        // CreateFromTexts will assert on any loader diagnostics via SyntaxFactory.

        (IAssemblySymbolLoader beforeLoader, Dictionary<string, IAssemblySymbol> beforeAssemblySymbols)
            = TestAssemblyLoaderFactory.CreateFromTexts(_log.Object, assemblyTexts: before, diagnosticOptions: DiffGeneratorFactory.DefaultDiagnosticOptions);

        (IAssemblySymbolLoader afterLoader, Dictionary<string, IAssemblySymbol> afterAssemblySymbols)
            = TestAssemblyLoaderFactory.CreateFromTexts(_log.Object, assemblyTexts: after, diagnosticOptions: DiffGeneratorFactory.DefaultDiagnosticOptions);

        using MemoryStream outputStream = new();

        IDiffGenerator generator = DiffGeneratorFactory.Create(
            _log.Object,
            beforeLoader,
            afterLoader,
            beforeAssemblySymbols,
            afterAssemblySymbols,
            attributesToExclude,
            apisToExclude,
            addPartialModifier,
            DiffGeneratorFactory.DefaultDiagnosticOptions);

        await generator.RunAsync(CancellationToken.None);

        foreach ((string expectedAssemblyName, string expectedCode) in expected)
        {
            if (string.IsNullOrEmpty(expectedCode))
            {
                Assert.False(generator.Results.TryGetValue(expectedAssemblyName, out string? _), $"Assembly should've been absent among the results: {expectedAssemblyName}");
            }
            else
            {
                Assert.True(generator.Results.TryGetValue(expectedAssemblyName, out string? actualCode), $"Assembly should've been present among the results: {expectedAssemblyName}");
                string fullExpectedCode = GetExpected(expectedCode, expectedAssemblyName);
                if (!fullExpectedCode.Equals(actualCode))
                {
                    Assert.Fail($"Expected:\n[{ReplacedNewLines(fullExpectedCode)}]\nActual:\n[{ReplacedNewLines(actualCode)}]");
                }
            }
        }
    }

    private static string ReplacedNewLines(string orig) => orig.Replace("\n", "\\n\n").Replace("\r", "\\r");

    private static string GetExpected(string expectedCode, string expectedAssemblyName) =>
        $"""
        # {Path.GetFileNameWithoutExtension(expectedAssemblyName)}

        ```diff
        {expectedCode}
        ```

        """;
}
