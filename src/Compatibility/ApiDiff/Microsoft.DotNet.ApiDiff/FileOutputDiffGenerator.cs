// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

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
    private readonly string _beforeFriendlyName;
    private readonly string _afterFriendlyName;
    private readonly string _tableOfContentsTitle;
    private readonly string[] _assembliesToExclude;
    private readonly string[] _attributesToExclude;
    private readonly string[] _apisToExclude;
    private readonly bool _addPartialModifier;
    private readonly bool _writeToDisk;
    private readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>>? _diagnosticOptions;
    private readonly Dictionary<string, string> _results;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOutputDiffGenerator"/> class.
    /// </summary>
    /// <param name="log">The logger to use for logging messages.</param>
    /// <param name="beforeAssembliesFolderPath">The folder path containing the assemblies before the changes.</param>
    /// <param name="beforeAssemblyReferencesFolderPath">The folder path containing the assembly references before the changes.</param>
    /// <param name="afterAssembliesFolderPath">The folder path containing the assemblies after the changes.</param>
    /// <param name="afterAssemblyReferencesFolderPath">The folder path containing the assembly references after the changes.</param>
    /// <param name="outputFolderPath">The folder path where the output files will be written.</param>
    /// <param name="beforeFriendlyName">The friendly name for the before version of the assemblies.</param>
    /// <param name="afterFriendlyName">The friendly name for the after version of the assemblies.</param>
    /// <param name="tableOfContentsTitle">The title for the table of contents.</param>
    /// <param name="filesWithAssembliesToExclude">An optional array of filepaths each containing a list of assemblies to avoid showing in the diff.</param>
    /// <param name="filesWithAttributesToExclude">An optional array of filepaths each containing a list of attributes to avoid showing in the diff.</param>
    /// <param name="filesWithApisToExclude">An optional array of filepaths each containing a list of APIs to avoid showing in the diff.</param>
    /// <param name="addPartialModifier">A value indicating whether to add the partial modifier to types.</param>
    /// <param name="writeToDisk">If <see langword="true"/>, when calling <see cref="RunAsync"/>, the generated markdown files get written to disk, and no item is added to the <see cref="RunAsync"/> dictionary. If <see langword="false"/>, when calling <see cref="RunAsync"/>, the generated markdown files get added to the <see cref="RunAsync"/> dictionary (with the file path as the dictionary key) and none of them is written to disk. This is meant for testing purposes.</param>
    /// <param name="diagnosticOptions">An optional set of diagnostic options.</param>
    internal FileOutputDiffGenerator(ILog log,
                                    string beforeAssembliesFolderPath,
                                    string? beforeAssemblyReferencesFolderPath,
                                    string afterAssembliesFolderPath,
                                    string? afterAssemblyReferencesFolderPath,
                                    string outputFolderPath,
                                    string beforeFriendlyName,
                                    string afterFriendlyName,
                                    string tableOfContentsTitle,
                                    FileInfo[]? filesWithAssembliesToExclude,
                                    FileInfo[]? filesWithAttributesToExclude,
                                    FileInfo[]? filesWithApisToExclude,
                                    bool addPartialModifier,
                                    bool writeToDisk,
                                    IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)

    {
        _log = log;
        _beforeAssembliesFolderPaths = [beforeAssembliesFolderPath];
        _beforeAssemblyReferencesFolderPaths = beforeAssemblyReferencesFolderPath != null ? [beforeAssemblyReferencesFolderPath] : [];
        _afterAssembliesFolderPaths = [afterAssembliesFolderPath];
        _afterAssemblyReferencesFolderPaths = afterAssemblyReferencesFolderPath != null ? [afterAssemblyReferencesFolderPath] : [];
        _outputFolderPath = outputFolderPath;
        _beforeFriendlyName = beforeFriendlyName;
        _afterFriendlyName = afterFriendlyName;
        _tableOfContentsTitle = tableOfContentsTitle;
        _assembliesToExclude = CollectListsFromFiles(filesWithAssembliesToExclude);
        _attributesToExclude = CollectAttributesFromFilesOrDefaults(filesWithAttributesToExclude);
        _apisToExclude = CollectListsFromFiles(filesWithApisToExclude);
        _addPartialModifier = addPartialModifier;
        _writeToDisk = writeToDisk;
        _diagnosticOptions = diagnosticOptions ?? DiffGeneratorFactory.DefaultDiagnosticOptions;
        _results = [];
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Results => _results.AsReadOnly();

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_beforeAssembliesFolderPaths.Length == 1);
        Debug.Assert(_afterAssembliesFolderPaths.Length == 1);

        cancellationToken.ThrowIfCancellationRequested();

        (IAssemblySymbolLoader beforeLoader, Dictionary<string, IAssemblySymbol> beforeAssemblySymbols) =
            AssemblySymbolLoader.CreateFromFiles(
                _log,
                assembliesPaths: _beforeAssembliesFolderPaths,
                assemblyReferencesPaths: _beforeAssemblyReferencesFolderPaths,
                assembliesToExclude: _assembliesToExclude,
                diagnosticOptions: _diagnosticOptions);

        (IAssemblySymbolLoader afterLoader, Dictionary<string, IAssemblySymbol> afterAssemblySymbols) =
            AssemblySymbolLoader.CreateFromFiles(
                _log,
                assembliesPaths: _afterAssembliesFolderPaths,
                assemblyReferencesPaths: _afterAssemblyReferencesFolderPaths,
                assembliesToExclude: _assembliesToExclude,
                diagnosticOptions: _diagnosticOptions);

        MemoryOutputDiffGenerator generator = new(_log,
                                                  beforeLoader,
                                                  afterLoader,
                                                  beforeAssemblySymbols,
                                                  afterAssemblySymbols,
                                                  _attributesToExclude,
                                                  _apisToExclude,
                                                  _addPartialModifier,
                                                  _diagnosticOptions);

        await generator.RunAsync(cancellationToken).ConfigureAwait(false);

        // If true, output is disk. Otherwise, it's the Results dictionary.
        if (_writeToDisk)
        {
            Directory.CreateDirectory(_outputFolderPath);
        }

        StringBuilder tableOfContents = new();
        tableOfContents.AppendLine($"# API difference between {_beforeFriendlyName} and {_afterFriendlyName}");
        tableOfContents.AppendLine();
        tableOfContents.AppendLine("API listing follows standard diff formatting.");
        tableOfContents.AppendLine("Lines preceded by a '+' are additions and a '-' indicates removal.");
        tableOfContents.AppendLine();

        foreach ((string assemblyName, string text) in generator.Results.OrderBy(r => r.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = $"{_tableOfContentsTitle}_{assemblyName}.md";
            tableOfContents.AppendLine($"* [{assemblyName}]({fileName})");

            string filePath = Path.Combine(_outputFolderPath, fileName);
            if (_writeToDisk)
            {
                await File.WriteAllTextAsync(filePath, text).ConfigureAwait(false);
            }
            else
            {
                _results.Add(filePath, text);
            }

            _log.LogMessage($"Wrote '{filePath}'.");
        }

        string tableOfContentsFilePath = Path.Combine(_outputFolderPath, $"{_tableOfContentsTitle}.md");

        if (_writeToDisk)
        {
            await File.WriteAllTextAsync(tableOfContentsFilePath, tableOfContents.ToString()).ConfigureAwait(false);
        }
        else
        {
            _results.Add(tableOfContentsFilePath, tableOfContents.ToString());
        }

        _log.LogMessage($"Wrote table of contents to '{tableOfContentsFilePath}'.");
    }

    private static string[] CollectListsFromFiles(FileInfo[]? filesWithLists)
    {
        List<string> list = [];

        if (filesWithLists != null)
        {
            foreach (FileInfo file in filesWithLists)
            {
                // This will throw if file does not exist.
                foreach (string line in File.ReadLines(file.FullName))
                {
                    if (!list.Contains(line))
                    {
                        // Prevent duplicates.
                        list.Add(line);
                    }
                }
            }
        }

        return [.. list.Order()];
    }

    private static string[] CollectAttributesFromFilesOrDefaults(FileInfo[]? filesWithLists)
    {
        // If no files are specified, use default attributes
        if (filesWithLists == null || filesWithLists.Length == 0)
        {
            return [
                "T:System.AttributeUsageAttribute",
                "T:System.ComponentModel.EditorBrowsableAttribute",
                "T:System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute",
                "T:System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute",
                "T:System.Windows.Markup.ContentWrapperAttribute",
                "T:System.Windows.TemplatePartAttribute"
            ];
        }

        List<string> list = [];

        foreach (FileInfo file in filesWithLists)
        {
            // Only read files that exist, skip missing files silently
            if (file.Exists)
            {
                foreach (string line in File.ReadLines(file.FullName))
                {
                    if (!list.Contains(line))
                    {
                        // Prevent duplicates.
                        list.Add(line);
                    }
                }
            }
        }

        return [.. list.Order()];
    }
}
