// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI;

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Generates a markdown diff of two different versions of the same assembly.
/// </summary>
public class MemoryOutputDiffGenerator : IDiffGenerator
{
    private readonly ILog _log;
    private readonly IAssemblySymbolLoader _beforeLoader;
    private readonly IAssemblySymbolLoader _afterLoader;
    private readonly ConcurrentDictionary<string, IAssemblySymbol> _beforeAssemblySymbols;
    private readonly ConcurrentDictionary<string, IAssemblySymbol> _afterAssemblySymbols;
    private readonly bool _addPartialModifier;
    private readonly bool _hideImplicitDefaultConstructors;
    private readonly ISymbolFilter _attributeSymbolFilter;
    private readonly ISymbolFilter _symbolFilter;
    private readonly SyntaxTriviaList _twoSpacesTrivia;
    private readonly SyntaxToken _missingCloseBrace;
    private readonly SyntaxTrivia _endOfLineTrivia;
    private readonly SyntaxList<AttributeListSyntax> _emptyAttributeList;
    private readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> _diagnosticOptions;
    private readonly ConcurrentDictionary<string, string> _results;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryOutputDiffGenerator"/> class.
    /// </summary>
    /// <param name="log"></param>
    /// <param name="beforeLoader"></param>
    /// <param name="afterLoader"></param>
    /// <param name="beforeAssemblySymbols"></param>
    /// <param name="afterAssemblySymbols"></param>
    /// <param name="attributesToExclude">An optional list of attributes to avoid showing in the diff. If <see langword="null"/>, the default list of attributes to exclude <see cref="DiffGeneratorFactory.DefaultAttributesToExclude"/> is used. If an empty list, no attributes are excluded.</param>
    /// <param name="apisToExclude">An optional list of APIs to avoid showing in the diff.</param>
    /// <param name="addPartialModifier"></param>
    /// <param name="hideImplicitDefaultConstructors"></param>
    /// <param name="diagnosticOptions"></param>
    internal MemoryOutputDiffGenerator(
        ILog log,
        IAssemblySymbolLoader beforeLoader,
        IAssemblySymbolLoader afterLoader,
        Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
        Dictionary<string, IAssemblySymbol> afterAssemblySymbols,
        string[]? attributesToExclude,
        string[]? apisToExclude,
        bool addPartialModifier,
        bool hideImplicitDefaultConstructors,
        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        _log = log;
        _beforeLoader = beforeLoader;
        _afterLoader = afterLoader;
        _beforeAssemblySymbols = new ConcurrentDictionary<string, IAssemblySymbol>(beforeAssemblySymbols);
        _afterAssemblySymbols = new ConcurrentDictionary<string, IAssemblySymbol>(afterAssemblySymbols);
        _addPartialModifier = addPartialModifier;
        _hideImplicitDefaultConstructors = hideImplicitDefaultConstructors;
        _diagnosticOptions = diagnosticOptions ?? DiffGeneratorFactory.DefaultDiagnosticOptions;
        _attributeSymbolFilter = SymbolFilterFactory.GetFilterFromList(attributesToExclude ?? DiffGeneratorFactory.DefaultAttributesToExclude, includeExplicitInterfaceImplementationSymbols: true);
        _symbolFilter = SymbolFilterFactory.GetFilterFromList(apisToExclude ?? [], includeExplicitInterfaceImplementationSymbols: true);
        _twoSpacesTrivia = SyntaxFactory.TriviaList(SyntaxFactory.Space, SyntaxFactory.Space);
        _missingCloseBrace = SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken);
        _emptyAttributeList = SyntaxFactory.List<AttributeListSyntax>();
        _results = [];
        _endOfLineTrivia = Environment.NewLine == "\r\n" ? SyntaxFactory.CarriageReturnLineFeed : SyntaxFactory.LineFeed;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Results => _results.AsReadOnly();

