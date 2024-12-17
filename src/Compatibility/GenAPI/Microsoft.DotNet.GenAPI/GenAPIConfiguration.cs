// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Text.RegularExpressions;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.GenAPI.Filtering;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.DotNet.GenAPI;

public class GenAPIConfiguration
{
    private GenAPIConfiguration()
    {
    }

    [AllowNull]
    public AssemblySymbolLoader Loader { get; private set; }
    [AllowNull]
    public Dictionary<string, IAssemblySymbol> AssemblySymbols { get; private set; }

    public static Builder GetBuilder() => new Builder();

    public static ISymbolFilter GetSymbolFilterFromFiles(string[]? apiExclusionFilePaths,
                                                                 bool respectInternals = false,
                                                                 bool includeEffectivelyPrivateSymbols = true,
                                                                 bool includeExplicitInterfaceImplementationSymbols = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            apiExclusionFilePaths?.Count() > 0 ?
            DocIdSymbolFilter.CreateFromFiles(apiExclusionFilePaths) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: true);
    }

    public static ISymbolFilter GetSymbolFilterFromList(string[]? apiExclusionList,
                                                                bool respectInternals = false,
                                                                bool includeEffectivelyPrivateSymbols = true,
                                                                bool includeExplicitInterfaceImplementationSymbols = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            apiExclusionList?.Count() > 0 ?
            DocIdSymbolFilter.CreateFromDocIDs(apiExclusionList) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: true);
    }

    public static ISymbolFilter GetAttributeFilterFromPaths(string[]? attributeExclusionFilePaths,
                                                                    bool respectInternals = false,
                                                                    bool includeEffectivelyPrivateSymbols = true,
                                                                    bool includeExplicitInterfaceImplementationSymbols = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            attributeExclusionFilePaths?.Count() > 0 ?
            DocIdSymbolFilter.CreateFromFiles(attributeExclusionFilePaths) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: false);
    }

    public static ISymbolFilter GetAttributeFilterFromList(string[]? attributeExclusionList,
                                                                   bool respectInternals = false,
                                                                   bool includeEffectivelyPrivateSymbols = true,
                                                                   bool includeExplicitInterfaceImplementationSymbols = true)
    {
        DocIdSymbolFilter? docIdSymbolFilter =
            attributeExclusionList?.Count() > 0 ?
            DocIdSymbolFilter.CreateFromDocIDs(attributeExclusionList) : null;

        return GetCompositeSymbolFilter(docIdSymbolFilter, respectInternals, includeEffectivelyPrivateSymbols, includeExplicitInterfaceImplementationSymbols, withImplicitSymbolFilter: false);
    }

    private static ISymbolFilter GetCompositeSymbolFilter(DocIdSymbolFilter? customFilter,
                                                                  bool respectInternals,
                                                                  bool includeEffectivelyPrivateSymbols,
                                                                  bool includeExplicitInterfaceImplementationSymbols,
                                                                  bool withImplicitSymbolFilter)
    {
        AccessibilitySymbolFilter accessibilitySymbolFilter = new(
                respectInternals,
                includeEffectivelyPrivateSymbols,
                includeExplicitInterfaceImplementationSymbols);

        CompositeSymbolFilter filter = new();

        if (customFilter != null)
        {
            filter.Add(customFilter);
        }
        if (withImplicitSymbolFilter)
        {
            filter.Add(new ImplicitSymbolFilter());
        }

        filter.Add(accessibilitySymbolFilter);

        return filter;
    }


    public static string GetFormattedHeader(string? customHeader = null)
    {
        const string defaultFileHeader = """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------

            """;

        if (customHeader != null)
        {
#if NET
            return customHeader.ReplaceLineEndings();
#else
            return Regex.Replace(customHeader, @"\r\n|\n\r|\n|\r", Environment.NewLine);
#endif
        }

        return defaultFileHeader;
    }

    public class Builder
    {
        private string[]? _assembliesPaths = null;
        private string[]? _assemblyReferencesPaths = null;
        private (string, string)[]? _assemblyTexts;
        private bool _allowUnsafe = false;
        private bool _respectInternals = false;

        public Builder WithAssembliesPaths(params string[]? assembliesPaths)
        {
            if (assembliesPaths == null)
            {
                return this;
            }
            if (_assemblyTexts != null)
            {
                throw new InvalidOperationException("Cannot specify both assembly paths and streams.");
            }
            foreach (string path in assembliesPaths)
            {
                ThrowIfFileSystemEntryNotFound(nameof(assembliesPaths), path);
            }
            _assembliesPaths = assembliesPaths;
            return this;
        }

        public Builder WithAssemblyTexts(params (string, string)[]? assemblyTexts)
        {
            if (assemblyTexts == null)
            {
                return this;
            }
            if (_assembliesPaths != null)
            {
                throw new InvalidOperationException("Cannot specify both assembly paths and streams.");
            }

            _assemblyTexts = assemblyTexts;
            return this;
        }

        public Builder WithAssemblyReferencesPaths(params string[]? assemblyReferencesPaths)
        {
            if (assemblyReferencesPaths == null)
            {
                return this;
            }
            foreach (string path in assemblyReferencesPaths)
            {
                ThrowIfFileSystemEntryNotFound(nameof(assemblyReferencesPaths), path);
            }
            _assemblyReferencesPaths = assemblyReferencesPaths;
            return this;
        }

        public Builder WithAllowUnsafe(bool allowUnsafe)
        {
            _allowUnsafe = allowUnsafe;
            return this;
        }

        public Builder WithRespectInternals(bool respectInternals)
        {
            _respectInternals = respectInternals;
            return this;
        }

        public GenAPIConfiguration Build()
        {
            AssemblySymbolLoader loader;
            Dictionary<string, IAssemblySymbol> assemblySymbols;

            if (_assembliesPaths?.Length > 0)
            {
                bool resolveAssemblyReferences = _assemblyReferencesPaths?.Count() > 0;
                loader = new AssemblySymbolLoader(resolveAssemblyReferences, _respectInternals);
                if (_assemblyReferencesPaths?.Length > 0)
                {
                    loader.AddReferenceSearchPaths(_assemblyReferencesPaths);
                }
                assemblySymbols = new Dictionary<string, IAssemblySymbol>(loader.LoadAssembliesAsDictionary(_assembliesPaths));
            }
            else if (_assemblyTexts?.Count() > 0)
            {
                loader = new AssemblySymbolLoader(resolveAssemblyReferences: true, includeInternalSymbols: _respectInternals);
                loader.AddReferenceSearchPaths(typeof(object).Assembly!.Location!);
                loader.AddReferenceSearchPaths(typeof(DynamicAttribute).Assembly!.Location!);

                assemblySymbols = new Dictionary<string, IAssemblySymbol>();
                foreach ((string assemblyName, string assemblyText) in _assemblyTexts)
                {
                    using Stream assemblyStream = EmitAssemblyStreamFromSyntax(assemblyText, enableNullable: true, allowUnsafe: _allowUnsafe, assemblyName: assemblyName);
                    if (loader.LoadAssembly(assemblyName, assemblyStream) is IAssemblySymbol assemblySymbol)
                    {
                        assemblySymbols.Add(assemblyName, assemblySymbol);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("No assemblies were specified, either from files or from streams.");
            }

            return new GenAPIConfiguration()
            {
                Loader = loader,
                AssemblySymbols = assemblySymbols
            };
        }


        private static IEnumerable<KeyValuePair<string, ReportDiagnostic>> DiagnosticOptions { get; } = new[]
        {
            // Suppress warning for unused events.
            new KeyValuePair<string, ReportDiagnostic>("CS0067", ReportDiagnostic.Suppress)
        };

        private static IEnumerable<MetadataReference> DefaultReferences { get; } = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DynamicAttribute).Assembly.Location),
        };

        public static Stream EmitAssemblyStreamFromSyntax(string syntax,
            bool enableNullable = false,
            byte[]? publicKey = null,
            [CallerMemberName] string assemblyName = "",
            bool allowUnsafe = false)
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax([syntax], assemblyName, enableNullable, publicKey, allowUnsafe);

            Debug.Assert(compilation.GetDiagnostics().IsEmpty);

            MemoryStream stream = new();
            compilation.Emit(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static SyntaxTree GetSyntaxTree(string syntax)
        {
            return CSharpSyntaxTree.ParseText(syntax, ParseOptions);
        }

        private static CSharpParseOptions ParseOptions { get; } = new CSharpParseOptions(preprocessorSymbols:
#if NETFRAMEWORK
                new string[] { "NETFRAMEWORK" }
#else
                Array.Empty<string>()
#endif
        );

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(IEnumerable<string> syntax, string name, bool enableNullable, byte[]? publicKey, bool allowUnsafe)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, publicKey, allowUnsafe);
            IEnumerable<SyntaxTree> syntaxTrees = syntax.Select(s => GetSyntaxTree(s));
            return compilation.AddSyntaxTrees(syntaxTrees);
        }

        private static CSharpCompilation CreateCSharpCompilation(string name, bool enableNullable, byte[]? publicKey, bool allowUnsafe)
        {
            bool publicSign = publicKey != null ? true : false;
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                                  publicSign: publicSign,
                                                                  cryptoPublicKey: publicSign ? publicKey!.ToImmutableArray() : default,
                                                                  nullableContextOptions: enableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
                                                                  allowUnsafe: allowUnsafe,
                                                                  specificDiagnosticOptions: DiagnosticOptions);

            return CSharpCompilation.Create(name, options: compilationOptions, references: DefaultReferences);
        }


        private void ThrowIfFileSystemEntryNotFound(string argumentName, string path)
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                return;
            }

            throw new FileNotFoundException($"The {argumentName} is not a valid file or directory: {path}");
        }
    }
}
