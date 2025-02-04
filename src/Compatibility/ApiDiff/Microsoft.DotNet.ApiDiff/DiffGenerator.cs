// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the

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
public sealed class DiffGenerator
{
    private readonly ILog _log;
    private readonly bool _addPartialModifier;
    private readonly bool _hideImplicitDefaultConstructors;
    private readonly ISymbolFilter _symbolFilter;
    private readonly ISymbolFilter _attributeSymbolFilter;
    private readonly SyntaxTriviaList _twoSpacesTrivia;
    private readonly SyntaxList<AttributeListSyntax> _emptyAttributeList;
    private readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> _diagnosticOptions;

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
    public static readonly IEnumerable<KeyValuePair<string, ReportDiagnostic>> DefaultDiagnosticOptions = new List<KeyValuePair<string, ReportDiagnostic>>() {
            new ("CS8019", ReportDiagnostic.Suppress), // CS8019: Unnecessary using directive.
            new ("CS8597", ReportDiagnostic.Suppress), // CS8597: Thrown value may be null.
        };

    /// <summary>
    /// Generates a markdown diff of two different versions of the same collections of assembly paths and saves the results to disk.
    /// </summary>
    /// <param name="log">A logger to log messages.</param>
    /// <param name="outputFolderPath">The path to the folder where the diffs will be saved.</param>
    /// <param name="attributesToExclude">The optional attributes to exclude from the diff. If <see langword="null"/>, then <see cref="DefaultAttributesToExclude"/> is used.</param>
    /// <param name="beforeAssembliesFolderPath">The path to the folder containing the old (before) assemblies.</param>
    /// <param name="beforeAssemblyReferencesFolderPath">The path to the folder containing the old (before) reference assemblies.</param>
    /// <param name="afterAssembliesFolderPath">The path to the folder containing the new (after) assemblies.</param>
    /// <param name="afterAssemblyReferencesFolderPath">The path to the folder containing the new (after) reference assemblies.</param>
    /// <param name="addPartialModifier">Whether to add the partial modifier to types.</param>
    /// <param name="hideImplicitDefaultConstructors">Whether to hide implicit default constructors.</param>
    /// <param name="diagnosticOptions">The optional diagnostic options to use when generating the diff. If <see langword="null"/> is passed, the default value <see cref="DefaultDiagnosticOptions"/> is used.</param>
    public static void RunAndSaveToDisk(ILog log,
                                        string outputFolderPath,
                                        string[]? attributesToExclude,
                                        string beforeAssembliesFolderPath,
                                        string? beforeAssemblyReferencesFolderPath,
                                        string afterAssembliesFolderPath,
                                        string? afterAssemblyReferencesFolderPath,
                                        bool addPartialModifier,
                                        bool hideImplicitDefaultConstructors,
                                        IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
{
        Dictionary<string, string> results = Run(log,
                          attributesToExclude,
                          beforeAssembliesFolderPath,
                          beforeAssemblyReferencesFolderPath,
                          afterAssembliesFolderPath,
                          afterAssemblyReferencesFolderPath,
                          addPartialModifier,
                          hideImplicitDefaultConstructors,
                          diagnosticOptions);

        Directory.CreateDirectory(outputFolderPath);
        foreach ((string assemblyName, string text) in results)
        {
            string filePath = Path.Combine(outputFolderPath, $"{assemblyName}.md");
            File.WriteAllText(filePath, text);
            log.LogMessage($"Wrote '{filePath}'.");
        }
    }

    /// <summary>
    /// Generates a markdown diff of two different versions of the same collections of assembly paths.
    /// </summary>
    /// <param name="log">A logger to log messages.</param>
    /// <param name="attributesToExclude">The optional attributes to exclude from the diff. If <see langword="null"/>, then <see cref="DefaultAttributesToExclude"/> is used.</param>
    /// <param name="beforeAssembliesFolderPath">The path to the folder containing the old (before) assemblies.</param>
    /// <param name="beforeAssemblyReferencesFolderPath">The path to the folder containing the old (before) reference assemblies.</param>
    /// <param name="afterAssembliesFolderPath">The path to the folder containing the new (after) assemblies.</param>
    /// <param name="afterAssemblyReferencesFolderPath">The path to the folder containing the new (after) reference assemblies.</param>
    /// <param name="addPartialModifier">Whether to add the partial modifier to types.</param>
    /// <param name="hideImplicitDefaultConstructors">Whether to hide implicit default constructors.</param>
    /// <param name="diagnosticOptions">The optional diagnostic options to use when generating the diff. If <see langword="null"/> is passed, the default value <see cref="DefaultDiagnosticOptions"/> is used.</param>
    /// <returns>A dictionary with the assembly names as the keys and their diffs as the values.</returns>
    /// <remarks>This method is public so that it can be unit tested without writing to disk.</remarks>
    public static Dictionary<string, string> Run(ILog log,
                                                 string[]? attributesToExclude,
                                                 string beforeAssembliesFolderPath,
                                                 string? beforeAssemblyReferencesFolderPath,
                                                 string afterAssembliesFolderPath,
                                                 string? afterAssemblyReferencesFolderPath,
                                                 bool addPartialModifier,
                                                 bool hideImplicitDefaultConstructors,
                                                IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        attributesToExclude = (attributesToExclude != null && attributesToExclude.Length > 0) ? attributesToExclude : DefaultAttributesToExclude;

        diagnosticOptions = diagnosticOptions ?? DefaultDiagnosticOptions;

        (IAssemblySymbolLoader beforeLoader, Dictionary<string, IAssemblySymbol> beforeAssemblySymbols) =
            AssemblySymbolLoader.CreateFromFiles(
                log,
                assembliesPaths: [beforeAssembliesFolderPath],
                assemblyReferencesPaths: beforeAssemblyReferencesFolderPath != null ? [beforeAssemblyReferencesFolderPath] : null,
                diagnosticOptions: diagnosticOptions);

        (IAssemblySymbolLoader afterLoader, Dictionary<string, IAssemblySymbol> afterAssemblySymbols) =
            AssemblySymbolLoader.CreateFromFiles(
                log,
                assembliesPaths: [afterAssembliesFolderPath],
                assemblyReferencesPaths: afterAssemblyReferencesFolderPath != null ? [afterAssemblyReferencesFolderPath] : null,
                diagnosticOptions: diagnosticOptions);

        return Run(log,
                   attributesToExclude,
                   beforeLoader,
                   afterLoader,
                   beforeAssemblySymbols,
                   afterAssemblySymbols,
                   addPartialModifier,
                   hideImplicitDefaultConstructors,
                   diagnosticOptions);
    }

    /// <summary>
    /// Generates a markdown diff of two different versions of the same collections of assembly symbols.
    /// </summary>
    /// <param name="log">A logger to log messages.</param>
    /// <param name="attributesToExclude">The attributes to exclude from the diff.</param>
    /// <param name="beforeLoader">The loader for the before assembly.</param>
    /// <param name="afterLoader">The loader for the after assembly.</param>
    /// <param name="beforeAssemblySymbols">The symbols for the before assembly.</param>
    /// <param name="afterAssemblySymbols">The symbols for the after assembly.</param>
    /// <param name="addPartialModifier">Whether to add the partial modifier to types.</param>
    /// <param name="hideImplicitDefaultConstructors">Whether to hide implicit default constructors.</param>
    /// <param name="diagnosticOptions">The optional diagnostic options to use when generating the diff. If <see langword="null"/> is passed, the default value <see cref="DefaultDiagnosticOptions"/> is used.</param>
    /// <returns>A dictionary with the assembly names as the keys and their diffs as the values.</returns>
    /// <remarks>This method is public so that it can be unit tested without reading/writing from/to disk.</remarks>
    public static Dictionary<string, string> Run(ILog log,
                                                 string[] attributesToExclude,
                                                 IAssemblySymbolLoader beforeLoader,
                                                 IAssemblySymbolLoader afterLoader,
                                                 Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
                                                 Dictionary<string, IAssemblySymbol> afterAssemblySymbols,
                                                 bool addPartialModifier,
                                                 bool hideImplicitDefaultConstructors,
                                                 IEnumerable<KeyValuePair<string, ReportDiagnostic>>? diagnosticOptions = null)
    {
        diagnosticOptions = diagnosticOptions ?? DefaultDiagnosticOptions;

        DiffGenerator generator = new(log, attributesToExclude, addPartialModifier, hideImplicitDefaultConstructors, diagnosticOptions);
        return generator.GenerateDiff(beforeLoader, afterLoader, beforeAssemblySymbols, afterAssemblySymbols);
    }

    private DiffGenerator(ILog log, string[] attributesToExclude, bool addPartialModifier, bool hideImplicitDefaultConstructors, IEnumerable<KeyValuePair<string, ReportDiagnostic>> diagnosticOptions)
    {
        _log = log;
        _addPartialModifier = addPartialModifier;
        _hideImplicitDefaultConstructors = hideImplicitDefaultConstructors;
        _diagnosticOptions = diagnosticOptions;

        _symbolFilter = SymbolFilterFactory.GetFilterFromList([], includeExplicitInterfaceImplementationSymbols: true);
        _attributeSymbolFilter = SymbolFilterFactory.GetFilterFromList(attributesToExclude, includeExplicitInterfaceImplementationSymbols: true);

        _twoSpacesTrivia = SyntaxFactory.TriviaList(SyntaxFactory.Space, SyntaxFactory.Space);
        _emptyAttributeList = SyntaxFactory.List<AttributeListSyntax>();
    }

    private Dictionary<string, string> GenerateDiff(IAssemblySymbolLoader beforeLoader,
                                                    IAssemblySymbolLoader afterLoader,
                                                    Dictionary<string, IAssemblySymbol> beforeAssemblySymbols,
                                                    Dictionary<string, IAssemblySymbol> afterAssemblySymbols)
    {
        Dictionary<string, string> results = new();

        foreach ((string assemblyName, IAssemblySymbol beforeAssemblySymbol) in beforeAssemblySymbols)
        {
            StringBuilder sb = new();

            (SyntaxNode beforeAssemblyNode, SemanticModel beforeModel) = GetAssemblyRootNodeAndModel(beforeLoader, beforeAssemblySymbol);

            // See if an assembly with the same name can be found in the diff folder.
            if (afterAssemblySymbols.TryGetValue(assemblyName, out IAssemblySymbol? afterAssemblySymbol))
            {
                (SyntaxNode afterAssemblyNode, SemanticModel afterModel) = GetAssemblyRootNodeAndModel(afterLoader, afterAssemblySymbol);
                // We don't care about changed assembly attributes (for now).
                sb.Append(VisitChildren(beforeAssemblyNode, afterAssemblyNode, beforeModel, afterModel, wereParentAttributesChanged: false));
                // Remove the found ones. The remaining ones will be processed at the end because they're new.
                afterAssemblySymbols.Remove(assemblyName);
            }
            else
            {
                // The assembly was removed in the diff.
                sb.Append(GenerateDeletedDiff(beforeAssemblyNode));
            }

            if (sb.Length > 0)
            {
                results.Add(assemblyName, GetFinalAssemblyDiff(assemblyName, sb.ToString()));
            }
        }

        // Process the remaining assemblies in the diff folder.
        foreach ((string assemblyName, IAssemblySymbol afterAssemblySymbol) in afterAssemblySymbols)
        {
            StringBuilder sb = new();
            (SyntaxNode afterAssemblyNode, _) = GetAssemblyRootNodeAndModel(afterLoader, afterAssemblySymbol);
            sb.Append(GenerateAddedDiff(afterAssemblyNode));

            if (sb.Length > 0)
            {
                results.Add(assemblyName, GetFinalAssemblyDiff(assemblyName, sb.ToString()));
            }
        }

        return results;
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
            };
        }

        if (sb.Length == 0)
        {
            return null;
        }

        return sb.ToString();
    }
}