    /// <inheritdoc/>
    public async Task RunAsync()
    {
        Stopwatch swRun = Stopwatch.StartNew();

        List<Task> mainForLoopTasks = [];

        foreach ((string beforeAssemblyName, IAssemblySymbol beforeAssemblySymbol) in _beforeAssemblySymbols)
        {
            mainForLoopTasks.Add(Task.Run(async () =>
            {
                StringBuilder sb = new();

                _log.LogMessage($"Visiting assembly {beforeAssemblySymbol.Name} (before vs after)...");

                Stopwatch swAssembly = Stopwatch.StartNew();
                (SyntaxNode beforeAssemblyNode, SemanticModel beforeModel) = await GetAssemblyRootNodeAndModelAsync(_beforeLoader, beforeAssemblySymbol).ConfigureAwait(false);
                string finalMessage = $"Finished visiting {beforeAssemblySymbol.Name} (before) in {swAssembly.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

                // See if an assembly with the same name can be found in the diff folder.
                if (_afterAssemblySymbols.TryGetValue(beforeAssemblyName, out IAssemblySymbol? afterAssemblySymbol))
                {
                    swAssembly = Stopwatch.StartNew();
                    (SyntaxNode afterAssemblyNode, SemanticModel afterModel) = await GetAssemblyRootNodeAndModelAsync(_afterLoader, afterAssemblySymbol).ConfigureAwait(false);
                    finalMessage += $"Finished visiting {afterAssemblySymbol.Name} (after) in {swAssembly.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

                    // We don't care about changed assembly attributes (for now).
                    swAssembly = Stopwatch.StartNew();
                    sb.Append(VisitChildren(beforeAssemblyNode, afterAssemblyNode, beforeModel, afterModel, ChangeType.Unchanged, wereParentAttributesChanged: false));
                    finalMessage += $"Finished generating diff for {afterAssemblySymbol.Name} (before vs after) in {swAssembly.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

                    _log.LogMessage(finalMessage);
                    swAssembly.Stop();

                    // Remove the found ones. The remaining ones will be processed at the end because they're new.
                    _afterAssemblySymbols.Remove(beforeAssemblyName, out _);
                }
                else
                {
                    // The assembly was removed in the diff.
                    //sb.Append(GenerateDeletedDiff(beforeAssemblyNode));
                    sb.Append(VisitChildren(beforeAssemblyNode, afterParentNode: null, beforeModel, afterModel: null, ChangeType.Deleted, wereParentAttributesChanged: false));
                }

                if (sb.Length > 0)
                {
                    _results.TryAdd(beforeAssemblyName, GetFinalAssemblyDiff(beforeAssemblyName, sb.ToString()));
                }
            }));
        }

        // This needs to block, because the tasks remove items from _afterAssemblySymbols, processed in the forloop below
        await Task.WhenAll(mainForLoopTasks).ConfigureAwait(false);

        List<Task> rightSideForLoopTasks = [];

        // Process the remaining assemblies in the diff folder.
        foreach ((string afterAssemblyName, IAssemblySymbol afterAssemblySymbol) in _afterAssemblySymbols)
        {
            rightSideForLoopTasks.Add(Task.Run(async () =>
            {
                StringBuilder sb = new();
                _log.LogMessage($"Visiting *new* assembly {afterAssemblySymbol.Name}...");
                
                var swNew = Stopwatch.StartNew();
                (SyntaxNode afterAssemblyNode, SemanticModel afterModel) = await GetAssemblyRootNodeAndModelAsync(_afterLoader, afterAssemblySymbol).ConfigureAwait(false);
                string finalMessage = $"Finished visiting *new* {afterAssemblySymbol.Name} in {swNew.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

                // We don't care about changed assembly attributes (for now).
                swNew = Stopwatch.StartNew();
                sb.Append(VisitChildren(beforeParentNode: null, afterAssemblyNode, beforeModel: null, afterModel, ChangeType.Inserted, wereParentAttributesChanged: false));
                finalMessage += $"Finished generating diff for *new* assembly {afterAssemblySymbol.Name} in {swNew.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

                _log.LogMessage(finalMessage);
                swNew.Stop();

                if (sb.Length > 0)
                {
                    _results.TryAdd(afterAssemblyName, GetFinalAssemblyDiff(afterAssemblyName, sb.ToString()));
                }
            }));
        }

        await Task.WhenAll(rightSideForLoopTasks).ConfigureAwait(false);

        _log.LogMessage($"FINAL TOTAL: {swRun.Elapsed.TotalMilliseconds / 1000.0 / 60.0:F2} mins.");
        swRun.Stop();
    }

