// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal static class SymbolFactory
    {
        internal static IAssemblySymbol GetAssemblyFromSyntax(string syntax, bool enableNullable = false, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable);

            Assert.Empty(compilation.GetDiagnostics());

            return compilation.Assembly;
        }

        internal static IAssemblySymbol GetAssemblyFromSyntaxWithReferences(string syntax, IEnumerable<string> referencesSyntax, bool enableNullable = false, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable);
            CSharpCompilation compilationWithReferences = CreateCSharpCompilationFromSyntax(referencesSyntax, $"{assemblyName}_reference", enableNullable);

            compilation = compilation.AddReferences(compilationWithReferences.ToMetadataReference());

            Assert.Empty(compilation.GetDiagnostics());

            return compilation.Assembly;
        }

        internal static IList<ElementContainer<IAssemblySymbol>> GetElementContainersFromSyntaxes(IEnumerable<string> syntaxes, IEnumerable<string> referencesSyntax = null, bool enableNullable = false, [CallerMemberName] string assemblyName = "")
        {
            int i = 0;
            List<ElementContainer<IAssemblySymbol>> result = new();
            foreach (string syntax in syntaxes)
            {
                MetadataInformation info = new(string.Empty, string.Empty, $"runtime-{i++}");
                IAssemblySymbol symbol = referencesSyntax != null ?
                    GetAssemblyFromSyntaxWithReferences(syntax, referencesSyntax, enableNullable, assemblyName) :
                    GetAssemblyFromSyntax(syntax, enableNullable, assemblyName);

                ElementContainer<IAssemblySymbol> container = new(symbol, info);
                result.Add(container);
            }

            return result;
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(string syntax, string name, bool enableNullable)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable);
            return compilation.AddSyntaxTrees(GetSyntaxTree(syntax));
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(IEnumerable<string> syntax, string name, bool enableNullable)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable);
            IEnumerable<SyntaxTree> syntaxTrees = syntax.Select(s => GetSyntaxTree(s));
            return compilation.AddSyntaxTrees(syntaxTrees);
        }

        private static SyntaxTree GetSyntaxTree(string syntax)
        {
            IEnumerable<string> defineConstants =
#if NETFRAMEWORK
                new string[] { "NETFRAMEWORK" };
#else
                Array.Empty<string>();
#endif
            CSharpParseOptions options = new(preprocessorSymbols: defineConstants);

            return CSharpSyntaxTree.ParseText(syntax, options);
        }

        private static CSharpCompilation CreateCSharpCompilation(string name, bool enableNullable)
        {
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                                  nullableContextOptions: enableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable);

            // Suppress diagnostics that we don't care about in the tests.
            compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(new[]
            {
                // Suppress warning for unused fields.
                new KeyValuePair<string, ReportDiagnostic>("CS0067", ReportDiagnostic.Suppress)
            });
            return CSharpCompilation.Create(name, options: compilationOptions, references: DefaultReferences);
        }

        private static IEnumerable<MetadataReference> DefaultReferences { get; } = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
    }
}
