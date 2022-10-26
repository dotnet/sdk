// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.DotNet.GenAPI.Tests;

internal class CompilationHelper
{
    internal static IAssemblySymbol GetAssemblyFromSyntax(string syntax, bool enableNullable = false, byte[] publicKey = null, [CallerMemberName] string assemblyName = "")
    {
        CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey);

        Assert.Empty(compilation.GetDiagnostics());

        return compilation.Assembly;
    }

    private static CSharpCompilation CreateCSharpCompilationFromSyntax(string syntax, string name, bool enableNullable, byte[] publicKey)
    {
        CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, publicKey);
        return compilation.AddSyntaxTrees(GetSyntaxTree(syntax));
    }

    private static SyntaxTree GetSyntaxTree(string syntax)
    {
        return CSharpSyntaxTree.ParseText(syntax, ParseOptions);
    }

    private static CSharpCompilation CreateCSharpCompilation(string name, bool enableNullable, byte[] publicKey)
    {
        bool publicSign = publicKey != null ? true : false;
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                              publicSign: publicSign,
                                                              cryptoPublicKey: publicSign ? publicKey.ToImmutableArray() : default,
                                                              nullableContextOptions: enableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
                                                              specificDiagnosticOptions: DiagnosticOptions);

        return CSharpCompilation.Create(name, options: compilationOptions, references: DefaultReferences);
    }

    private static CSharpParseOptions ParseOptions { get; } = new(preprocessorSymbols:
#if NETFRAMEWORK
                new string[] { "NETFRAMEWORK" }
#else
            Array.Empty<string>()
#endif
    );

    private static IEnumerable<KeyValuePair<string, ReportDiagnostic>> DiagnosticOptions { get; } = new[]
    {
        // Suppress warning for unused events.
        new KeyValuePair<string, ReportDiagnostic>("CS0067", ReportDiagnostic.Suppress)
    };

    private static IEnumerable<MetadataReference> DefaultReferences { get; } = new[]
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
    };

}
