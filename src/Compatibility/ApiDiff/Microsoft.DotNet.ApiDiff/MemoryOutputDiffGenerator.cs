// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.ApiSymbolExtensions;
using System.Diagnostics;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.GenAPI;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ApiDiff;

/// <summary>
/// Generates a markdown diff of two different versions of the same assembly.
/// </summary>
public class MemoryOutputDiffGenerator : IDiffGenerator
{
    private readonly ILog _log;
    private readonly IAssemblySymbolLoader _beforeLoader;
    private readonly IAssemblySymbolLoader _afterLoader;
    private readonly Dictionary<string, IAssemblySymbol> _beforeAssemblySymbols;
    private readonly Dictionary<string, IAssemblySymbol> _afterAssemblySymbols;
    private readonly bool _addPartialModifier;
    private readonly bool _hideImplicitDefaultConstructors;
    private readonly ISymbolFilter _symbolFilter;
    private readonly ISymbolFilter _attributeSymbolFilter;
    private readonly SyntaxTriviaList _twoSpacesTrivia;
    private readonly SyntaxList<AttributeListSyntax> _emptyAttributeList;
    private readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> _diagnosticOptions;
    private readonly Dictionary<string, string> _results;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryOutputDiffGenerator"/> class.
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
    internal MemoryOutputDiffGenerator(
        ILog log,
        string[] attributesToExclude,
        IAssemblySymbolLoader beforeLoader,
        IAssemblySymbolLoader afterLoader,
        Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
        Dictionary<string, IAssemblySymbol> afterAssemblySymbols,
        bool addPartialModifier,
        bool hideImplicitDefaultConstructors,
        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        _log = log;
        _beforeLoader = beforeLoader;
        _afterLoader = afterLoader;
        _beforeAssemblySymbols = beforeAssemblySymbols;
        _afterAssemblySymbols = afterAssemblySymbols;
        _addPartialModifier = addPartialModifier;
        _hideImplicitDefaultConstructors = hideImplicitDefaultConstructors;
        _diagnosticOptions = diagnosticOptions ?? DiffGeneratorFactory.DefaultDiagnosticOptions;
        _symbolFilter = SymbolFilterFactory.GetFilterFromList([], includeExplicitInterfaceImplementationSymbols: true);
        _attributeSymbolFilter = SymbolFilterFactory.GetFilterFromList(attributesToExclude ?? DiffGeneratorFactory.DefaultAttributesToExclude, includeExplicitInterfaceImplementationSymbols: true);
        _twoSpacesTrivia = SyntaxFactory.TriviaList(SyntaxFactory.Space, SyntaxFactory.Space);
        _emptyAttributeList = SyntaxFactory.List<AttributeListSyntax>();
        _results = [];
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Results => _results.AsReadOnly();

    /// <inheritdoc/>
    public void Run()
    {
        foreach ((string assemblyName, IAssemblySymbol beforeAssemblySymbol) in _beforeAssemblySymbols)
        {
            StringBuilder sb = new();

            (SyntaxNode beforeAssemblyNode, SemanticModel beforeModel) = GetAssemblyRootNodeAndModel(_beforeLoader, beforeAssemblySymbol);

            // See if an assembly with the same name can be found in the diff folder.
            if (_afterAssemblySymbols.TryGetValue(assemblyName, out IAssemblySymbol? afterAssemblySymbol))
            {
                (SyntaxNode afterAssemblyNode, SemanticModel afterModel) = GetAssemblyRootNodeAndModel(_afterLoader, afterAssemblySymbol);
                // We don't care about changed assembly attributes (for now).
                sb.Append(VisitChildren(beforeAssemblyNode, afterAssemblyNode, beforeModel, afterModel, wereParentAttributesChanged: false));
                // Remove the found ones. The remaining ones will be processed at the end because they're new.
                _afterAssemblySymbols.Remove(assemblyName);
            }
            else
            {
                // The assembly was removed in the diff.
                sb.Append(GenerateDeletedDiff(beforeAssemblyNode));
            }

            if (sb.Length > 0)
            {
                _results.Add(assemblyName, GetFinalAssemblyDiff(assemblyName, sb.ToString()));
            }
        }

        // Process the remaining assemblies in the diff folder.
        foreach ((string assemblyName, IAssemblySymbol afterAssemblySymbol) in _afterAssemblySymbols)
        {
            StringBuilder sb = new();
            (SyntaxNode afterAssemblyNode, _) = GetAssemblyRootNodeAndModel(_afterLoader, afterAssemblySymbol);
            sb.Append(GenerateAddedDiff(afterAssemblyNode));

            if (sb.Length > 0)
            {
                _results.Add(assemblyName, GetFinalAssemblyDiff(assemblyName, sb.ToString()));
            }
        }
    }

    private string GetFinalAssemblyDiff(string assemblyName, string diffText)
    {
        StringBuilder sbAssembly = new();
        sbAssembly.AppendLine($"# {Path.GetFileNameWithoutExtension(assemblyName)}");
        sbAssembly.AppendLine();
        sbAssembly.AppendLine("```diff");
        sbAssembly.Append(diffText.TrimEnd()); // Leading text stays, it's usually valid indentation.
        sbAssembly.AppendLine();
        sbAssembly.AppendLine("```");
        return sbAssembly.ToString();
    }

    private string? VisitChildren(SyntaxNode beforeParentNode, SyntaxNode afterParentNode, SemanticModel beforeModel, SemanticModel afterModel, bool wereParentAttributesChanged)
    {
        Dictionary<string, MemberDeclarationSyntax> beforeChildrenNodes = CollectChildrenNodes(beforeParentNode, beforeModel);
        Dictionary<string, MemberDeclarationSyntax> afterChildrenNodes = CollectChildrenNodes(afterParentNode, afterModel);

        StringBuilder sb = new();
        // Traverse all the elements found on the left side. This only visits unchanged, modified and deleted APIs. In other words, this loop excludes those that are new on the right. Those are handled later.
        foreach ((string memberName, MemberDeclarationSyntax beforeMemberNode) in beforeChildrenNodes)
        {
            if (afterChildrenNodes.TryGetValue(memberName, out MemberDeclarationSyntax? afterMemberNode) &&
                beforeMemberNode.Kind() == afterMemberNode.Kind())
            {
                if (beforeMemberNode is BaseTypeDeclarationSyntax && afterMemberNode is BaseTypeDeclarationSyntax ||
                    beforeMemberNode is BaseNamespaceDeclarationSyntax && afterMemberNode is BaseNamespaceDeclarationSyntax)
                {
                    // At this point, the current API (which is a type) is considered unmodified in its signature.
                    // Before visiting its children, we need to check if the attributes have changed.
                    // The children visitation should take care of including the 'unmodified' parts of the parent type.
                    string? attributes = VisitAttributes(beforeMemberNode, afterMemberNode);
                    if (attributes != null)
                    {
                        sb.Append(attributes);
                    }
                    sb.Append(VisitChildren(beforeMemberNode, afterMemberNode, beforeModel, afterModel, wereParentAttributesChanged: attributes != null));
                }
                else
                {
                    // This returns the current member (as changed) topped with its added/deleted/changed attributes.
                    sb.Append(VisitLeafNode(memberName, beforeMemberNode, afterMemberNode, ChangeType.Modified));
                }
                // Remove the found ones. The remaining ones will be processed at the end because they're new.
                afterChildrenNodes.Remove(memberName);
            }
            else
            {
                // This returns the current member (as deleted) topped with its attributes as deleted.
                sb.Append(VisitLeafNode(memberName, beforeMemberNode, afterNode: null, ChangeType.Deleted));
            }
        }

        // Traverse all the elements that are new on the right side which were not found on the left. They are all treated as new APIs.
        foreach ((string memberName, MemberDeclarationSyntax newMemberNode) in afterChildrenNodes)
        {
            // This returns the current member (as added) topped with its attributes as added.
            sb.Append(VisitLeafNode(memberName, beforeNode: null, newMemberNode, ChangeType.Inserted));
        }

        if (sb.Length > 0 || wereParentAttributesChanged)
        {
            return GetCodeWrappedByParent(sb.ToString(), afterParentNode);
        }

        return null;
    }

    private Dictionary<string, MemberDeclarationSyntax> CollectChildrenNodes(SyntaxNode parentNode, SemanticModel model)
    {
        Dictionary<string, MemberDeclarationSyntax> dictionary = new();

        if (parentNode is BaseNamespaceDeclarationSyntax)
        {
            foreach (BaseTypeDeclarationSyntax typeNode in parentNode.ChildNodes().Where(n => n is BaseTypeDeclarationSyntax t && IsPublicOrProtected(t)))
            {
                dictionary.Add(GetDocId(typeNode, model), typeNode);
            }
            foreach (DelegateDeclarationSyntax delegateNode in parentNode.ChildNodes().Where(n => n is DelegateDeclarationSyntax d && IsPublicOrProtected(d)))
            {
                dictionary.Add(GetDocId(delegateNode, model), delegateNode);
            }
        }
        else if (parentNode is BaseTypeDeclarationSyntax)
        {
            foreach (MemberDeclarationSyntax memberNode in parentNode.ChildNodes().Where(n => n is MemberDeclarationSyntax m && IsPublicOrProtected(m)))
            {
                dictionary.Add(GetDocId(memberNode, model), memberNode);
            }
        }
        else if (parentNode is CompilationUnitSyntax)
        {
            foreach (BaseNamespaceDeclarationSyntax namespaceNode in parentNode.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                dictionary.Add(GetDocId(namespaceNode, model), namespaceNode);
            }
        }
        else
        {
            foreach (MemberDeclarationSyntax childNode in parentNode.ChildNodes().Where(n => n is MemberDeclarationSyntax m && IsPublicOrProtected(m)))
            {
                dictionary.Add(GetDocId(childNode, model), childNode);
            }
        }

        return dictionary;
    }

    // Returns the specified leaf node as changed (type, member or namespace) topped with its added/deleted/changed attributes and the correct leading trivia.
    private string? VisitLeafNode(string nodeName, MemberDeclarationSyntax? beforeNode, MemberDeclarationSyntax? afterNode, ChangeType changeType)
    {
        Debug.Assert(beforeNode != null || afterNode != null);

        StringBuilder sb = new();

        // If the leaf node was added or deleted, the visited attributes will also show as added or deleted.
        string? attributes = VisitAttributes(beforeNode, afterNode);
        if (attributes != null)
        {
            sb.Append(attributes);
        }

        // The string builder contains the attributes. Now append them on top of the member without their default attributes.
        SyntaxNode? beforeNodeWithoutAttributes = GetNodeWithoutAttributes(beforeNode);
        SyntaxNode? afterNodeWithoutAttributes = GetNodeWithoutAttributes(afterNode);

        Debug.Assert(beforeNodeWithoutAttributes != null || afterNodeWithoutAttributes != null);

        switch (changeType)
        {
            case ChangeType.Deleted:
                Debug.Assert(beforeNodeWithoutAttributes != null);
                sb.Append(GenerateDeletedDiff(beforeNodeWithoutAttributes));
                break;
            case ChangeType.Inserted:
                Debug.Assert(afterNodeWithoutAttributes != null);
                sb.Append(GenerateAddedDiff(afterNodeWithoutAttributes));
                break;
            case ChangeType.Modified:
                Debug.Assert(beforeNode != null && afterNode != null);
                string? changed = GenerateChangedDiff(beforeNodeWithoutAttributes!, afterNodeWithoutAttributes!);
                // The attributes might have changed, but they were handled before, separately.
                // If the API itself also changed, then we can directly append the changed diff.
                // Otherwise, we still need to show the API as unmodified, but only if the attributes also changed.
                if (changed != null)
                {
                    sb.Append(changed);
                }
                else if (attributes != null)
                {
                    Debug.Assert(afterNodeWithoutAttributes != null);
                    sb.Append($"  {afterNodeWithoutAttributes.ToFullString()}");
                }
                break;
            default:
                throw new NotSupportedException($"Unexpected change type for node {nodeName}: {changeType}");
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    [return: NotNullIfNotNull(nameof(node))]
    private SyntaxNode? GetNodeWithoutAttributes(SyntaxNode? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is MemberDeclarationSyntax memberNode)
        {
            return memberNode.WithAttributeLists(_emptyAttributeList).WithLeadingTrivia(node.GetLeadingTrivia());
        }
        else if (node is BaseNamespaceDeclarationSyntax namespaceNode)
        {
            return namespaceNode.WithAttributeLists(_emptyAttributeList).WithLeadingTrivia(node.GetLeadingTrivia());
        }

        throw new InvalidOperationException($"Unsupported node for removing attributes.");
    }

    // Returns a non-null string if any attribute was changed (added, deleted or modified). Returns null if all attributes were the same before and after.
    private string? VisitAttributes(MemberDeclarationSyntax? beforeNode, MemberDeclarationSyntax? afterNode)
    {
        Dictionary<string, AttributeSyntax>? beforeAttributeNodes = beforeNode != null ? CollectAttributeNodes(beforeNode) : null;
        Dictionary<string, AttributeSyntax>? afterAttributeNodes = afterNode != null ? CollectAttributeNodes(afterNode) : null;

        StringBuilder sb = new();
        if (beforeAttributeNodes != null)
        {
            foreach ((string attributeName, AttributeSyntax beforeAttributeNode) in beforeAttributeNodes)
            {
                // We found a before attribute.
                if (afterAttributeNodes != null &&
                    afterAttributeNodes.TryGetValue(attributeName, out AttributeSyntax? afterAttributeNode))
                {
                    AttributeListSyntax beforeAttributeList = GetAttributeAsAttributeList(beforeAttributeNode, beforeNode!);
                    AttributeListSyntax afterAttributeList = GetAttributeAsAttributeList(afterAttributeNode, afterNode!);

                    // We found the same after attribute. Retrieve the comparison string.
                    sb.Append(GenerateChangedDiff(beforeAttributeList, afterAttributeList));
                    // Remove the found ones. The remaining ones will be processed at the end because they're new.
                    afterAttributeNodes.Remove(attributeName);
                }
                else
                {
                    // Retrieve the attribute string as removed.
                    AttributeListSyntax beforeAttributeList = GetAttributeAsAttributeList(beforeAttributeNode, beforeNode!);
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
                AttributeListSyntax newAttributeList = GetAttributeAsAttributeList(newAttributeNode, afterNode!);
                sb.Append(GenerateAddedDiff(newAttributeList));
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private (SyntaxNode rootNode, SemanticModel model) GetAssemblyRootNodeAndModel(IAssemblySymbolLoader loader, IAssemblySymbol assemblySymbol)
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
        Document oldDocument = docGenerator.GetDocumentForAssembly(assemblySymbol);
        SyntaxNode oldRootNode = docGenerator.GetFormattedRootNodeForDocument(oldDocument); // This method rewrites the node and detaches it from the original document.
        Document newDocument = oldDocument.WithSyntaxRoot(oldRootNode); // This links the rewritten node to the document and returns that document, but...
        SyntaxNode newRootNode = newDocument.GetSyntaxRootAsync().Result!; // ... we need to retrieve the root node with the one linked to the document (it's a two way linking).
        SemanticModel model = newDocument.GetSemanticModelAsync().Result!;

        return (newRootNode, model);
    }

    // For types, members and namespaces.
    private static Dictionary<string, AttributeSyntax> CollectAttributeNodes(MemberDeclarationSyntax memberNode)
    {
        Dictionary<string, AttributeSyntax> dictionary = new();

        foreach (AttributeListSyntax attributeListNode in memberNode.AttributeLists)
        {
            foreach (AttributeSyntax attributeNode in attributeListNode.Attributes)
            {
                dictionary.Add(attributeNode.ToFullString(), attributeNode);
            }
        }

        return dictionary;
    }

    private static AttributeListSyntax GetAttributeAsAttributeList(AttributeSyntax attributeNode, MemberDeclarationSyntax memberNode) =>
        SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList([attributeNode])).WithLeadingTrivia(memberNode.GetLeadingTrivia());

    private string GetCodeWrappedByParent(string childrenText, SyntaxNode parentNode)
    {
        if (parentNode is CompilationUnitSyntax) // Special case for an assembly
        {
            return childrenText;
        }

        StringBuilder sb = new();

        SyntaxNode attributeLessNode = GetNodeWithoutAttributes(parentNode);
        SyntaxNode childlessNode = GetChildlessNode(attributeLessNode);

        sb.Append(GetCodeOfNodeOpening(childlessNode));
        sb.Append(childrenText);
        sb.Append(GetCodeOfNodeClosing(childlessNode));

        return sb.ToString();
    }

    private SyntaxNode GetChildlessNode(SyntaxNode node)
    {
        SyntaxNode childlessNode = node switch
        {
            BaseTypeDeclarationSyntax typeDecl => typeDecl
                .RemoveNodes(typeDecl.ChildNodes()
                .Where(c => c is MemberDeclarationSyntax),
                SyntaxRemoveOptions.KeepNoTrivia)!,

            NamespaceDeclarationSyntax namespaceDecl => namespaceDecl
                .RemoveNodes(namespaceDecl.ChildNodes()
                .Where(c => c is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax),
                SyntaxRemoveOptions.KeepNoTrivia)!,

            _ => node
        };

        return childlessNode.WithLeadingTrivia(node.GetLeadingTrivia());
    }

    private string GetCodeOfNodeOpening(SyntaxNode childlessNode)
    {
        SyntaxToken openBrace = SyntaxFactory.Token(
            leading: _twoSpacesTrivia.AddRange(childlessNode.GetLeadingTrivia()),
            kind: SyntaxKind.OpenBraceToken,
            trailing: SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed));

        SyntaxToken missingCloseBrace = SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken);

        SyntaxNode unclosedNode = childlessNode switch
        {
            BaseTypeDeclarationSyntax typeDecl => typeDecl
                .WithOpenBraceToken(openBrace)
                .WithCloseBraceToken(missingCloseBrace),

            NamespaceDeclarationSyntax nsDecl => nsDecl
                .WithOpenBraceToken(openBrace)
                .WithCloseBraceToken(missingCloseBrace),

            _ => childlessNode
        };

        StringBuilder sb = new();

        sb.Append("  ");
        sb.Append(unclosedNode.GetLeadingTrivia().ToFullString());
        sb.Append(unclosedNode.WithoutTrivia().ToString());

        return sb.ToString();
    }

    private string GetCodeOfNodeClosing(SyntaxNode childlessNode)
    {
        SyntaxToken closeBrace = childlessNode switch
        {
            BaseTypeDeclarationSyntax typeDecl => typeDecl.CloseBraceToken
                        .WithLeadingTrivia(typeDecl.CloseBraceToken.LeadingTrivia.AddRange(_twoSpacesTrivia))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed),

            NamespaceDeclarationSyntax nsDecl => nsDecl.CloseBraceToken
                        .WithLeadingTrivia(nsDecl.CloseBraceToken.LeadingTrivia.AddRange(_twoSpacesTrivia))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed),

            _ => throw new InvalidOperationException("Unexpected node type.")
        };

        StringBuilder sb = new();

        sb.Append(closeBrace.ToFullString());

        return sb.ToString();
    }