    private static string GetFinalAssemblyDiff(string assemblyName, string diffText)
    {
        StringBuilder sbAssembly = new();
        sbAssembly.AppendLine($"# {assemblyName}");
        sbAssembly.AppendLine();
        sbAssembly.AppendLine("```diff");
        sbAssembly.Append(diffText.TrimEnd()); // Leading text stays, it's valid indentation.
        sbAssembly.AppendLine();
        sbAssembly.AppendLine("```");
        return sbAssembly.ToString();
    }

    private string? VisitChildren(SyntaxNode? beforeParentNode, SyntaxNode? afterParentNode, SemanticModel? beforeModel, SemanticModel? afterModel, ChangeType parentChangeType, bool wereParentAttributesChanged)
    {
        Debug.Assert(beforeParentNode != null || afterParentNode != null);

        ConcurrentDictionary<string, MemberDeclarationSyntax> beforeChildrenNodes = CollectChildrenNodes(beforeParentNode, beforeModel);
        ConcurrentDictionary<string, MemberDeclarationSyntax> afterChildrenNodes = CollectChildrenNodes(afterParentNode, afterModel);

        StringBuilder sb = new();
        // Traverse all the elements found on the left side. This only visits unchanged, modified and deleted APIs.
        // In other words, this loop excludes those that are new on the right. Those are handled later.
        foreach ((string beforeMemberName, MemberDeclarationSyntax beforeMemberNode) in beforeChildrenNodes.OrderBy(x => x.Key))
        {
            if (afterChildrenNodes.TryGetValue(beforeMemberName, out MemberDeclarationSyntax? afterMemberNode) &&
                beforeMemberNode.Kind() == afterMemberNode.Kind())
            {
                if (beforeMemberNode is BaseTypeDeclarationSyntax && afterMemberNode is BaseTypeDeclarationSyntax ||
                    beforeMemberNode is BaseNamespaceDeclarationSyntax && afterMemberNode is BaseNamespaceDeclarationSyntax)
                {
                    // At this point, the current API (which is a type or a namespace) is considered unmodified in its signature.
                    // Before visiting its children, which might have changed, we need to check if the current API has attributes that changed, and append the diff if needed.
                    // The children visitation should take care of wrapping the children with the current 'unmodified' parent API.
                    string? attributes = VisitAttributes(beforeMemberNode, afterMemberNode, beforeModel, afterModel);
                    if (attributes != null)
                    {
                        sb.Append(attributes);
                    }
                    sb.Append(VisitChildren(beforeMemberNode, afterMemberNode, beforeModel, afterModel, ChangeType.Unchanged, wereParentAttributesChanged: attributes != null));
                }
                else
                {
                    // This returns the current member (as changed) topped with its added/deleted/changed attributes.
                    sb.Append(VisitLeafNode(beforeMemberNode, afterMemberNode, beforeModel, afterModel, ChangeType.Modified));
                }
                // Remove the found ones. The remaining ones will be processed at the end because they're new.
                afterChildrenNodes.Remove(beforeMemberName, out _);
            }
            else
            {
                // This returns the current member (as deleted) topped with its attributes as deleted.
                if (beforeMemberNode is BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax)
                {
                    string? attributes = VisitAttributes(beforeMemberNode, afterMemberNode, beforeModel, afterModel);
                    if (attributes != null)
                    {
                        sb.Append(attributes);
                    }
                    sb.Append(VisitChildren(beforeMemberNode, afterParentNode: null, beforeModel, afterModel: null, ChangeType.Deleted, wereParentAttributesChanged: true));
                }
                else
                {
                    sb.Append(VisitLeafNode(beforeMemberNode, afterNode: null, beforeModel, afterModel, ChangeType.Deleted));
                }
            }
        }

        if (!afterChildrenNodes.IsEmpty)
        {
            // Traverse all the elements that are new on the right side which were not found on the left. They are all treated as new APIs.
            foreach ((string newMemberName, MemberDeclarationSyntax newMemberNode) in afterChildrenNodes.OrderBy(x => x.Key))
            {
                // Need to do a full visit of each member of namespaces and types anyway, so that leaf bodies are removed
                if (newMemberNode is BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax)
                {
                    string? attributes = VisitAttributes(beforeNode: null, newMemberNode, beforeModel, afterModel);
                    if (attributes != null)
                    {
                        sb.Append(attributes);
                    }
                    sb.Append(VisitChildren(beforeParentNode: null, newMemberNode, beforeModel, afterModel, ChangeType.Inserted, wereParentAttributesChanged: attributes != null));
                }
                else
                {
                    // This returns the current member (as changed) topped with its added/deleted/changed attributes.
                    sb.Append(VisitLeafNode(beforeNode: null, newMemberNode, beforeModel, afterModel, ChangeType.Inserted));
                }
            }
        }

        if (sb.Length > 0 || wereParentAttributesChanged || parentChangeType != ChangeType.Unchanged)
        {
            if (afterParentNode is not null and not CompilationUnitSyntax)
            {
                return GetCodeWrappedByParent(sb.ToString(), afterParentNode, parentChangeType);
            }
            else if (beforeParentNode is not null and not CompilationUnitSyntax)
            {
                return GetCodeWrappedByParent(sb.ToString(), beforeParentNode, parentChangeType);
            }
        }

        return sb.ToString();
    }

