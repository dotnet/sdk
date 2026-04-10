// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiDiff;

public static class DiffGeneratorFactory
{
    /// <summary>
    /// The default diagnostic options to use when generating the diff.
    /// </summary>
    public static readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> DefaultDiagnosticOptions = [
        new ("CS8019", ReportDiagnostic.Suppress), // CS8019: Unnecessary using directive.
        new ("CS8597", ReportDiagnostic.Suppress), // CS8597: Thrown value may be null.
        new ("CS0067", ReportDiagnostic.Suppress), // CS0067: The API is never used.
        new ("CS9113", ReportDiagnostic.Suppress), // CS9113: Parameter is unread.
        new ("CS0501", ReportDiagnostic.Suppress), // CS0501: Method must declare a body because it is not marked abstract.
    ];

    /// <summary>
    /// Creates a new instance of <see cref="IDiffGenerator"/> that writes the diff to disk.
    /// </summary>
    /// <param name="log">The logger to use for logging messages.</param>
    /// <param name="beforeAssembliesFolderPath">The folder path containing the assemblies before the change.</param>
    /// <param name="beforeAssemblyReferencesFolderPath">The folder path containing the assembly references before the change.</param>
    /// <param name="afterAssembliesFolderPath">The folder path containing the assemblies after the change.</param>
    /// <param name="afterAssemblyReferencesFolderPath">The folder path containing the assembly references after the change.</param>
    /// <param name="outputFolderPath">The folder path where the output will be written.</param>
    /// <param name="beforeFriendlyName">The friendly name for the assemblies before the change.</param>
    /// <param name="afterFriendlyName">The friendly name for the assemblies after the change.</param>
    /// <param name="tableOfContentsTitle">The title for the table of contents in the generated diff.</param>
    /// <param name="filesWithAssembliesToExclude">An optional array of filepaths each containing a list of assemblies to avoid showing in the diff. If <see langword="null"/>, no assemblies are excluded.</param>
    /// <param name="filesWithAttributesToExclude">An optional array of filepaths each containing a list of  attributes to avoid showing in the diff.</param>
    /// <param name="filesWithApisToExclude">An optional array of filepaths each containing a list of  APIs to avoid showing in the diff.</param>
    /// <param name="addPartialModifier">Indicates whether to add the partial modifier to types.</param>
    /// <param name="writeToDisk">If <see langword="true"/>, when calling <see cref="IDiffGenerator.RunAsync"/>, the generated markdown files get written to disk, and no item is added to the <see cref="IDiffGenerator.RunAsync"/> dictionary. If <see langword="false"/>, when calling <see cref="IDiffGenerator.RunAsync"/>, the generated markdown files get added to the <see cref="IDiffGenerator.RunAsync"/> dictionary (with the file path as the dictionary key) and none of them is written to disk. This is meant for testing purposes.</param>
    /// <param name="diagnosticOptions">An optional list of diagnostic options to use when generating the diff.</param>
    /// <returns>A new instance of <see cref="IDiffGenerator"/> that writes the diff to disk.</returns>
    /// <returns></returns>
    public static IDiffGenerator Create(ILog log,
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
        return new FileOutputDiffGenerator(log,
                                           beforeAssembliesFolderPath,
                                           beforeAssemblyReferencesFolderPath,
                                           afterAssembliesFolderPath,
                                           afterAssemblyReferencesFolderPath,
                                           outputFolderPath,
                                           beforeFriendlyName,
                                           afterFriendlyName,
                                           tableOfContentsTitle,
                                           filesWithAssembliesToExclude,
                                           filesWithAttributesToExclude,
                                           filesWithApisToExclude,
                                           addPartialModifier,
                                           writeToDisk,
                                           diagnosticOptions);
    }

    /// <summary>
    /// Creates a new instance of <see cref="IDiffGenerator"/> that writes the diff to memory.
    /// </summary>
    /// <param name="log">The logger to use for logging messages.</param>
    /// <param name="beforeLoader">The loader to use for loading the assemblies before the change.</param>
    /// <param name="afterLoader">The loader to use for loading the assemblies after the change.</param>
    /// <param name="beforeAssemblySymbols">The dictionary containing the assembly symbols before the change.</param>
    /// <param name="afterAssemblySymbols">The dictionary containing the assembly symbols after the change.</param>
    /// <param name="attributesToExclude">An optional list of attributes to avoid showing in the diff.</param>
    /// <param name="apisToExclude">An optional list of APIs to avoid showing in the diff.</param>
    /// <param name="addPartialModifier">Indicates whether to add the partial modifier to types.</param>
    /// <param name="diagnosticOptions">An optional list of diagnostic options to use when generating the diff.</param>
    /// <returns>A new instance of <see cref="IDiffGenerator"/> that writes the diff to memory.</returns>
    public static IDiffGenerator Create(ILog log,
                                        IAssemblySymbolLoader beforeLoader,
                                        IAssemblySymbolLoader afterLoader,
                                        Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
                                        Dictionary<string, IAssemblySymbol> afterAssemblySymbols,
                                        string[]? attributesToExclude,
                                        string[]? apisToExclude,
                                        bool addPartialModifier,
                                        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        return new MemoryOutputDiffGenerator(log,
                                             beforeLoader,
                                             afterLoader,
                                             beforeAssemblySymbols,
                                             afterAssemblySymbols,
                                             attributesToExclude,
                                             apisToExclude,
                                             addPartialModifier,
                                             diagnosticOptions);
    }
}
