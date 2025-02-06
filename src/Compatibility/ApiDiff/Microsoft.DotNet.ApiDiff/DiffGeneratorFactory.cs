// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiDiff;

public static class DiffGeneratorFactory
{
    /// <summary>
    /// The default attributes to exclude from the diff.
    /// </summary>
    public static readonly string[] DefaultAttributesToExclude = [
        "T:System.AttributeUsageAttribute",
        "T:System.ComponentModel.EditorBrowsableAttribute",
        "T:System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute",
        "T:System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"
    ];

    /// <summary>
    /// The default diagnostic options to use when generating the diff.
    /// </summary>
    public static readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> DefaultDiagnosticOptions = [
        new ("CS8019", ReportDiagnostic.Suppress), // CS8019: Unnecessary using directive.
        new ("CS8597", ReportDiagnostic.Suppress), // CS8597: Thrown value may be null.
    ];

    /// <summary>
    /// Creates a new instance of <see cref="IDiffGenerator"/> that writes the diff to disk.
    /// </summary>
    /// <param name="log"></param>
    /// <param name="beforeAssembliesFolderPath"></param>
    /// <param name="beforeAssemblyReferencesFolderPath"></param>
    /// <param name="afterAssembliesFolderPath"></param>
    /// <param name="afterAssemblyReferencesFolderPath"></param>
    /// <param name="outputFolderPath"></param>
    /// <param name="tableOfContentsTitle"></param>
    /// <param name="attributesToExclude"></param>
    /// <param name="addPartialModifier"></param>
    /// <param name="hideImplicitDefaultConstructors"></param>
    /// <param name="writeToDisk">If <see langword="true"/>, when calling <see cref="IDiffGenerator.Run"/>, the generated markdown files get written to disk, and no item is added to the <see cref="IDiffGenerator.Run"/> dictionary. If <see langword="false"/>, when calling <see cref="IDiffGenerator.Run"/>, the generated markdown files get added to the <see cref="IDiffGenerator.Run"/> dictionary (with the file path as the dictionary key) and none of them is written to disk. This is meant for testing purposes.</param>
    /// <param name="diagnosticOptions"></param>
    /// <returns></returns>
    public static IDiffGenerator Create(ILog log,
                                        string beforeAssembliesFolderPath,
                                        string? beforeAssemblyReferencesFolderPath,
                                        string afterAssembliesFolderPath,
                                        string? afterAssemblyReferencesFolderPath,
                                        string outputFolderPath,
                                        string tableOfContentsTitle,
                                        string[]? attributesToExclude,
                                        bool addPartialModifier,
                                        bool hideImplicitDefaultConstructors,
                                        bool writeToDisk,
                                        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        return new FileOutputDiffGenerator(log,
                                           beforeAssembliesFolderPath,
                                           beforeAssemblyReferencesFolderPath,
                                           afterAssembliesFolderPath,
                                           afterAssemblyReferencesFolderPath,
                                           outputFolderPath,
                                           tableOfContentsTitle,
                                           attributesToExclude,
                                           addPartialModifier,
                                           hideImplicitDefaultConstructors,
                                           writeToDisk,
                                           diagnosticOptions);
    }

    /// <summary>
    /// Creates a new instance of <see cref="IDiffGenerator"/> that writes the diff to memory.
    /// </summary>
    /// <param name="log"></param>
    /// <param name="attributesToExclude"></param>
    /// <param name="beforeLoader"></param>
    /// <param name="afterLoader"></param>
    /// <param name="beforeAssemblySymbols"></param>
    /// <param name="afterAssemblySymbols"></param>
    /// <param name="addPartialModifier"></param>
    /// <param name="hideImplicitDefaultConstructors"></param>
    /// <param name="diagnosticOptions"></param>
    /// <returns></returns>
    public static IDiffGenerator Create(ILog log,
                                        string[] attributesToExclude,
                                        IAssemblySymbolLoader beforeLoader,
                                        IAssemblySymbolLoader afterLoader,
                                        Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
                                        Dictionary<string, IAssemblySymbol> afterAssemblySymbols,
                                        bool addPartialModifier,
                                        bool hideImplicitDefaultConstructors,
                                        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        return new MemoryOutputDiffGenerator(log,
                                             attributesToExclude,
                                             beforeLoader,
                                             afterLoader,
                                             beforeAssemblySymbols,
                                             afterAssemblySymbols,
                                             addPartialModifier,
                                             hideImplicitDefaultConstructors,
                                             diagnosticOptions);
    }
}