    private static ConcurrentDictionary<string, MemberDeclarationSyntax> CollectChildrenNodes(SyntaxNode? parentNode, SemanticModel? model)
    {
        if (parentNode == null)
        {
            return [];
        }

        Debug.Assert(model != null);

        ConcurrentDictionary<string, MemberDeclarationSyntax> dictionary = [];

        if (parentNode is BaseNamespaceDeclarationSyntax)
        {
            foreach (BaseTypeDeclarationSyntax typeNode in parentNode.ChildNodes().Where(n => n is BaseTypeDeclarationSyntax t && IsPublicOrProtected(t)).Cast<BaseTypeDeclarationSyntax>())
            {
                dictionary.TryAdd(GetDocId(typeNode, model), typeNode);
            }
            foreach (DelegateDeclarationSyntax delegateNode in parentNode.ChildNodes().Where(n => n is DelegateDeclarationSyntax d && IsPublicOrProtected(d)).Cast<DelegateDeclarationSyntax>())
            {
                dictionary.TryAdd(GetDocId(delegateNode, model), delegateNode);
            }
        }
        else if (parentNode is BaseTypeDeclarationSyntax)
        {
            foreach (MemberDeclarationSyntax memberNode in parentNode.ChildNodes().Where(n => n is MemberDeclarationSyntax m && IsPublicOrProtected(m)).Cast<MemberDeclarationSyntax>())
            {
                dictionary.TryAdd(GetDocId(memberNode, model), memberNode);
            }
        }
        else if (parentNode is CompilationUnitSyntax)
        {
            foreach (BaseNamespaceDeclarationSyntax namespaceNode in parentNode.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                dictionary.TryAdd(GetDocId(namespaceNode, model), namespaceNode);
            }
        }
        else
        {
            throw new InvalidOperationException(string.Format(Resources.UnexpectedNodeType, parentNode.Kind()));
        }

        return dictionary;
    }

