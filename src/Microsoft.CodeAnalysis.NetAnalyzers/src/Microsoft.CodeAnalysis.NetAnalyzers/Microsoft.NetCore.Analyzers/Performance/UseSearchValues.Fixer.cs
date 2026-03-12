// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1870: <inheritdoc cref="UseSearchValuesTitle"/>
    /// </summary>
    public abstract class UseSearchValuesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseSearchValuesAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (node is null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    UseSearchValuesCodeFixTitle,
                    cancellationToken => ConvertToSearchValuesAsync(context.Document, node, cancellationToken),
                    equivalenceKey: nameof(UseSearchValuesCodeFixTitle)),
                context.Diagnostics);
        }

        protected abstract ValueTask<(SyntaxNode TypeDeclaration, INamedTypeSymbol? TypeSymbol, bool IsRealType)> GetTypeSymbolAsync(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);

        protected abstract SyntaxNode ReplaceSearchValuesFieldName(SyntaxNode node);

        protected abstract SyntaxNode GetDeclaratorInitializer(SyntaxNode syntax);

        protected abstract SyntaxNode? TryReplaceArrayCreationWithInlineLiteralExpression(IOperation operation);

        private async Task<Document> ConvertToSearchValuesAsync(Document document, SyntaxNode argumentNode, CancellationToken cancellationToken)
        {
            SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;

            if (semanticModel?.Compilation is not { } compilation ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemBuffersSearchValues, out INamedTypeSymbol? searchValues) ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemBuffersSearchValues1, out INamedTypeSymbol? searchValuesOfT) ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions, out INamedTypeSymbol? memoryExtensions) ||
                semanticModel.GetOperation(argumentNode, cancellationToken) is not { } argument ||
                GetArgumentOperationAncestorOrSelf(argument) is not { } argumentOperation)
            {
                return document;
            }

            bool isByte =
                argumentOperation.Parameter?.Type is INamedTypeSymbol parameterType &&
                parameterType.TypeArguments is [var typeArgument] &&
                typeArgument.SpecialType == SpecialType.System_Byte;

            SyntaxNode createArgument = CreateSearchValuesCreateArgument(argumentOperation.Syntax, argumentOperation.Value, out SyntaxNode? memberToRemove, cancellationToken);

            string? removedMemberName = null;

            // If the member we're relacing is not public, and the argument to IndexOfAny was its only use, remove it.
            if (memberToRemove is not null &&
                semanticModel.GetDeclaredSymbol(memberToRemove, cancellationToken) is { } symbolToRemove &&
                symbolToRemove.DeclaredAccessibility is Accessibility.NotApplicable or Accessibility.Private &&
                !symbolToRemove.IsImplementationOfAnyInterfaceMember() &&
                symbolToRemove.Locations.Length == 1)
            {
                var refs = await SymbolFinder.FindReferencesAsync(symbolToRemove, document.Project.Solution, cancellationToken).ConfigureAwait(false);
                var locations = refs.SelectMany(r => r.Locations);
                var documentLocations = locations.Select(loc => (loc.Document.FilePath, loc.Location.SourceSpan));

                if (documentLocations.Distinct().Count() == 1)
                {
                    // A single location in a single document.
                    editor.RemoveNode(memberToRemove);
                    removedMemberName = symbolToRemove.Name;
                }
            }

            string defaultSearchValuesFieldName = GetSearchValuesFieldName(argumentOperation.Value, isByte, removedOriginalMember: removedMemberName is not null);

            string fieldName = defaultSearchValuesFieldName;

            (var typeDeclaration, var typeSymbol, bool isRealType) = await GetTypeSymbolAsync(semanticModel, argumentNode, cancellationToken).ConfigureAwait(false);

            // Find a unique name for the field that does not conflict with other members in scope.
            if (typeSymbol is not null && fieldName != removedMemberName)
            {
                var members = GetAllMemberNamesInScope(typeSymbol).ToArray();
                int memberCount = 1;
                while (members.Contains(fieldName, StringComparer.Ordinal))
                {
                    fieldName = $"{defaultSearchValuesFieldName}{memberCount++}";
                }
            }

            // private static readonly SearchValues<T> s_myValues = SearchValues.Create(argument);
            var newField = generator.FieldDeclaration(
                fieldName,
                generator.TypeExpression(searchValuesOfT.Construct(compilation.GetSpecialType(isByte ? SpecialType.System_Byte : SpecialType.System_Char))),
                Accessibility.Private,
                DeclarationModifiers.Static.WithIsReadOnly(true),
                generator.InvocationExpression(
                    generator.MemberAccessExpression(generator.TypeExpressionForStaticMemberAccess(searchValues), "Create"),
                    createArgument));

            // Allow the user to pick a different name for the method.
            newField = ReplaceSearchValuesFieldName(newField);

            // foo.IndexOfAny(argument) => foo.IndexOfAny(s_myValues)
            editor.ReplaceNode(argumentNode, generator.IdentifierName(fieldName));

            if (isRealType)
            {
                // Insert the new field at the top of the parent type.
                editor.InsertMembers(typeDeclaration, 0, new[] { newField });
            }
            else
            {
                // We are in the 'Program' class of a top-level statements file.
                // Create a new partial Program class with the new field.
                editor.AddMember(typeDeclaration, generator.ClassDeclaration("Program", modifiers: DeclarationModifiers.Partial, members: new[] { newField }));
            }

            // If this was a string IndexOfAny call, we must also insert an AsSpan call.
            if (!isByte &&
                argumentOperation.Parent is IInvocationOperation indexOfAnyOperation &&
                indexOfAnyOperation.Instance?.Syntax is { } stringInstance)
            {
                // foo.IndexOfAny => foo.AsSpan().IndexOfAny
                editor.ReplaceNode(stringInstance, generator.InvocationExpression(generator.MemberAccessExpression(stringInstance, "AsSpan")));

                // We are now using the MemoryExtensions.AsSpan() extension method. Make sure it's in scope.
                ImportSystemNamespaceIfNeeded(editor, memoryExtensions, stringInstance);
            }

            return editor.GetChangedDocument();
        }

        private static void ImportSystemNamespaceIfNeeded(DocumentEditor editor, INamedTypeSymbol memoryExtensions, SyntaxNode node)
        {
            var symbols = editor.SemanticModel.LookupNamespacesAndTypes(node.SpanStart, name: nameof(MemoryExtensions));

            if (!symbols.Contains(memoryExtensions, SymbolEqualityComparer.Default))
            {
                SyntaxNode withoutSystemImport = editor.GetChangedRoot();
                SyntaxNode systemNamespaceImportStatement = editor.Generator.NamespaceImportDeclaration(nameof(System));
                SyntaxNode withSystemImport = editor.Generator.AddNamespaceImports(withoutSystemImport, systemNamespaceImportStatement);
                editor.ReplaceNode(editor.OriginalRoot, withSystemImport);
            }
        }

        private static IArgumentOperation? GetArgumentOperationAncestorOrSelf(IOperation operation) =>
            (operation as IArgumentOperation) ??
            operation.GetAncestor<IArgumentOperation>(OperationKind.Argument);

        private static IEnumerable<string> GetAllMemberNamesInScope(ITypeSymbol? symbol)
        {
            while (symbol != null)
            {
                foreach (ISymbol member in symbol.GetMembers())
                {
                    yield return member.Name;
                }

                symbol = symbol.BaseType;
            }
        }

        private static string GetSearchValuesFieldName(IOperation argument, bool isByte, bool removedOriginalMember)
        {
            if (argument is IConversionOperation conversion)
            {
                if (TryGetNameFromLocalOrFieldReference(conversion.Operand, out string? name))
                {
                    return name;
                }
                else if (conversion.Operand is IInvocationOperation invocation)
                {
                    if (TryGetNameFromLocalOrFieldReference(invocation.Instance, out name))
                    {
                        return name;
                    }
                }
            }
            else if (TryGetNameFromLocalOrFieldReference(argument, out string? name))
            {
                return name;
            }
            else if (argument is IPropertyReferenceOperation propertyReference)
            {
                return CreateFromExistingName(propertyReference.Property.Name);
            }
            else if (argument is IInvocationOperation invocation)
            {
                if (TryGetNameFromLocalOrFieldReference(invocation.Instance, out name))
                {
                    return name;
                }
            }

            return isByte ? "s_myBytes" : "s_myChars";

            bool TryGetNameFromLocalOrFieldReference(IOperation? argument, [NotNullWhen(true)] out string? name)
            {
                if (argument is ILocalReferenceOperation localReference)
                {
                    name = CreateFromExistingName(localReference.Local.Name);
                    return true;
                }
                else if (argument is IFieldReferenceOperation fieldReference)
                {
                    name = CreateFromExistingName(fieldReference.Field.Name);
                    return true;
                }

                name = null;
                return false;
            }

            string CreateFromExistingName(string name)
            {
                if (!name.StartsWith("s_", StringComparison.OrdinalIgnoreCase))
                {
                    if (name.Length >= 2 && IsAsciiLetterUpper(name[0]) && !IsAsciiLetterUpper(name[1]))
                    {
                        name = $"{char.ToLowerInvariant(name[0])}{name[1..]}";
                    }

                    return $"s_{name.TrimStart('_')}";
                }

                return removedOriginalMember
                    ? name
                    : $"{name}SearchValues";

                static bool IsAsciiLetterUpper(char c) => c is >= 'A' and <= 'Z';
            }
        }

        private SyntaxNode CreateSearchValuesCreateArgument(SyntaxNode originalSyntax, IOperation argument, out SyntaxNode? memberToRemove, CancellationToken cancellationToken)
        {
            SyntaxNode createArgument = CreateSearchValuesCreateArgumentCore(originalSyntax, argument, out memberToRemove, cancellationToken);

            // If the argument is an inline array creation, we can transform it into a string literal expression instead.
            if (argument.SemanticModel?.GetOperation(createArgument, cancellationToken) is { } newOperation)
            {
                if (newOperation is IArgumentOperation argumentOperation)
                {
                    newOperation = argumentOperation.Value;
                }

                if (TryReplaceArrayCreationWithInlineLiteralExpression(newOperation) is { } literalExpression)
                {
                    return literalExpression;
                }
            }

            return createArgument;
        }

        private SyntaxNode CreateSearchValuesCreateArgumentCore(SyntaxNode originalSyntax, IOperation argument, out SyntaxNode? memberToRemove, CancellationToken cancellationToken)
        {
            if (argument is IConversionOperation conversion)
            {
                argument = conversion.Operand;
            }

            if (argument is IPropertyReferenceOperation propertyReference)
            {
                if (!propertyReference.Property.IsStatic)
                {
                    // Can't access an instance property from a field initializer.
                    memberToRemove = GetDeclarator(propertyReference.Property);
                    return GetDeclaratorInitializer(memberToRemove);
                }
            }
            else if (TryGetArgumentFromLocalOrFieldReference(argument, out SyntaxNode? createArgument, out memberToRemove))
            {
                return createArgument;
            }
            else if (TryGetArgumentFromStringToCharArray(argument, out createArgument, out memberToRemove))
            {
                return createArgument;
            }

            // Use the original syntax (e.g. string literal, inline array creation, static property reference ...)
            memberToRemove = null;
            return originalSyntax;

            bool TryGetArgumentFromStringToCharArray(IOperation operation, [NotNullWhen(true)] out SyntaxNode? createArgument, out SyntaxNode? memberToRemove)
            {
                if (operation is IInvocationOperation invocation &&
                    invocation.Instance is { } stringInstance)
                {
                    Debug.Assert(invocation.TargetMethod.Name == nameof(string.ToCharArray));

                    if (!TryGetArgumentFromLocalOrFieldReference(stringInstance, out createArgument, out memberToRemove))
                    {
                        // This is a string.ToCharArray call, but the string instance is not something we can refer to by name.
                        // e.g. for '"foo".ToCharArray()', we want to emit '"foo"'.
                        createArgument = stringInstance.Syntax;
                    }

                    return true;
                }

                createArgument = null;
                memberToRemove = null;
                return false;
            }

            bool TryGetArgumentFromLocalOrFieldReference(IOperation operation, [NotNullWhen(true)] out SyntaxNode? createArgument, out SyntaxNode? memberToRemove)
            {
                if (operation is ILocalReferenceOperation localReference)
                {
                    // Local string literal would be out of scope in the field declaration.
                    memberToRemove = GetDeclarator(localReference.Local);
                    createArgument = GetDeclaratorInitializer(memberToRemove);
                    return true;
                }
                else if (operation is IFieldReferenceOperation fieldReference)
                {
                    if (!fieldReference.ConstantValue.HasValue)
                    {
                        // If we were to use the field reference directly, we risk initializing the SearchValues field to an empty
                        // instance depending on field declaration order.
                        memberToRemove = GetDeclarator(fieldReference.Field);
                        createArgument = GetDeclaratorInitializer(memberToRemove);
                        return true;
                    }
                }

                createArgument = null;
                memberToRemove = null;
                return false;
            }

            SyntaxNode GetDeclarator(ISymbol symbol)
            {
                Debug.Assert(symbol.DeclaringSyntaxReferences.Length == 1);

                return symbol.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
            }
        }
    }
}