    private static bool IsPublicOrProtected(MemberDeclarationSyntax m) =>
        m.Modifiers.Any(SyntaxKind.PublicKeyword) || m.Modifiers.Any(SyntaxKind.ProtectedKeyword);

    private static string GetDocId(SyntaxNode node, SemanticModel model)
    {
        if (node is FieldDeclarationSyntax fieldDeclaration)
        {
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                if (fieldSymbol?.GetDocumentationCommentId() is string fieldDocId)
                {
                    return fieldDocId;
                }
            }
        }
        else
        {
            var symbol = model.GetDeclaredSymbol(node);
            if (symbol?.GetDocumentationCommentId() is string docId)
            {
                return docId;
            }
        }

        throw new NullReferenceException($"Could not get the DocID of the node: {node}");
    }

    private static string? GenerateAddedDiff(SyntaxNode afterNode) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: string.Empty, newText: afterNode.ToFullString()));

    private static string? GenerateChangedDiff(SyntaxNode beforeNode, SyntaxNode afterNode) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: beforeNode.ToFullString(), newText: afterNode.ToFullString()));

    private static string? GenerateDeletedDiff(SyntaxNode beforeNode) =>
        GenerateDiff(InlineDiffBuilder.Diff(oldText: beforeNode.ToFullString(), newText: string.Empty));

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
            ;
        }

        if (sb.Length == 0)
        {
            return null;
        }

        return sb.ToString();
    }
}