    // Returns the specified leaf node as changed (type, member or namespace) topped with its added/deleted/changed attributes and the correct leading trivia.
    private string? VisitLeafNode(MemberDeclarationSyntax? beforeNode, MemberDeclarationSyntax? afterNode, SemanticModel? beforeModel, SemanticModel? afterModel, ChangeType changeType)
    {
        Debug.Assert(beforeNode != null || afterModel != null);
        Debug.Assert(beforeNode == null || (beforeNode != null && beforeModel != null));
        Debug.Assert(afterNode == null || (afterNode != null && afterModel != null));

        StringBuilder sb = new();

        // If the leaf node was added or deleted, the visited attributes will also show as added or deleted.
        // If the attributes were changed, even if the leaf node wasn't, they should also show up as added+deleted.
        string? attributes = VisitAttributes(beforeNode, afterNode, beforeModel, afterModel);
        bool wereAttributesModified = attributes != null;
        if (wereAttributesModified)
        {
            sb.Append(attributes);
        }

        // The string builder contains the attributes. Now append them on top of the member without their default attributes.
        MemberDeclarationSyntax? deconstructedBeforeNode = GetNodeWithoutAttributes(beforeNode);
        MemberDeclarationSyntax? deconstructedAfterNode = GetNodeWithoutAttributes(afterNode);

        // Special case: types can be leaves if they are completely getting deleted or completely getting added
        // For actual children of types, we need to remove their body.
        deconstructedBeforeNode = RemoveLeafNodeBody(deconstructedBeforeNode, changeType);
        deconstructedAfterNode = RemoveLeafNodeBody(deconstructedAfterNode, changeType);

        Debug.Assert(deconstructedBeforeNode != null || deconstructedAfterNode != null);

        switch (changeType)
        {
            case ChangeType.Deleted:
                Debug.Assert(deconstructedBeforeNode != null);
                sb.Append(GenerateDeletedDiff(deconstructedBeforeNode));
                break;
            case ChangeType.Inserted:
                Debug.Assert(deconstructedAfterNode != null);
                sb.Append(GenerateAddedDiff(deconstructedAfterNode));
                break;
            case ChangeType.Modified:
                Debug.Assert(beforeNode != null && afterNode != null);
                string? changed = GenerateChangedDiff(deconstructedBeforeNode!, deconstructedAfterNode!);
                // At this stage, the attributes that decorated this API have already been analyzed and appended.
                // If the API itself also changed, then we can directly append this API's diff under the attributes (if any).
                if (changed != null)
                {
                    sb.Append(changed);
                }
                // Otherwise, we still need to show the API as unmodified, but only if the attributes also changed.
                else if (attributes != null)
                {
                    Debug.Assert(deconstructedAfterNode != null);
                    sb.Append(GenerateUnchangedDiff(deconstructedAfterNode.WithoutTrailingTrivia()));
                }
                break;
            default:
                throw new NotSupportedException(string.Format(Resources.UnexpectedChangeType, changeType));
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private MemberDeclarationSyntax? RemoveLeafNodeBody(MemberDeclarationSyntax? node, ChangeType changeType)
    {
        if (node == null || node is BaseTypeDeclarationSyntax)
        {
            return node;
        }

        // These syntax kinds should not get the leading trivia appended like the rest of the cases below.
        // For some reason, the two spaces trivia gets duplicated, unsure why.
        if (node is PropertyDeclarationSyntax propertyNode)
        {
            return propertyNode.WithAccessorList(GetEmptiedAccessors(propertyNode.AccessorList));
        }
        else if (node is EventDeclarationSyntax eventNode)
        {
            return eventNode.WithAccessorList(GetEmptiedAccessors(eventNode.AccessorList));
        }
        else if (node is FieldDeclarationSyntax fieldNode)
        {
            return fieldNode;
        }

        SyntaxTriviaList leadingTrivia = (changeType == ChangeType.Unchanged) ? _twoSpacesTrivia.AddRange(node.GetLeadingTrivia()) : node.GetLeadingTrivia();

        MemberDeclarationSyntax changedNode;
        // Everything below has the shape of a method but they don't share a common ancestor, time to cry
        if (node is MethodDeclarationSyntax methodNode)
        {
            changedNode = methodNode.WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Append a semicolon at the end
        }
        else if (node is ConstructorDeclarationSyntax constructorNode)
        {
            changedNode = constructorNode.WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Append a semicolon at the end
        }
        else if (node is OperatorDeclarationSyntax operatorNode)
        {
            changedNode = operatorNode.WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Append a semicolon at the end
        }
        else if (node is ConversionOperatorDeclarationSyntax conversionOperatorNode)
        {
            // These bad boys look like: 'public static explicit operator int(MyClass value)'
            changedNode = conversionOperatorNode.WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Append a semicolon at the end
        }
        else if (node is DestructorDeclarationSyntax destructorNode)
        {
            changedNode = destructorNode.WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Append a semicolon at the end
        }
        // Other kinds remain untouched
        else
        {
            changedNode = node;
        }

        return changedNode.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(_endOfLineTrivia);
    }

    private static AccessorListSyntax? GetEmptiedAccessors(AccessorListSyntax? accessorList)
    {
        SyntaxList<AccessorDeclarationSyntax>? accessors = accessorList?.Accessors;
        if (accessors == null)
        {
            return null;
        }

        IEnumerable<AccessorDeclarationSyntax> newAccessors = accessors.Value.Select(
            static accessorDeclaration => accessorDeclaration.WithBody(null) // remove the default empty body wrapped by brackets
                .WithoutTrailingTrivia() // Remove the single space that follows this new declaration
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)) // Append a semicolon at the end
                .WithTrailingTrivia(SyntaxFactory.Space) // Add a space after the semicolon
        );

        return SyntaxFactory.AccessorList(SyntaxFactory.List(newAccessors));
    }

