// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.DotNet.ApiDiff.SyntaxRewriter;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI;
using Microsoft.DotNet.GenAPI.SyntaxRewriter;

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
    private readonly ISymbolFilter _attributeSymbolFilter;
    private readonly ISymbolFilter _symbolFilter;
    private readonly SyntaxTriviaList _twoSpacesTrivia;
    private readonly SyntaxToken _missingCloseBrace;
    private readonly SyntaxTrivia _endOfLineTrivia;
    private readonly SyntaxList<AttributeListSyntax> _emptyAttributeList;
    private readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> _diagnosticOptions;
    private readonly ConcurrentDictionary<string, string> _results;
    private readonly ChildrenNodesComparer _childrenNodesComparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryOutputDiffGenerator"/> class.
    /// </summary>
    /// <param name="log">The logger to use for logging messages.</param>
    /// <param name="beforeLoader">The loader for the "before" assembly symbols.</param>
    /// <param name="afterLoader">The loader for the "after" assembly symbols.</param>
    /// <param name="beforeAssemblySymbols">The dictionary of "before" assembly symbols.</param>
    /// <param name="afterAssemblySymbols">The dictionary of "after" assembly symbols.</param>
    /// <param name="attributesToExclude">An optional list of attributes to avoid showing in the diff.</param>
    /// <param name="apisToExclude">An optional list of APIs to avoid showing in the diff.</param>
    /// <param name="addPartialModifier">A boolean indicating whether to add the partial modifier to types.</param>
    /// <param name="diagnosticOptions">An optional dictionary of diagnostic options.</param>
    internal MemoryOutputDiffGenerator(
        ILog log,
        IAssemblySymbolLoader beforeLoader,
        IAssemblySymbolLoader afterLoader,
        Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
        Dictionary<string, IAssemblySymbol> afterAssemblySymbols,
        string[]? attributesToExclude,
        string[]? apisToExclude,
        bool addPartialModifier,
        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        _log = log;
        _beforeLoader = beforeLoader;
        _afterLoader = afterLoader;
        _beforeAssemblySymbols = new ConcurrentDictionary<string, IAssemblySymbol>(beforeAssemblySymbols);
        _afterAssemblySymbols = new ConcurrentDictionary<string, IAssemblySymbol>(afterAssemblySymbols);
        _addPartialModifier = addPartialModifier;
        _diagnosticOptions = diagnosticOptions ?? DiffGeneratorFactory.DefaultDiagnosticOptions;
        _attributeSymbolFilter = SymbolFilterFactory.GetFilterFromList(attributesToExclude ?? [], includeExplicitInterfaceImplementationSymbols: true);
        _symbolFilter = SymbolFilterFactory.GetFilterFromList(apisToExclude ?? [], includeExplicitInterfaceImplementationSymbols: true);
        _twoSpacesTrivia = SyntaxFactory.TriviaList(SyntaxFactory.Space, SyntaxFactory.Space);
        _missingCloseBrace = SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken);
        _emptyAttributeList = SyntaxFactory.List<AttributeListSyntax>();
        _results = [];
        _endOfLineTrivia = Environment.NewLine == "\r\n" ? SyntaxFactory.CarriageReturnLineFeed : SyntaxFactory.LineFeed;
        _childrenNodesComparer = new ChildrenNodesComparer();
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Results => _results.AsReadOnly();

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Stopwatch swRun = Stopwatch.StartNew();

        foreach ((string beforeAssemblyName, IAssemblySymbol beforeAssemblySymbol) in _beforeAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Needs to block so the _afterAssemblySymbols dictionary gets updated.
            await ProcessBeforeAndAfterAssemblyAsync(beforeAssemblyName, beforeAssemblySymbol).ConfigureAwait(false);
        }

        // Needs to happen after processing the before and after assemblies and filtering out the existing ones.
        foreach ((string afterAssemblyName, IAssemblySymbol afterAssemblySymbol) in _afterAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessNewAssemblyAsync(afterAssemblyName, afterAssemblySymbol).ConfigureAwait(false);
        }

        _log.LogMessage($"FINAL TOTAL TIME: {swRun.Elapsed.TotalMilliseconds / 1000.0 / 60.0:F2} mins.");
        swRun.Stop();
    }

    private async Task ProcessBeforeAndAfterAssemblyAsync(string beforeAssemblyName, IAssemblySymbol beforeAssemblySymbol)
    {
        StringBuilder sb = new();

        _log.LogMessage($"Visiting assembly {beforeAssemblySymbol.Name} (before vs after)...");

        Stopwatch swAssembly = Stopwatch.StartNew();
        (SyntaxNode beforeAssemblyNode, SemanticModel beforeModel) = await GetAssemblyRootNodeAndModelAsync(_beforeLoader, beforeAssemblySymbol).ConfigureAwait(false);
        string finalMessage = $"Finished visiting {beforeAssemblySymbol.Name} (before) in {swAssembly.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

        // See if an assembly with the same name can be found in the diff folder.
        if (_afterAssemblySymbols.TryGetValue(beforeAssemblyName, out IAssemblySymbol? afterAssemblySymbol))
        {
            string afterAssemblyString = await ProcessAfterAssemblyAsync(afterAssemblySymbol, beforeAssemblyNode, beforeModel).ConfigureAwait(false);
            sb.Append(afterAssemblyString);

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
    }

    private async Task ProcessNewAssemblyAsync(string afterAssemblyName, IAssemblySymbol afterAssemblySymbol)
    {
        string afterAssemblyString = await ProcessAfterAssemblyAsync(afterAssemblySymbol).ConfigureAwait(false);
        if (afterAssemblyString.Length > 0)
        {
            _results.TryAdd(afterAssemblyName, GetFinalAssemblyDiff(afterAssemblyName, afterAssemblyString));
        }
    }

    private async Task<string> ProcessAfterAssemblyAsync(IAssemblySymbol afterAssemblySymbol, SyntaxNode? beforeParentNode = null, SemanticModel? beforeModel = null)
    {
        StringBuilder sb = new();
        var swNew = Stopwatch.StartNew();
        (SyntaxNode afterAssemblyNode, SemanticModel afterModel) = await GetAssemblyRootNodeAndModelAsync(_afterLoader, afterAssemblySymbol).ConfigureAwait(false);
        string assemblyDescriptor = beforeParentNode == null ? "new" : "existing";
        string finalMessage = $"Finished visiting *{assemblyDescriptor}* assembly {afterAssemblySymbol.Name} in {swNew.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

        swNew = Stopwatch.StartNew();
        ChangeType parentChangeType = beforeParentNode == null ? ChangeType.Inserted : ChangeType.Unchanged;
        // We don't care about changed assembly attributes (for now).
        sb.Append(VisitChildren(beforeParentNode, afterAssemblyNode, beforeModel, afterModel, parentChangeType, wereParentAttributesChanged: false));
        finalMessage += $"Finished generating diff for *{assemblyDescriptor}* assembly {afterAssemblySymbol.Name} in {swNew.Elapsed.TotalMilliseconds / 1000.0:F2}s{Environment.NewLine}";

        _log.LogMessage(finalMessage);
        swNew.Stop();

        return sb.ToString();
    }

    private async Task<(SyntaxNode rootNode, SemanticModel model)> GetAssemblyRootNodeAndModelAsync(IAssemblySymbolLoader loader, IAssemblySymbol assemblySymbol)
    {
        CSharpAssemblyDocumentGeneratorOptions options = new(loader, _symbolFilter, _attributeSymbolFilter)
        {
            HideImplicitDefaultConstructors = false,
            ShouldFormat = true,
            ShouldReduce = false,
            MetadataReferences = loader.MetadataReferences,
            DiagnosticOptions = _diagnosticOptions,
            SyntaxRewriters = [
                // IMPORTANT: The order of these elements matters!
                new TypeDeclarationCSharpSyntaxRewriter(_addPartialModifier), // This must be visited BEFORE GlobalPrefixRemover as it depends on the 'global::' prefix to be found
                GlobalPrefixRemover.Singleton, // And then call this ASAP afterwards so there are fewer identifiers to visit
                PrimitiveSimplificationRewriter.Singleton,
                RemoveBodyCSharpSyntaxRewriter.Singleton,
                SingleLineStatementCSharpSyntaxRewriter.Singleton,
            ],
            AdditionalAnnotations = [Formatter.Annotation] // Formatter is needed to fix some spacing
        };

        CSharpAssemblyDocumentGenerator docGenerator = new(_log, options);

        Document document = await docGenerator.GetDocumentForAssemblyAsync(assemblySymbol).ConfigureAwait(false); // Super hot and resource-intensive path
        SyntaxNode root = await document.GetSyntaxRootAsync().ConfigureAwait(false) ?? throw new InvalidOperationException($"Root node could not be found for document of assembly '{assemblySymbol.Name}'.");
        SemanticModel model = await document.GetSemanticModelAsync().ConfigureAwait(false) ?? throw new InvalidOperationException($"Semantic model could not be found for document of assembly '{assemblySymbol.Name}'.");

        return (root, model);
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
        foreach ((string beforeMemberName, MemberDeclarationSyntax beforeMemberNode) in beforeChildrenNodes.Order(_childrenNodesComparer))
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
            foreach ((string newMemberName, MemberDeclarationSyntax newMemberNode) in afterChildrenNodes.Order(_childrenNodesComparer))
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

    private ConcurrentDictionary<string, MemberDeclarationSyntax> CollectChildrenNodes(SyntaxNode? parentNode, SemanticModel? model)
    {
        if (parentNode == null)
        {
            return [];
        }

        Debug.Assert(model != null);

        ConcurrentDictionary<string, MemberDeclarationSyntax> dictionary = [];

        if (parentNode is BaseNamespaceDeclarationSyntax)
        {
            // Find all types
            foreach (BaseTypeDeclarationSyntax typeNode in GetMembersOfType<BaseTypeDeclarationSyntax>(parentNode))
            {
                dictionary.TryAdd(GetDocId(typeNode, model), typeNode);
            }

            // Find all delegates
            foreach (DelegateDeclarationSyntax delegateNode in GetMembersOfType<DelegateDeclarationSyntax>(parentNode))
            {
                dictionary.TryAdd(GetDocId(delegateNode, model), delegateNode);
            }
        }
        else if (parentNode is BaseTypeDeclarationSyntax)
        {
            // Special case for records that have members
            if (parentNode is RecordDeclarationSyntax record && record.Members.Any())
            {
                foreach (MemberDeclarationSyntax memberNode in GetMembersOfType<MemberDeclarationSyntax>(parentNode))
                {
                    // Note that these could also be nested types
                    dictionary.TryAdd(GetDocId(memberNode, model), memberNode);
                }
            }
            else
            {
                foreach (MemberDeclarationSyntax memberNode in GetMembersOfType<MemberDeclarationSyntax>(parentNode))
                {
                    dictionary.TryAdd(GetDocId(memberNode, model), memberNode);
                }
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
            throw new InvalidOperationException($"Unexpected node type '{parentNode.Kind()}'");
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
                throw new NotSupportedException($"Unexpected change type '{changeType}'.");
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    [return: NotNullIfNotNull(nameof(node))]
    private MemberDeclarationSyntax? GetNodeWithoutAttributes(MemberDeclarationSyntax? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is EnumMemberDeclarationSyntax enumMember)
        {
            SyntaxTriviaList commaTrivia = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, ","));
            return enumMember
                .WithAttributeLists(_emptyAttributeList)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(commaTrivia);
        }
        else if (node is BaseNamespaceDeclarationSyntax namespaceNode)
        {
            return namespaceNode.WithAttributeLists(_emptyAttributeList).WithLeadingTrivia(node.GetLeadingTrivia());
        }
        else if (node is MemberDeclarationSyntax memberDeclaration)
        {
            return memberDeclaration.WithAttributeLists(_emptyAttributeList).WithLeadingTrivia(node.GetLeadingTrivia());
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
                IMethodSymbol? constructor = model.GetSymbolInfo(attributeNode).Symbol as IMethodSymbol;
                if (constructor is null || !_attributeSymbolFilter.Include(constructor.ContainingType))
                {
                    // The attributes decorating an API are actually calls to their
                    // constructor method, but the attribute filter needs type docIDs.
                    continue;
                }
                var realAttributeNode = attributeNode.WithArgumentList(attributeNode.ArgumentList);

                if (!dictionary.TryAdd(GetAttributeDocId(constructor, attributeNode, model), realAttributeNode))
                {
                    _log.LogWarning($"Attribute already exists in dictionary. Attribute: '{realAttributeNode.ToFullString()}', Member: '{GetDocId(memberNode, model)}'");
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
                ChangeType.Inserted => GenerateAddedDiff(codeToDiff),
                ChangeType.Deleted => GenerateDeletedDiff(codeToDiff),
                ChangeType.Unchanged => codeToDiff,
                _ => throw new InvalidOperationException($"Unexpected change type '{parentChangeType}'."),
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
                                             .WithTrailingTrivia(_endOfLineTrivia);

        SyntaxNode unclosedNode;

        if (childlessNode is RecordDeclarationSyntax recordDecl)
        {
            if (recordDecl.OpenBraceToken.IsKind(SyntaxKind.None))
            {
                unclosedNode = recordDecl;
            }
            else
            {
                // Add the missing newline after the declaration
                SyntaxTriviaList endLineAndLeadingTrivia = SyntaxFactory.TriviaList(_endOfLineTrivia).AddRange(leadingTrivia);
                openBrace = openBrace.WithLeadingTrivia(endLineAndLeadingTrivia);
                unclosedNode = recordDecl.WithOpenBraceToken(openBrace).WithCloseBraceToken(_missingCloseBrace);
            }
        }
        else
        {
            openBrace = openBrace.WithLeadingTrivia(leadingTrivia);
            unclosedNode = childlessNode switch
            {
                BaseTypeDeclarationSyntax typeDecl => typeDecl.WithOpenBraceToken(openBrace)
                                                              .WithCloseBraceToken(_missingCloseBrace),

                NamespaceDeclarationSyntax nsDecl => nsDecl.WithOpenBraceToken(openBrace)
                                                            .WithCloseBraceToken(_missingCloseBrace),

                _ => childlessNode
            };
        }

        return unclosedNode.WithLeadingTrivia(leadingTrivia).ToFullString();
    }

    private string GetClosingBraceCode(SyntaxNode childlessNode, SyntaxTriviaList leadingTrivia)
    {
        SyntaxToken closeBrace = childlessNode switch
        {
            RecordDeclarationSyntax recordDecl => recordDecl.CloseBraceToken.IsMissing ? _missingCloseBrace : recordDecl.CloseBraceToken,
            BaseTypeDeclarationSyntax typeDecl => typeDecl.CloseBraceToken,
            NamespaceDeclarationSyntax nsDecl => nsDecl.CloseBraceToken,
            _ => throw new InvalidOperationException($"Unexpected node type '{childlessNode.Kind()}'")
        };

        return closeBrace.WithTrailingTrivia(_endOfLineTrivia)
                         .WithLeadingTrivia(leadingTrivia)
                         .ToFullString();
    }

    private static bool IsEnumMemberOrHasPublicOrProtectedModifierOrIsDestructor(MemberDeclarationSyntax m) =>
        // Destructors don't have visibility modifiers so they're special-cased
        m.Modifiers.Any(SyntaxKind.PublicKeyword) || m.Modifiers.Any(SyntaxKind.ProtectedKeyword) ||
        // Enum member declarations don't have any modifiers
        m is EnumMemberDeclarationSyntax ||
        m.IsKind(SyntaxKind.DestructorDeclaration);

    private static IEnumerable<T> GetMembersOfType<T>(SyntaxNode node) where T : MemberDeclarationSyntax => node
        .ChildNodes()
        .Where(n => n is T m &&
                    // Interface members have no visibility modifiers
                    (node.IsKind(SyntaxKind.InterfaceDeclaration) ||
                     // For the rest of the types, analyze each member directly
                     IsEnumMemberOrHasPublicOrProtectedModifierOrIsDestructor(m)))
        .Cast<T>();

    private string GetAttributeDocId(IMethodSymbol attributeConstructorSymbol, AttributeSyntax attribute, SemanticModel model)
    {
        Debug.Assert(_attributeSymbolFilter.Include(attributeConstructorSymbol.ContainingType));

        string? attributeDocId = null;

        if (attribute.ArgumentList is AttributeArgumentListSyntax list && list.Arguments.Any())
        {
            // If the constructor has arguments, it might be added multiple times on top of an API,
            // so we need to get the unique full string which includes the actual value of the arguments.
            attributeDocId = attribute.ToFullString();
        }
        else
        {
            try
            {
                attributeDocId = attributeConstructorSymbol.ContainingType.GetDocumentationCommentId();
            }
            catch (Exception e)
            {
                _log.LogWarning($"Could not retrieve docId of the attribute constructor of {attribute.ToFullString()}: {e.Message} ");
            }
        }

        // Temporary workaround because some rare cases of attributes in WinForms cannot be found in the current model.
        attributeDocId ??= attribute.ToFullString();

        return attributeDocId;
    }

    private string GetDocId(SyntaxNode node, SemanticModel model)
    {
        ISymbol? symbol = node switch
        {
            DestructorDeclarationSyntax destructorDeclaration => model.GetDeclaredSymbol(destructorDeclaration),
            FieldDeclarationSyntax fieldDeclaration => model.GetDeclaredSymbol(fieldDeclaration.Declaration.Variables.First()),
            EventDeclarationSyntax eventDeclaration => model.GetDeclaredSymbol(eventDeclaration),
            EventFieldDeclarationSyntax eventFieldDeclaration => model.GetDeclaredSymbol(eventFieldDeclaration.Declaration.Variables.First()),
            PropertyDeclarationSyntax propertyDeclaration => model.GetDeclaredSymbol(propertyDeclaration),
            _ => model.GetDeclaredSymbol(node)
        };

        if (symbol?.GetDocumentationCommentId() is string docId)
        {
            if (node is RecordDeclarationSyntax record && record.ParameterList != null && record.ParameterList.Parameters.Any())
            {
                // Special case for when a record has a parameter list, we need to be able to differentiate the signature's parameters too,
                // but the regular DocId does not differentiate that.
                return $"{docId}({record.ParameterList.Parameters.ToFullString()})";
            }
            return docId;
        }

        throw new NullReferenceException($"Could not get the DocID for node: {node}");
    }

    private static string? GenerateAddedDiff(SyntaxNode afterNode) => GenerateAddedDiff(afterNode.ToFullString());

    private static string? GenerateDeletedDiff(SyntaxNode beforeNode) => GenerateDeletedDiff(beforeNode.ToFullString());

    private static string? GenerateChangedDiff(SyntaxNode beforeNode, SyntaxNode afterNode) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: beforeNode.ToFullString(), newText: afterNode.ToFullString()));

    private static string? GenerateAddedDiff(string afterNodeText) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: string.Empty, newText: afterNodeText));

    private static string? GenerateDeletedDiff(string beforeNodeText) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: beforeNodeText, newText: string.Empty));

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

    private class ChildrenNodesComparer : IComparer<KeyValuePair<string, MemberDeclarationSyntax>>
    {
        public int Compare(KeyValuePair<string, MemberDeclarationSyntax> first, KeyValuePair<string, MemberDeclarationSyntax> second)
        {
            // Enum members need to be sorted by their value, not alphabetically, so they need to be special-cased.
            if (first.Value is EnumMemberDeclarationSyntax beforeMember && second.Value is EnumMemberDeclarationSyntax afterMember &&
                beforeMember.EqualsValue is EqualsValueClauseSyntax beforeEVCS && afterMember.EqualsValue is EqualsValueClauseSyntax afterEVCS &&
                beforeEVCS.Value is LiteralExpressionSyntax beforeLes && afterEVCS.Value is LiteralExpressionSyntax afterLes)
            {
                return beforeLes.Token.ValueText.CompareTo(afterLes.Token.ValueText);
            }

            // Everything else is shown alphabetically.
            return first.Key.CompareTo(second.Key);
        }

    }
}
