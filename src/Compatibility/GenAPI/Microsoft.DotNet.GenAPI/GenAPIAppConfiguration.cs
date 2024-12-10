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
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI.Filtering;

namespace Microsoft.DotNet.GenAPI;

public enum OutputType
{
    Console,
    Files,
    Diff
}

public class GenApiAppConfiguration
{
    private GenApiAppConfiguration()
    {
    }

    [AllowNull]
    public ILog Logger { get; private set; }
    public OutputType OutputType { get; private set; } = OutputType.Console;
    public string? OutputPath { get; private set; }
    [AllowNull]
    public string Header { get; private set; }
    public string? ExceptionMessage { get; private set; }
    public bool IncludeAssemblyAttributes { get; private set; }
    [AllowNull]
    public AssemblySymbolLoader Loader { get; private set; }
    [AllowNull]
    public IReadOnlyList<IAssemblySymbol?> AssemblySymbols { get; private set; }
    [AllowNull]
    public CompositeSymbolFilter SymbolFilter { get; private set; }
    [AllowNull]
    public CompositeSymbolFilter AttributeDataSymbolFilter { get; private set; }

    public static Builder GetBuilder() => new Builder();

    public class Builder
    {
        private ILog? _logger = null;
        private string[]? _assembliesPaths = null;
        private string[]? _assemblyReferencesPaths = null;
        private OutputType _outputType = OutputType.Console;
        private string? _outputPath = null;
        private (string, Stream)[]? _assemblyStreams;
        private string? _header = null;
        private string? _exceptionMessage = null;
        private string[]? _apiExclusionFilePaths = null;
        private string[]? _attributeExclusionFilePaths = null;
        bool _includeEffectivelyPrivateSymbols = true;
        bool _includeExplicitInterfaceImplementationSymbols = true;
        bool _respectInternals = false;
        bool _includeAssemblyAttributes = false;

        public Builder WithLogger(ILog logger)
        {
            _logger = logger;
            return this;
        }

