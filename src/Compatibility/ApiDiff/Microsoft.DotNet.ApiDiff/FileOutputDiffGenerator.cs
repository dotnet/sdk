// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.ApiSymbolExtensions;
using System.Diagnostics;

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Generates a markdown diff of two different versions of the same assembly.
/// </summary>
internal sealed class FileOutputDiffGenerator : IDiffGenerator
{
    private readonly ILog _log;
    private readonly string[] _beforeAssembliesFolderPaths;
    private readonly string[] _beforeAssemblyReferencesFolderPaths;
    private readonly string[] _afterAssembliesFolderPaths;
    private readonly string[] _afterAssemblyReferencesFolderPaths;
    private readonly string _outputFolderPath;
    private readonly string _tableOfContentsTitle;
    private readonly string[] _attributesToExclude;
    private readonly string[] _apisToExclude;
    private readonly bool _addPartialModifier;
    private readonly bool _hideImplicitDefaultConstructors;
    private readonly bool _writeToDisk;
    private readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>>? _diagnosticOptions;
    private readonly Dictionary<string, string> _results;

    /// <summary>
    ///
    /// </summary>
    /// <param name="log"></param>
    /// <param name="beforeAssembliesFolderPath"></param>
    /// <param name="beforeAssemblyReferencesFolderPath"></param>
    /// <param name="afterAssembliesFolderPath"></param>
    /// <param name="afterAssemblyReferencesFolderPath"></param>
    /// <param name="outputFolderPath"></param>
    /// <param name="tableOfContentsTitle"></param>
    /// <param name="attributesToExclude">An optional list of attributes to avoid showing in the diff. If <see langword="null"/>, the default list of attributes to exclude <see cref="DiffGeneratorFactory.DefaultAttributesToExclude"/> is used. If an empty list, no attributes are excluded.</param>
    /// <param name="apisToExclude">An optional list of APIs to avoid showing in the diff.</param>
    /// <param name="addPartialModifier"></param>
    /// <param name="hideImplicitDefaultConstructors"></param>
    /// <param name="writeToDisk">If <see langword="true"/>, when calling <see cref="Run"/>, the generated markdown files get written to disk, and no item is added to the <see cref="Run"/> dictionary. If <see langword="false"/>, when calling <see cref="Run"/>, the generated markdown files get added to the <see cref="Run"/> dictionary (with the file path as the dictionary key) and none of them is written to disk. This is meant for testing purposes.</param>
    /// <param name="diagnosticOptions"></param>
    internal FileOutputDiffGenerator(ILog log,
                                    string beforeAssembliesFolderPath,
                                    string? beforeAssemblyReferencesFolderPath,
                                    string afterAssembliesFolderPath,
                                    string? afterAssemblyReferencesFolderPath,
                                    string outputFolderPath,
                                    string tableOfContentsTitle,
                                    string[]? attributesToExclude,
                                    string[]? apisToExclude,
                                    bool addPartialModifier,
                                    bool hideImplicitDefaultConstructors,
                                    bool writeToDisk,
                                    IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)

    {
        _log = log;
        _beforeAssembliesFolderPaths = [beforeAssembliesFolderPath];
        _beforeAssemblyReferencesFolderPaths = beforeAssemblyReferencesFolderPath != null ? [beforeAssemblyReferencesFolderPath] : [];
        _afterAssembliesFolderPaths = [afterAssembliesFolderPath];
        _afterAssemblyReferencesFolderPaths = afterAssemblyReferencesFolderPath != null ? [afterAssemblyReferencesFolderPath] : [];
        _outputFolderPath = outputFolderPath;
        _tableOfContentsTitle = tableOfContentsTitle;
        _attributesToExclude = attributesToExclude ?? DiffGeneratorFactory.DefaultAttributesToExclude;
        _apisToExclude = apisToExclude ?? [];
        _addPartialModifier = addPartialModifier;
        _hideImplicitDefaultConstructors = hideImplicitDefaultConstructors;
        _writeToDisk = writeToDisk;
        _diagnosticOptions = diagnosticOptions ?? DiffGeneratorFactory.DefaultDiagnosticOptions;
        _results = [];
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Results => _results.AsReadOnly();

    /// <inheritdoc/>
    public void Run()
    {
        Debug.Assert(_beforeAssembliesFolderPaths.Length == 1);
        Debug.Assert(_afterAssembliesFolderPaths.Length == 1);

        (IAssemblySymbolLoader beforeLoader, Dictionary<string, IAssemblySymbol> beforeAssemblySymbols) =
            AssemblySymbolLoader.CreateFromFiles(
                _log,
                assembliesPaths: _beforeAssembliesFolderPaths,
                assemblyReferencesPaths: _beforeAssemblyReferencesFolderPaths,
                diagnosticOptions: _diagnosticOptions);

        (IAssemblySymbolLoader afterLoader, Dictionary<string, IAssemblySymbol> afterAssemblySymbols) =
            AssemblySymbolLoader.CreateFromFiles(
                _log,
                assembliesPaths: _afterAssembliesFolderPaths,
                assemblyReferencesPaths: _afterAssemblyReferencesFolderPaths,
                diagnosticOptions: _diagnosticOptions);

        MemoryOutputDiffGenerator generator = new(_log,
                                                  beforeLoader,
                                                  afterLoader,
                                                  beforeAssemblySymbols,
                                                  afterAssemblySymbols,
                                                  _attributesToExclude,
                                                  _apisToExclude,
                                                  _addPartialModifier,
                                                  _hideImplicitDefaultConstructors,
                                                  _diagnosticOptions);
        generator.Run();

        // If true, output is disk. Otherwise, it's the Results dictionary.
        if (_writeToDisk)
        {
            Directory.CreateDirectory(_outputFolderPath);
        }

        string beforeFileName = Path.GetFileName(_beforeAssembliesFolderPaths[0]);
        string afterFileName = Path.GetFileName(_afterAssembliesFolderPaths[0]);

        StringBuilder tableOfContents = new();
        tableOfContents.AppendLine($"# API difference between {beforeFileName} and {afterFileName}");
        tableOfContents.AppendLine();
        tableOfContents.AppendLine("API listing follows standard diff formatting.");
        tableOfContents.AppendLine("Lines preceded by a '+' are additions and a '-' indicates removal.");
        tableOfContents.AppendLine();

        foreach ((string assemblyName, string text) in generator.Results)
        {
            string fileName = $"{_tableOfContentsTitle}_{assemblyName}.md";
            tableOfContents.AppendLine($"* [{assemblyName}]({fileName})");

            string filePath = Path.Combine(_outputFolderPath, fileName);
            if (_writeToDisk)
            {
                File.WriteAllText(filePath, text);
            }
            else
            {
                _results.Add(filePath, text);
            }

            _log.LogMessage($"Wrote '{filePath}'.");
        }

        tableOfContents.AppendLine();

        string tableOfContentsFilePath = Path.Combine(_outputFolderPath, $"{_tableOfContentsTitle}.md");

        if (_writeToDisk)
        {
            File.WriteAllText(tableOfContentsFilePath, tableOfContents.ToString());
        }
        else
        {
            _results.Add(tableOfContentsFilePath, tableOfContents.ToString());
        }

        _log.LogMessage($"Wrote table of contents to '{tableOfContentsFilePath}'.");
    }
}