    [return: NotNullIfNotNull(nameof(node))]
    private MemberDeclarationSyntax? GetNodeWithoutAttributes(MemberDeclarationSyntax? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is BaseNamespaceDeclarationSyntax namespaceNode)
        {
            return namespaceNode.WithAttributeLists(_emptyAttributeList).WithLeadingTrivia(node.GetLeadingTrivia());
        }

        return node.WithAttributeLists(_emptyAttributeList).WithLeadingTrivia(node.GetLeadingTrivia());
    }

    // Returns a non-null string if any attribute was changed (added, deleted or modified). Returns null if all attributes were the same before and after.
    private string? VisitAttributes(MemberDeclarationSyntax? beforeNode, MemberDeclarationSyntax? afterNode, SemanticModel? beforeModel, SemanticModel? afterModel)
    {
        Dictionary<string, AttributeSyntax>? beforeAttributeNodes = CollectAttributeNodes(beforeNode, beforeModel);
        Dictionary<string, AttributeSyntax>? afterAttributeNodes = CollectAttributeNodes(afterNode, afterModel);

        StringBuilder sb = new();
        if (beforeAttributeNodes != null)
        {
            foreach ((string attributeName, AttributeSyntax beforeAttributeNode) in beforeAttributeNodes.OrderBy(x => x.Key))
            {
                if (afterAttributeNodes != null &&
                    afterAttributeNodes.TryGetValue(attributeName, out AttributeSyntax? afterAttributeNode))
                {
                    AttributeListSyntax beforeAttributeList = GetAttributeAsAttributeList(beforeAttributeNode)
                        .WithLeadingTrivia(beforeNode!.GetLeadingTrivia());
                    AttributeListSyntax afterAttributeList = GetAttributeAsAttributeList(afterAttributeNode)
                        .WithLeadingTrivia(afterNode!.GetLeadingTrivia());

                    // We found the same after attribute. Retrieve the comparison string.
                    sb.Append(GenerateChangedDiff(beforeAttributeList, afterAttributeList));
                    // Remove the found ones. The remaining ones will be processed at the end because they're new.
                    afterAttributeNodes.Remove(attributeName);
                }
                else
                {
                    // Retrieve the attribute string as removed.
                    AttributeListSyntax beforeAttributeList = GetAttributeAsAttributeList(beforeAttributeNode)
                        .WithLeadingTrivia(beforeNode!.GetLeadingTrivia());
                    sb.Append(GenerateDeletedDiff(beforeAttributeList));
                }
            }
        }
        // At this point, we may have found changed or deleted attributes (or none).
        // Check now if there were any added attributes.
        if (afterAttributeNodes != null)
        {
            foreach ((_, AttributeSyntax newAttributeNode) in afterAttributeNodes)
            {
                // Retrieve the attribute string as added.
                AttributeListSyntax newAttributeList = GetAttributeAsAttributeList(newAttributeNode)
                        .WithLeadingTrivia(afterNode!.GetLeadingTrivia());
                sb.Append(GenerateAddedDiff(newAttributeList));
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private async Task<(SyntaxNode rootNode, SemanticModel model)> GetAssemblyRootNodeAndModelAsync(IAssemblySymbolLoader loader, IAssemblySymbol assemblySymbol)
    {
        CSharpAssemblyDocumentGenerator docGenerator = new(_log,
                                                           loader,
                                                           _symbolFilter,
                                                           _attributeSymbolFilter,
                                                           exceptionMessage: null,
                                                           includeAssemblyAttributes: false,
                                                           loader.MetadataReferences,
                                                           diagnosticOptions: _diagnosticOptions,
                                                           addPartialModifier: _addPartialModifier,
                                                           hideImplicitDefaultConstructors: _hideImplicitDefaultConstructors);

        // This is a workaround to get the root node and semantic model for a rewritten assembly root node.
        Document oldDocument = await docGenerator.GetDocumentForAssemblyAsync(assemblySymbol).ConfigureAwait(false); // Super hot and resource-intensive path
        SyntaxNode oldRootNode = await docGenerator.GetFormattedRootNodeForDocument(oldDocument).ConfigureAwait(false); // This method rewrites the node and detaches it from the original document.
        Document newDocument = oldDocument.WithSyntaxRoot(oldRootNode); // This links the rewritten node to the document and returns that document, but...
        SyntaxNode? newRootNode = await newDocument.GetSyntaxRootAsync().ConfigureAwait(false); // ... we need to retrieve the root node with the one linked to the document (it's a two way linking).
        SemanticModel? model = await newDocument.GetSemanticModelAsync().ConfigureAwait(false);

        return (newRootNode!, model!);
    }

    // For types, members and namespaces.
    private Dictionary<string, AttributeSyntax> CollectAttributeNodes(MemberDeclarationSyntax? memberNode, SemanticModel? model)
    {
        if (memberNode == null)
        {
            return [];
        }

        Debug.Assert(model != null);

        Dictionary<string, AttributeSyntax> dictionary = [];

        foreach (AttributeListSyntax attributeListNode in memberNode.AttributeLists)
        {
            foreach (AttributeSyntax attributeNode in attributeListNode.Attributes)
            {
                if(model.GetSymbolInfo(attributeNode).Symbol is ISymbol attributeSymbol &&
                   !_attributeSymbolFilter.Include(attributeSymbol.ContainingType))
                {
                    // The attributes decorating an API are actually calls to their constructor method, but the attribute filter needs type docIDs
                    continue;
                }
                var realAttributeNode = attributeNode.WithArgumentList(attributeNode.ArgumentList);
                if (!dictionary.TryAdd(realAttributeNode.ToFullString(), realAttributeNode))
                {
                    _log.LogWarning(string.Format(Resources.AttributeAlreadyExists, realAttributeNode.ToFullString(), GetDocId(memberNode, model)));
                }
            }
        }

        return dictionary;
    }

    private static AttributeListSyntax GetAttributeAsAttributeList(AttributeSyntax attributeNode) =>
        SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList([attributeNode.WithArgumentList(attributeNode.ArgumentList)]));

    private string GetCodeWrappedByParent(string diffedChildrenCode, SyntaxNode parentNode, ChangeType parentChangeType)
    {
        if (parentNode is CompilationUnitSyntax)
        {
            // Nothing to wrap, parent is the actual assembly
            return diffedChildrenCode;
        }

        MemberDeclarationSyntax? memberParentNode = parentNode as MemberDeclarationSyntax;
        Debug.Assert(memberParentNode != null);

        StringBuilder sb = new();

        MemberDeclarationSyntax attributelessParentNode = GetNodeWithoutAttributes(memberParentNode);
        SyntaxNode childlessParentNode = GetChildlessNode(attributelessParentNode);

        SyntaxTriviaList parentLeadingTrivia = parentChangeType == ChangeType.Unchanged ? _twoSpacesTrivia.AddRange(parentNode.GetLeadingTrivia()) : parentNode.GetLeadingTrivia();

        string openingBraceCode = GetDeclarationAndOpeningBraceCode(childlessParentNode, parentLeadingTrivia);
        string? diffedOpeningBraceCode = GetDiffedCode(openingBraceCode);

        string closingBraceCode = GetClosingBraceCode(childlessParentNode, parentLeadingTrivia);
        string? diffedClosingBraceCode = GetDiffedCode(closingBraceCode);

        sb.Append(diffedOpeningBraceCode);
        sb.Append(diffedChildrenCode);
        sb.Append(diffedClosingBraceCode);

        return sb.ToString();

        string? GetDiffedCode(string codeToDiff)
        {
            return parentChangeType switch
            {
                ChangeType.Inserted  => GenerateAddedDiff(codeToDiff),
                ChangeType.Deleted   => GenerateDeletedDiff(codeToDiff),
                ChangeType.Unchanged => codeToDiff,
                _ => throw new InvalidOperationException(string.Format(Resources.UnexpectedChangeType, parentChangeType)),
            };
        }
    }

    private static SyntaxNode GetChildlessNode(MemberDeclarationSyntax node)
    {
        SyntaxNode childlessNode = node.RemoveNodes(node.ChildNodes().Where(
            c => c is MemberDeclarationSyntax or BaseTypeDeclarationSyntax or DelegateDeclarationSyntax), SyntaxRemoveOptions.KeepNoTrivia)!;

        return childlessNode.WithLeadingTrivia(node.GetLeadingTrivia());
    }

    private string GetDeclarationAndOpeningBraceCode(SyntaxNode childlessNode, SyntaxTriviaList leadingTrivia)
    {
        SyntaxToken openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                                             .WithLeadingTrivia(leadingTrivia)
                                             .WithTrailingTrivia(_endOfLineTrivia);

        SyntaxNode unclosedNode = childlessNode switch
        {
            BaseTypeDeclarationSyntax typeDecl => typeDecl.WithOpenBraceToken(openBrace)
                                                          .WithCloseBraceToken(_missingCloseBrace),

            NamespaceDeclarationSyntax nsDecl  => nsDecl.WithOpenBraceToken(openBrace)
                                                        .WithCloseBraceToken(_missingCloseBrace),

            _ => childlessNode
        };

        return unclosedNode.WithLeadingTrivia(leadingTrivia).ToFullString();
    }

    private string GetClosingBraceCode(SyntaxNode childlessNode, SyntaxTriviaList leadingTrivia)
    {
        SyntaxToken closeBrace = childlessNode switch
        {
            BaseTypeDeclarationSyntax typeDecl => typeDecl.CloseBraceToken,
            NamespaceDeclarationSyntax nsDecl => nsDecl.CloseBraceToken,
            _ => throw new InvalidOperationException(string.Format(Resources.UnexpectedNodeType, childlessNode.Kind()))
        };

        return closeBrace.WithTrailingTrivia(_endOfLineTrivia)
                         .WithLeadingTrivia(leadingTrivia)
                         .ToFullString();
    }

    private static bool IsPublicOrProtected(MemberDeclarationSyntax m) =>
        m.Modifiers.Any(SyntaxKind.PublicKeyword) || m.Modifiers.Any(SyntaxKind.ProtectedKeyword);

    private static string GetDocId(SyntaxNode node, SemanticModel model)
    {
        ISymbol? symbol = node switch
        {
            FieldDeclarationSyntax fieldDeclaration           => model.GetDeclaredSymbol(fieldDeclaration.Declaration.Variables.First()),
            EventDeclarationSyntax eventDeclaration           => model.GetDeclaredSymbol(eventDeclaration),
            EventFieldDeclarationSyntax eventFieldDeclaration => model.GetDeclaredSymbol(eventFieldDeclaration.Declaration.Variables.First()),
            PropertyDeclarationSyntax propertyDeclaration     => model.GetDeclaredSymbol(propertyDeclaration),
            _ => model.GetDeclaredSymbol(node)
        };

        if (symbol?.GetDocumentationCommentId() is string docId)
        {
            return docId;
        }

        throw new NullReferenceException(string.Format(Resources.CouldNotGetDocIdForNode, node));
    }

    private static string? GenerateAddedDiff(SyntaxNode afterNode) => GenerateAddedDiff(afterNode.ToFullString());

    private static string? GenerateDeletedDiff(SyntaxNode beforeNode) => GenerateDeletedDiff(beforeNode.ToFullString());

    private static string? GenerateChangedDiff(SyntaxNode beforeNode, SyntaxNode afterNode) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: beforeNode.ToFullString(), newText: afterNode.ToFullString()));

    private static string? GenerateAddedDiff(string afterNodeText) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: string.Empty, newText: afterNodeText));

    private static string? GenerateDeletedDiff(string beforeNodeText) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText:beforeNodeText, newText: string.Empty));

    private static string? GenerateUnchangedDiff(SyntaxNode unchangedNode)
    {
        StringBuilder sb = new();
        string unchangedText = unchangedNode.ToFullString();
        foreach (var line in InlineDiffBuilder.Diff(oldText: unchangedText, newText: unchangedText).Lines)
        {
            sb.AppendLine($"  {line.Text}");
        }
        return sb.ToString();
    }

    private static string? GenerateDiff(DiffPaneModel diff)
    {
        StringBuilder sb = new();

        foreach (var line in diff.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    sb.AppendLine($"+ {line.Text}");
                    break;
                case ChangeType.Deleted:
                    sb.AppendLine($"- {line.Text}");
                    break;
                default:
                    break;
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }
}