        public Builder WithAssembliesPaths(params string[]? assembliesPaths)
        {
            if (assembliesPaths == null)
            {
                return this;
            }
            if (_assemblyStreams != null)
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

        public Builder WithAssemblyStreams(params (string, Stream)[]? assemblyStreams)
        {
            if (assemblyStreams == null)
            {
                return this;
            }
            if (_assembliesPaths != null)
            {
                throw new InvalidOperationException("Cannot specify both assembly paths and streams.");
            }

            _assemblyStreams = assemblyStreams;
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

        public Builder WithOutputPath(string? outputPath)
        {
            if (outputPath == null)
            {
                return this;
            }
            ThrowIfDirectoryNotFound(nameof(outputPath), outputPath);
            _outputPath = outputPath;
            _outputType = OutputType.Files;
            return this;
        }

        public Builder WithHeaderFilePath(string? headerFilePath)
        {
            if (headerFilePath == null)
            {
                return this;
            }
            ThrowIfFileNotFound(nameof(headerFilePath), headerFilePath);
            return WithHeader(File.ReadAllText(headerFilePath));
        }

        public Builder WithHeader(string? header)
        {
            if (header == null)
            {
                return this;
            }
            _header = header;
            return this;
        }

        public Builder WithExceptionMessage(string? exceptionMessage)
        {
            if (exceptionMessage == null)
            {
                return this;
            }
            _exceptionMessage = exceptionMessage;
            return this;
        }

        public Builder WithApiExclusionFilePaths(params string[]? apiExclusionFilePaths)
        {
            if (apiExclusionFilePaths == null)
            {
                return this;
            }

            foreach (string path in apiExclusionFilePaths)
            {
                ThrowIfPathIsDirectory(nameof(apiExclusionFilePaths), path);
            }
            _apiExclusionFilePaths = apiExclusionFilePaths;
            return this;
        }

        public Builder WithAttributeExclusionFilePaths(params string[]? attributeExclusionFilePaths)
        {
            if (attributeExclusionFilePaths == null)
            {
                return this;
            }

            foreach (string path in attributeExclusionFilePaths)
            {
                ThrowIfPathIsDirectory(nameof(attributeExclusionFilePaths), path);
            }
            _attributeExclusionFilePaths = attributeExclusionFilePaths;
            return this;
        }

        public Builder WithIncludeEffectivelyPrivateSymbols(bool includeEffectivelyPrivateSymbols)
        {
            _includeEffectivelyPrivateSymbols = includeEffectivelyPrivateSymbols;
            return this;
        }

        public Builder WithIncludeExplicitInterfaceImplementationSymbols(bool includeExplicitInterfaceImplementationSymbols)
        {
            _includeExplicitInterfaceImplementationSymbols = includeExplicitInterfaceImplementationSymbols;
            return this;
        }

        public Builder WithRespectInternals(bool respectInternals)
        {
            _respectInternals = respectInternals;
            return this;
        }

        public Builder WithIncludeAssemblyAttributes(bool includeAssemblyAttributes)
        {
            _includeAssemblyAttributes = includeAssemblyAttributes;
            return this;
        }

        public GenApiAppConfiguration Build()
        {
            AssemblySymbolLoader loader;
            IReadOnlyList<IAssemblySymbol?> assemblySymbols;

            if (_assembliesPaths?.Length > 0)
            {
                bool resolveAssemblyReferences = _assemblyReferencesPaths?.Count() > 0;
                loader = new(resolveAssemblyReferences, _respectInternals);
                if (_assemblyReferencesPaths?.Count() > 0)
                {
                    loader.AddReferenceSearchPaths(_assemblyReferencesPaths);
                }
                assemblySymbols = loader.LoadAssemblies(_assembliesPaths);
            }
            else if (_assemblyStreams?.Count() > 0)
            {
                loader = new(resolveAssemblyReferences: true, includeInternalSymbols: _respectInternals);
                loader.AddReferenceSearchPaths(typeof(object).Assembly!.Location!);
                loader.AddReferenceSearchPaths(typeof(DynamicAttribute).Assembly!.Location!);
                List<IAssemblySymbol> symbols = [];
                foreach ((string assemblyName, Stream assemblyStream) in _assemblyStreams)
                {
                    if (loader.LoadAssembly(assemblyName, assemblyStream) is IAssemblySymbol assemblySymbol)
                    {
                        symbols.Add(assemblySymbol);
                    }
                }
                assemblySymbols = symbols.AsReadOnly();
            }
            else
            {
                throw new InvalidOperationException("No assemblies were specified, either from files or from streams.");
            }

            AccessibilitySymbolFilter accessibilitySymbolFilter = new(
                _respectInternals,
                includeEffectivelyPrivateSymbols: _includeEffectivelyPrivateSymbols,
                includeExplicitInterfaceImplementationSymbols: _includeExplicitInterfaceImplementationSymbols);

            // Configure the symbol filter
            CompositeSymbolFilter symbolFilter = new();
            if (_apiExclusionFilePaths?.Count() > 0)
            {
                symbolFilter.Add(DocIdSymbolFilter.GetFilterForDocIds(_apiExclusionFilePaths));
            }
            symbolFilter.Add(new ImplicitSymbolFilter());
            symbolFilter.Add(accessibilitySymbolFilter);

            // Configure the attribute data symbol filter
            CompositeSymbolFilter attributeDataSymbolFilter = new();
            if (_attributeExclusionFilePaths?.Count() > 0)
            {
                attributeDataSymbolFilter.Add(DocIdSymbolFilter.GetFilterForDocIds(_attributeExclusionFilePaths));
            }
            attributeDataSymbolFilter.Add(accessibilitySymbolFilter);

            return new GenApiAppConfiguration()
            {
                Logger = _logger ?? new ConsoleLog(MessageImportance.Normal),
                OutputType = _outputType,
                OutputPath = _outputPath,
                Header = GetHeader(),
                ExceptionMessage = _exceptionMessage,
                IncludeAssemblyAttributes = _includeAssemblyAttributes,
                Loader = loader,
                AssemblySymbols = assemblySymbols,
                SymbolFilter = symbolFilter,
                AttributeDataSymbolFilter = attributeDataSymbolFilter
            };

        }

        private string GetHeader()
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

            if (_header != null)
            {
#if NET
                _header = header.ReplaceLineEndings();
#else
                _header = Regex.Replace(_header, @"\r\n|\n\r|\n|\r", Environment.NewLine);
#endif
                return _header;
            }

            return defaultFileHeader;
        }

        private void ThrowIfFileNotFound(string argumentName, string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The {argumentName} file was not found: {filePath}");
            }
        }

        private void ThrowIfDirectoryNotFound(string argumentName, string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The {argumentName} directory was not found: {directoryPath}");
            }
        }

        private void ThrowIfPathIsDirectory(string argumentName, string path)
        {
            if (Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"The {argumentName} is a directory, not a file: {path}");
            }
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
