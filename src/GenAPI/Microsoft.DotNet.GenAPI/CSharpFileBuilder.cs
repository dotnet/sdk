// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.DotNet.ApiSymbolExtensions;
using System.Diagnostics;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Processes assembly symbols to build corresponding structures in C# language.
    /// </summary>
    public class CSharpFileBuilder : IAssemblySymbolWriter, IDisposable
    {
        private readonly TextWriter _textWriter;
        private readonly ISymbolFilter _symbolFilter;
        private readonly CSharpSyntaxRewriter _syntaxRewriter;

        private readonly AdhocWorkspace _adhocWorkspace;
        private readonly SyntaxGenerator _syntaxGenerator;

        private readonly IEnumerable<MetadataReference> _metadataReferences;

        public CSharpFileBuilder(
            ISymbolFilter symbolFilter,
            TextWriter textWriter,
            CSharpSyntaxRewriter syntaxRewriter,
            IEnumerable<MetadataReference> metadataReferences)
        {
            _textWriter = textWriter;
            _symbolFilter = symbolFilter;
            _syntaxRewriter = syntaxRewriter;

            _adhocWorkspace = new AdhocWorkspace();
            _syntaxGenerator = SyntaxGenerator.GetGenerator(_adhocWorkspace, LanguageNames.CSharp);

            _metadataReferences = metadataReferences;
        }

        /// <inheritdoc />
        public void WriteAssembly(IAssemblySymbol assembly) => Visit(assembly);

        private void Visit(IAssemblySymbol assembly)
        {
            CSharpCompilationOptions compilationOptions = new(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable);
            Project project = _adhocWorkspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(), VersionStamp.Create(), assembly.Name, assembly.Name, LanguageNames.CSharp,
                compilationOptions: compilationOptions));
            project = project.AddMetadataReferences(_metadataReferences);

            IEnumerable<INamespaceSymbol> namespaceSymbols = EnumerateNamespaces(assembly).Where(_symbolFilter.Include);
            List<SyntaxNode> namespaceSyntaxNodes = new();

            foreach (INamespaceSymbol namespaceSymbol in namespaceSymbols.Order())
            {
                SyntaxNode? syntaxNode = Visit(namespaceSymbol);

                if (syntaxNode is not null)
                {
                    namespaceSyntaxNodes.Add(syntaxNode);
                }
            }

            SyntaxNode compilationUnit = _syntaxGenerator.CompilationUnit(namespaceSyntaxNodes)
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation)
                .NormalizeWhitespace();

            compilationUnit = _syntaxRewriter.Visit(compilationUnit);

            Document document = project.AddDocument(assembly.Name, compilationUnit);
            document = Simplifier.ReduceAsync(document).Result;
            document = Formatter.FormatAsync(document, DefineFormattingOptions()).Result;

            compilationUnit = document.GetSyntaxRootAsync().Result!;
            compilationUnit.WriteTo(_textWriter);
        }

        private SyntaxNode? Visit(INamespaceSymbol namespaceSymbol)
        {
            SyntaxNode namespaceNode = _syntaxGenerator.NamespaceDeclaration(namespaceSymbol.ToDisplayString());

            IEnumerable<INamedTypeSymbol> typeMembers = namespaceSymbol.GetTypeMembers().Where(_symbolFilter.Include);
            if (!typeMembers.Any())
            {
                return null;
            }

            foreach (INamedTypeSymbol typeMember in typeMembers.Order())
            {
                SyntaxNode typeDeclaration = _syntaxGenerator.DeclarationExt(typeMember);

                foreach (AttributeData attribute in typeMember.GetAttributes()
                    .Where(a => a.AttributeClass != null && _symbolFilter.Include(a.AttributeClass)))
                {
                    typeDeclaration = _syntaxGenerator.AddAttributes(typeDeclaration, _syntaxGenerator.Attribute(attribute));
                }

                typeDeclaration = Visit(typeDeclaration, typeMember);

                namespaceNode = _syntaxGenerator.AddMembers(namespaceNode, typeDeclaration);
            }

            return namespaceNode;
        }

        private static bool walkTypeSymbol(ITypeSymbol ty, HashSet<ITypeSymbol> visited, Func<ITypeSymbol, bool> f)
        {
            visited.Add(ty);
            if (f(ty))
            {
                return true;
            }
            foreach(INamedTypeSymbol memberType in ty.GetTypeMembers())
            {
                if (!visited.Contains(memberType))
                {
                    if (walkTypeSymbol(memberType, visited, f))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsOrContainsReferenceType(ITypeSymbol ty)
        {
            HashSet<ITypeSymbol> visited = new(SymbolEqualityComparer.Default);
            return walkTypeSymbol(ty, visited, ty => ty.IsRefLikeType || ty.IsReferenceType); // assuming it covers ByReference<T>
        }

        private static bool IsOrContainsNonEmptyStruct(ITypeSymbol root)
        {
            HashSet<ITypeSymbol> visited = new(SymbolEqualityComparer.Default);
            return walkTypeSymbol(root, visited, ty => {
                if (ty.IsUnmanagedType)
                {
                    return true;
                }

                if (ty.IsReferenceType || ty.IsRefLikeType) // assuming it covers ByReference<T>
                {
                    return !SymbolEqualityComparer.Default.Equals(root, ty);
                }

                return false;
            });
        }

        private SyntaxList<AttributeListSyntax> fromAttributeData(IEnumerable<AttributeData> attrData)
        {
            IEnumerable<SyntaxNode?> syntaxNodes = attrData.Select(ad => ad.ApplicationSyntaxReference?.GetSyntax(new System.Threading.CancellationToken(false)));
            IEnumerable<AttributeListSyntax?> asNodes = syntaxNodes.Select(sn => {
                if (sn is AttributeSyntax ass) {
                    SeparatedSyntaxList<AttributeSyntax> singletonList = SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(ass);
                    AttributeListSyntax alSyntax = SyntaxFactory.AttributeList(singletonList);
                    return alSyntax;
                }
                return null;
            });
            List<AttributeListSyntax> asList = asNodes.Where(a => a != null).OfType<AttributeListSyntax>().ToList();
            return SyntaxFactory.List(asList);
        }

        private IEnumerable<SyntaxNode> synthesizeDummyFields(INamedTypeSymbol namedType) {
            // If it's a value type
            if (namedType.TypeKind != TypeKind.Struct) {
                yield break;
            }

            IEnumerable<IFieldSymbol> excludedFields = namedType.GetMembers()
                .Where(member => !_symbolFilter.Include(member) && member is IFieldSymbol)
                .Select(m => (IFieldSymbol)m);
            
            if (excludedFields.Any())
            {
                IEnumerable<IFieldSymbol> genericTypedFields = excludedFields.Where(f => {
                    if (f.Type is INamedTypeSymbol ty) {
                        return ty.IsGenericType;
                    }
                    return f.Type is ITypeParameterSymbol;
                });

                foreach(IFieldSymbol genericField in genericTypedFields)
                {
                    yield return dummyField(genericField.Type.ToDisplayString(), genericField.Name, fromAttributeData(genericField.GetAttributes()));
                }

                bool hasRefPrivateField = excludedFields.Any(f => IsOrContainsReferenceType(f.Type));

                if (hasRefPrivateField)
                {
                    // add reference type dummy field
                    yield return dummyField("object", "_dummy", new());

                    // add int field
                    yield return dummyField("int", "_dummyPrimitive", new());
                }
                else
                {
                    bool hasNonEmptyStructPrivateField = excludedFields.Any(f => IsOrContainsNonEmptyStruct(f.Type));
                    
                    if (hasNonEmptyStructPrivateField)
                    {
                        // add int field
                        yield return dummyField("int", "_dummyPrimitive", new());
                    }
                }
            }
        }

        private static SyntaxNode dummyField(string typ, string fieldName, SyntaxList<AttributeListSyntax> attrs)
        {
            return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(typ))
                .WithVariables(
                    SyntaxFactory.SingletonSeparatedList<Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax>(
                        SyntaxFactory.VariableDeclarator(
                            SyntaxFactory.Identifier(fieldName))))
            ).WithModifiers(SyntaxFactory.TokenList(new []{SyntaxFactory.Token(SyntaxKind.PrivateKeyword)})
            ).WithAttributeLists(attrs);
        }

        private SyntaxNode Visit(SyntaxNode namedTypeNode, INamedTypeSymbol namedType)
        {
            IEnumerable<ISymbol> members = namedType.GetMembers().Where(_symbolFilter.Include);

            namedTypeNode = _syntaxGenerator.AddMembers(namedTypeNode, synthesizeDummyFields(namedType));

            foreach (ISymbol member in members.Order())
            {
                SyntaxNode memberDeclaration = _syntaxGenerator.DeclarationExt(member);

                foreach (AttributeData attribute in member.GetAttributes()
                    .Where(a => a.AttributeClass != null && _symbolFilter.Include(a.AttributeClass)))
                {
                    memberDeclaration = _syntaxGenerator.AddAttributes(memberDeclaration, _syntaxGenerator.Attribute(attribute));
                }

                if (member is INamedTypeSymbol nestedTypeSymbol)
                {
                    memberDeclaration = Visit(memberDeclaration, nestedTypeSymbol);
                }

                namedTypeNode = _syntaxGenerator.AddMembers(namedTypeNode, memberDeclaration);
            }

            return namedTypeNode;
        }

        private IEnumerable<INamespaceSymbol> EnumerateNamespaces(IAssemblySymbol assemblySymbol)
        {
            Stack<INamespaceSymbol> stack = new();
            stack.Push(assemblySymbol.GlobalNamespace);

            while (stack.Count > 0)
            {
                INamespaceSymbol current = stack.Pop();

                yield return current;

                foreach (INamespaceSymbol subNamespace in current.GetNamespaceMembers())
                {
                    stack.Push(subNamespace);
                }
            }
        }

        private OptionSet DefineFormattingOptions()
        {
            /// TODO: consider to move configuration into file.
            return _adhocWorkspace.Options
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInProperties, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAccessors, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousMethods, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousTypes, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInLambdaExpressionBody, false)
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, false)
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, false)
                .WithChangedOption(CSharpFormattingOptions.NewLineForClausesInQuery, false);
        }

        /// <inheritdoc />
        public void Dispose() => _textWriter.Dispose();
    }
}
